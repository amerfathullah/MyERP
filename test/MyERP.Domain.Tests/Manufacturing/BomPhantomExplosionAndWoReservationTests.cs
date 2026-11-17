using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class BomPhantomExplosionAndWoReservationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fgItemId = Guid.NewGuid();
    private readonly Guid _kitItemId = Guid.NewGuid();
    private readonly Guid _rmItemId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    // =========================================================================
    // ERPNext PR #59445 / commit fd8e6230f3:
    // Explode phantom BOM rows by their stock qty
    // =========================================================================

    [Fact]
    public async Task BomPhantomExplosion_ExplodesSubBomRowsByStockQty()
    {
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();

        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        // Kit BOM: 1 Kit = 1 RM (valuation rate 10)
        var phantomBomId = Guid.NewGuid();
        var phantomBom = new BillOfMaterials(phantomBomId, _companyId, "BOM-PHANTOM", _kitItemId) { Quantity = 1m };
        phantomBom.Items.Add(new BomItem(Guid.NewGuid(), phantomBomId, _rmItemId, "Raw Material", quantity: 1m, rate: 10m));

        // Parent BOM: 1 FG = 2 Boxes of Kit Item (conversion factor 5 -> 10 units stock qty)
        var parentBomId = Guid.NewGuid();
        var parentBom = new BillOfMaterials(parentBomId, _companyId, "BOM-FG", _fgItemId) { Quantity = 1m };
        var kitBomItem = new BomItem(Guid.NewGuid(), parentBomId, _kitItemId, "Kit Item", quantity: 2m, rate: 50m, uom: "Box", conversionFactor: 5m)
        {
            IsPhantom = true,
            SubBomId = phantomBomId
        };
        parentBom.Items.Add(kitBomItem);

        bomRepo.GetAsync(parentBomId).Returns(Task.FromResult(parentBom));
        bomRepo.GetAsync(phantomBomId).Returns(Task.FromResult(phantomBom));

        var rm = new Item(_rmItemId, _companyId, "RM", "Raw Material", ItemType.Goods);
        var kit = new Item(_kitItemId, _companyId, "KIT", "Kit Item", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { rm, kit }.AsQueryable()));

        // When: Calculate material requirements for 1 FG
        var requirements = await manager.CalculateMaterialRequirementsAsync(parentBomId, produceQty: 1m);

        // Then: RM requirement must be 10 (2 boxes x 5 units conversion x 1 RM per kit), not 2!
        requirements.Length.ShouldBe(1);
        requirements[0].ItemId.ShouldBe(_rmItemId);
        requirements[0].RequiredQty.ShouldBe(10m);
        requirements[0].Rate.ShouldBe(10m);
    }

    [Fact]
    public async Task BomPhantomExplosion_AggregatesSharedRawMaterialsAcrossBoms()
    {
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();

        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        var phantomBomId = Guid.NewGuid();
        var phantomBom = new BillOfMaterials(phantomBomId, _companyId, "BOM-PHANTOM", _kitItemId) { Quantity = 1m };
        phantomBom.Items.Add(new BomItem(Guid.NewGuid(), phantomBomId, _rmItemId, "Raw Material", quantity: 5m, rate: 20m)
        {
            SourceWarehouseId = _warehouseId
        });

        // Parent BOM requires 3 units of RM directly, plus 1 phantom kit (which needs 5 units)
        var parentBomId = Guid.NewGuid();
        var parentBom = new BillOfMaterials(parentBomId, _companyId, "BOM-FG", _fgItemId) { Quantity = 1m };
        parentBom.Items.Add(new BomItem(Guid.NewGuid(), parentBomId, _rmItemId, "Raw Material", quantity: 3m, rate: 10m)
        {
            SourceWarehouseId = _warehouseId
        });
        parentBom.Items.Add(new BomItem(Guid.NewGuid(), parentBomId, _kitItemId, "Kit Item", quantity: 1m, rate: 100m)
        {
            IsPhantom = true,
            SubBomId = phantomBomId
        });

        bomRepo.GetAsync(parentBomId).Returns(Task.FromResult(parentBom));
        bomRepo.GetAsync(phantomBomId).Returns(Task.FromResult(phantomBom));

        var rm = new Item(_rmItemId, _companyId, "RM", "Raw Material", ItemType.Goods);
        var kit = new Item(_kitItemId, _companyId, "KIT", "Kit Item", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { rm, kit }.AsQueryable()));

        var requirements = await manager.CalculateMaterialRequirementsAsync(parentBomId, produceQty: 1m);

        // Aggregated: 3 + 5 = 8 units. Weighted rate: (3 * 10 + 5 * 20) / 8 = 130 / 8 = 16.25
        requirements.Length.ShouldBe(1);
        requirements[0].ItemId.ShouldBe(_rmItemId);
        requirements[0].RequiredQty.ShouldBe(8m);
        requirements[0].Rate.ShouldBe(16.25m);
        requirements[0].SourceWarehouseId.ShouldBe(_warehouseId);
    }

    // =========================================================================
    // ERPNext PR #59425 / commit 4dd9f3bf67:
    // Release work order reservation before previewing / validating stock entry
    // =========================================================================

    [Fact]
    public async Task ValidateAvailability_ExcludesVouchersOwnReservation()
    {
        var workOrderId = Guid.NewGuid();
        var sre = new StockReservationEntry(
            Guid.NewGuid(), _companyId, _rmItemId, _warehouseId,
            "WorkOrder", workOrderId, 20m);
        sre.Submit();

        var bin = new Bin(Guid.NewGuid(), _rmItemId, _warehouseId)
        {
            ActualQty = 20m,
            ReservedQty = 20m
        };

        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockReservationEntry> { sre }.AsQueryable()));
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin }.AsQueryable()));
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();

        var manager = new StockReservationManager(sreRepo, binRepo, sleRepo);

        // Without ignoring own voucher, available stock is 20 - 20 = 0 -> throws InsufficientStock
        await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            manager.ValidateAvailabilityAsync(_rmItemId, _warehouseId, 20m));

        // When ignoring own voucher (PR #59425): available is 20 - 0 = 20 -> succeeds!
        await manager.ValidateAvailabilityAsync(
            _rmItemId, _warehouseId, 20m,
            ignoreVoucherType: "WorkOrder", ignoreVoucherId: workOrderId);
    }

    // =========================================================================
    // ERPNext PR #59426 / commit ec775eb782:
    // Default closing amount for payment modes not in POS opening entry
    // =========================================================================

    [Fact]
    public async Task PosClosing_DefaultClosingAmount_DefaultsToExpectedWhenUnspecified()
    {
        var closingRepo = Substitute.For<IRepository<PosClosingEntry, Guid>>();
        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var consolidationService = Substitute.For<PosConsolidationService>(invoiceRepo);
        var numberGen = Substitute.For<MyERP.Core.DomainServices.IDocumentNumberGenerator>();

        var appService = new PosClosingAppService(closingRepo, consolidationService, invoiceRepo, numberGen);

        var lazyProvider = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        var guidGen = Substitute.For<Volo.Abp.Guids.IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());
        lazyProvider.LazyGetService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetRequiredService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);
        lazyProvider.LazyGetRequiredService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);
        appService.LazyServiceProvider = lazyProvider;

        PosClosingEntry? captured = null;
        await closingRepo.InsertAsync(Arg.Do<PosClosingEntry>(x => captured = x), autoSave: true);

        var modeId = Guid.NewGuid();
        var input = new CreatePosClosingDto
        {
            CompanyId = _companyId,
            PosProfileId = Guid.NewGuid(),
            PosOpeningEntryId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Invoices = new List<CreatePosClosingInvoiceDto>(),
            Payments = new List<CreatePosClosingPaymentDto>
            {
                // Cashier did not enter closing amount (or it was defaulted to 0 on new mode): Expected 150, Closing 0
                new CreatePosClosingPaymentDto
                {
                    ModeOfPaymentId = modeId,
                    ModeName = "Credit Card",
                    ExpectedAmount = 150m,
                    ClosingAmount = 0m
                }
            }
        };

        await appService.CreateAsync(input);

        captured.ShouldNotBeNull();
        captured.Payments.Count.ShouldBe(1);
        captured.Payments[0].ExpectedAmount.ShouldBe(150m);
        captured.Payments[0].ClosingAmount.ShouldBe(150m);
        captured.Payments[0].Difference.ShouldBe(0m);
    }

    // =========================================================================
    // ERPNext commits ff5f2d6667, 95bf4771c3, 62b2b6871c, 2c673e4f7e, b9fa398013:
    // BOM valuation rate fallback, price list validity, zero conversion factor, and WO full-precision rate
    // =========================================================================

    [Fact]
    public async Task BomCostPropagationService_ResolveBomItemValuationRate_NoBinOnSourceWarehouse_FallsBackToOwnCompanySle()
    {
        var otherCompanyId = Guid.NewGuid();
        var emptySourceWarehouseId = Guid.NewGuid();

        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();

        // Source warehouse has no Bin at all (commit ff5f2d6667)
        binRepo.GetQueryableAsync().Returns(Task.FromResult(Enumerable.Empty<Bin>().AsQueryable()));
        var wh = new Warehouse(emptySourceWarehouseId, _companyId, "Source WH");
        whRepo.FindAsync(emptySourceWarehouseId).Returns(Task.FromResult<Warehouse?>(wh));

        // SLE has a newer entry in another company (rate 999) and an entry in the BOM's own company (rate 42.5) (commit 95bf4771c3)
        var ownCompanySle = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _rmItemId, Guid.NewGuid(),
            new DateTime(2026, 11, 10), 5m, 42.5m, 5m, 212.5m);
        var otherCompanySle = new StockLedgerEntry(
            Guid.NewGuid(), otherCompanyId, _rmItemId, Guid.NewGuid(),
            new DateTime(2026, 11, 15), 5m, 999m, 5m, 4995m);

        sleRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { ownCompanySle, otherCompanySle }.AsQueryable()));

        var service = new BomCostPropagationService(bomRepo, binRepo, whRepo, sleRepo, itemRepo);

        var rate = await service.ResolveBomItemValuationRateAsync(_rmItemId, _companyId, emptySourceWarehouseId);
        rate.ShouldBe(42.5m);
    }

    [Fact]
    public async Task BomCostPropagationService_ResolveBomItemPriceListRate_SkipsFutureAndExpiredItemPrices()
    {
        var priceListId = Guid.NewGuid();
        var today = new DateTime(2026, 11, 17);

        var expiredPrice = new ItemPrice(Guid.NewGuid(), _rmItemId, priceListId, 10m, "Unit", "MYR")
        {
            ValidFrom = today.AddDays(-30),
            ValidUpto = today.AddDays(-1)
        };
        var validPrice = new ItemPrice(Guid.NewGuid(), _rmItemId, priceListId, 25m, "Unit", "MYR")
        {
            ValidFrom = today.AddDays(-5),
            ValidUpto = today.AddDays(10)
        };
        var futurePrice = new ItemPrice(Guid.NewGuid(), _rmItemId, priceListId, 90m, "Unit", "MYR")
        {
            ValidFrom = today.AddDays(1),
            ValidUpto = today.AddDays(30)
        };

        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var priceRepo = Substitute.For<IRepository<ItemPrice, Guid>>();
        priceRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { expiredPrice, validPrice, futurePrice }.AsQueryable()));

        var service = new BomCostPropagationService(bomRepo, itemPriceRepository: priceRepo);

        var rate = await service.ResolveBomItemPriceListRateAsync(_rmItemId, priceListId, "Box", conversionFactor: 4m, asOfDate: today);
        rate.ShouldBe(100m); // 25 * 4 conversion factor, skipping expired (10) and future (90)
    }

    [Fact]
    public async Task WorkOrderManager_CalculateMaterialRequirements_ZeroConversionFactorAndSubCentRate_PreservesFullPrecision()
    {
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();

        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials(bomId, _companyId, "BOM-PREC", _fgItemId) { Quantity = 1m };
        // Legacy row with ConversionFactor = 0 and sub-cent stock UOM rate (0.00004)
        var bomItem = new BomItem(Guid.NewGuid(), bomId, _rmItemId, "Micro Resistor", quantity: 1m, rate: 0.00004m, uom: "Unit", conversionFactor: 0m);
        bom.Items.Add(bomItem);

        // BomItem.StockQty and StockUomRate treat ConversionFactor = 0 as 1 (commit 2c673e4f7e)
        bomItem.StockQty.ShouldBe(1m);
        bomItem.StockUomRate.ShouldBe(0.00004m);

        bomRepo.GetAsync(bomId).Returns(Task.FromResult(bom));
        var rm = new Item(_rmItemId, _companyId, "RES", "Micro Resistor", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { rm }.AsQueryable()));

        // Produce 100,000 units: rate must remain 0.00004 (not rounded to 0 at 4dp per commit b9fa398013)
        var reqs = await manager.CalculateMaterialRequirementsAsync(bomId, produceQty: 100000m);
        reqs.Length.ShouldBe(1);
        reqs[0].RequiredQty.ShouldBe(100000m);
        reqs[0].Rate.ShouldBe(0.00004m);
        (reqs[0].RequiredQty * reqs[0].Rate).ShouldBe(4m);
    }
}
