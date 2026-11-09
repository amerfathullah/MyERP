using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 8:
/// - 38b9280852 &amp; 26bf8efb9e: Master Production Schedule extends ToDate to cover child row delivery dates beyond Sales Forecast range.
/// - 7754616e80, b314476739, 11ea4f022a &amp; d172ca0198: UOM conversion validation on MPS items and whole-pack Purchase Order quantity calculation in MRP.
/// - 2f2d383015, 889b5fa479 &amp; b96fbf92df: Separate accepted vs rejected bundle requirement checks on return rows.
/// - 8cf7652623: Prevent double-counting batch reservations when a StockReservationEntry carries both BatchId and SerialAndBatchBundleId.
/// - 2548ec4047 &amp; 8c517abc81: Financial statement / cash flow BuildPeriods month-end alignment, fiscal-year split in accumulated mode, and duplicate period key disambiguation.
/// </summary>
public class UpstreamBatch8ParityTests
{
    [Fact]
    public void MasterProductionSchedule_ExtendToDate_ExpandsScheduleToDateBeyondForecastToMaxRowDeliveryDate_38b9280852_26bf8efb9e()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var mps = new MasterProductionSchedule(
            Guid.NewGuid(),
            companyId,
            "MPS-2026-0001",
            postingDate: new DateTime(2026, 4, 1),
            fromDate: new DateTime(2026, 4, 1));

        mps.ExtendToDate(new DateTime(2026, 4, 30));
        mps.ToDate.ShouldBe(new DateTime(2026, 4, 30));

        mps.SetItems(new[]
        {
            new MasterProductionScheduleItem(
                Guid.NewGuid(),
                mps.Id,
                itemId,
                "Finished Widget",
                deliveryDate: new DateTime(2026, 5, 15),
                plannedQty: 10m,
                cumulativeLeadTimeDays: 0)
            {
                Uom = "Nos"
            }
        });

        mps.ToDate.ShouldBe(new DateTime(2026, 5, 15));
    }

    [Fact]
    public async Task UomConversionService_GetRequiredConversionFactorAndWholePackPurchaseOrderQty_7754616e80_b314476739_11ea4f022a_d172ca0198()
    {
        var itemId = Guid.NewGuid();
        var conversionRepo = Substitute.For<IRepository<UomConversion, Guid>>();
        var conversions = new List<UomConversion>
        {
            new(Guid.NewGuid(), "Box", "Nos", 10m, itemId)
        };
        conversionRepo.GetQueryableAsync().Returns(Task.FromResult(conversions.AsQueryable()));

        var service = new UomConversionService(conversionRepo);

        // 1) Defined UOM conversion returns factor 10
        var boxFactor = await service.GetRequiredConversionFactorAsync(itemId, "Box", "Nos");
        boxFactor.ShouldBe(10m);

        // 2) Undefined UOM conversion throws BusinessException (7754616e80 / b314476739)
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.GetRequiredConversionFactorAsync(itemId, "Pallet", "Nos"));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.UomNotConfiguredForItem);

        // 3) Whole-pack PO quantity calculation (11ea4f022a / d172ca0198):
        //    11 Nos required with Box = 10 Nos:
        //    - When Box is a whole-number UOM: ceiling(11/10) = 2 Boxes
        //    - When Box allows fractions: 1.1 Boxes
        var wholePackQty = UomConversionService.CalculateWholePackPurchaseOrderQty(
            requiredStockQty: 11m,
            minOrderQty: 0m,
            conversionFactor: 10m,
            mustBeWholeNumber: true);
        wholePackQty.ShouldBe(2m);

        var fractionalPackQty = UomConversionService.CalculateWholePackPurchaseOrderQty(
            requiredStockQty: 11m,
            minOrderQty: 0m,
            conversionFactor: 10m,
            mustBeWholeNumber: false);
        fractionalPackQty.ShouldBe(1.1m);
    }

    [Fact]
    public void SerialBatchBundleValidationService_NeedsAcceptedAndRejectedReturnBundles_2f2d383015_889b5fa479_b96fbf92df()
    {
        var rejectedWhId = Guid.NewGuid();

        // Rejected-only return row (Quantity = 0, RejectedQty = -3): only rejected bundle needed
        SerialBatchBundleValidationService.NeedsAcceptedReturnBundle(0m, returnQtyFromRejectedWarehouse: false).ShouldBeFalse();
        SerialBatchBundleValidationService.NeedsRejectedReturnBundle(-3m, rejectedWhId, returnQtyFromRejectedWarehouse: false).ShouldBeTrue();

        // Accepted-only return row (Quantity = -5, RejectedQty = 0): only accepted bundle needed
        SerialBatchBundleValidationService.NeedsAcceptedReturnBundle(-5m, returnQtyFromRejectedWarehouse: false).ShouldBeTrue();
        SerialBatchBundleValidationService.NeedsRejectedReturnBundle(0m, rejectedWhId, returnQtyFromRejectedWarehouse: false).ShouldBeFalse();

        // When ReturnQtyFromRejectedWarehouse is true: accepted bundle is skipped, rejected bundle checks existingAcceptedBundleId
        SerialBatchBundleValidationService.NeedsAcceptedReturnBundle(-5m, returnQtyFromRejectedWarehouse: true).ShouldBeFalse();
        SerialBatchBundleValidationService.NeedsRejectedReturnBundle(-5m, rejectedWhId, returnQtyFromRejectedWarehouse: true).ShouldBeTrue();
    }

    [Fact]
    public async Task BatchAppService_GetStockBalanceAndAvailableBatches_DoNotDoubleCountSreWithBundleAndBatchId_8cf7652623()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();

        var batchRepo = Substitute.For<IRepository<Batch, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        var bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
        var stockEntryAppService = Substitute.For<IStockEntryAppService>();
        var settingProvider = Substitute.For<ISettingProvider>();
        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();

        var batch = new Batch(batchId, itemId, "BATCH-SRE-01");
        var wh = new Warehouse(warehouseId, companyId, "Main Store");
        var item = new Item(itemId, companyId, "BATCH-ITEM-01", "Batch Item", ItemType.Goods) { HasBatchNo = true };

        batchRepo.GetAsync(batchId).Returns(Task.FromResult(batch));
        batchRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Batch> { batch }.AsQueryable()));
        whRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { wh }.AsQueryable()));
        itemRepo.FindAsync(itemId).Returns(Task.FromResult<Item?>(item));
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var sles = new List<StockLedgerEntry>
        {
            new(Guid.NewGuid(), companyId, itemId, warehouseId, DateTime.UtcNow.Date, 10m, 50m, 10m, 500m)
            {
                BatchId = batchId
            }
        };
        sleRepo.GetQueryableAsync().Returns(Task.FromResult(sles.AsQueryable()));

        var bundle = new SerialAndBatchBundle(
            bundleId, companyId, itemId, warehouseId,
            BundleTransactionType.Outward, "StockReservationEntry", Guid.NewGuid(), DateTime.UtcNow.Date);
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 2m, 50m, batchId: batchId)
        {
            WarehouseId = warehouseId
        });
        bundle.Submit();
        bundleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SerialAndBatchBundle> { bundle }.AsQueryable()));

        // SRE carries BOTH BatchId and SerialAndBatchBundleId
        var sre = new StockReservationEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            "SalesOrder", Guid.NewGuid(), 2m, 10m)
        {
            BatchId = batchId,
            SerialAndBatchBundleId = bundleId
        };
        sre.Submit();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockReservationEntry> { sre }.AsQueryable()));

        lazyProvider.LazyGetRequiredService<IRepository<Item, Guid>>().Returns(itemRepo);
        lazyProvider.LazyGetService<IRepository<Item, Guid>>().Returns(itemRepo);
        lazyProvider.LazyGetService<IRepository<StockReservationEntry, Guid>>().Returns(sreRepo);
        lazyProvider.LazyGetService<IRepository<SerialAndBatchBundle, Guid>>().Returns(bundleRepo);
        lazyProvider.LazyGetRequiredService<ISettingProvider>().Returns(settingProvider);

        var service = new BatchAppService(batchRepo, sleRepo, whRepo, stockEntryAppService)
        {
            LazyServiceProvider = lazyProvider
        };

        var available = await service.GetAvailableBatchesAsync(new GetAvailableBatchesDto
        {
            CompanyId = companyId,
            ItemId = itemId,
            WarehouseId = warehouseId,
        });

        available.Count.ShouldBe(1);
        available[0].BalanceQuantity.ShouldBe(10m);
        available[0].ReservedQuantity.ShouldBe(2m);
        available[0].AvailableQuantity.ShouldBe(8m);

        var stockBalance = await service.GetStockBalanceAsync(batchId);
        stockBalance.TotalQuantity.ShouldBe(10m);
        stockBalance.TotalReservedQuantity.ShouldBe(2m);
        stockBalance.TotalAvailableQuantity.ShouldBe(8m);
    }

    [Fact]
    public void CashFlowStatementAppService_BuildPeriods_MonthlyAlignsToMonthEnd_QuarterlyStartsFromDate_AccumulatedSplitsFiscalYear_DisambiguatesDuplicateKeys_2548ec4047_8c517abc81()
    {
        var companyId = Guid.NewGuid();
        var fy2526 = new FiscalYear(Guid.NewGuid(), companyId, "FY 25-26", new DateTime(2025, 7, 1), new DateTime(2026, 6, 30));
        var fy2627 = new FiscalYear(Guid.NewGuid(), companyId, "FY 26-27", new DateTime(2026, 7, 1), new DateTime(2027, 6, 30));
        var fiscalYears = new List<FiscalYear> { fy2526, fy2627 };

        // 1) Monthly starting mid-month aligns first bucket to month-end (2548ec4047)
        var monthly = CashFlowStatementAppService.BuildPeriods(
            new DateTime(2026, 7, 15),
            new DateTime(2026, 8, 31),
            "Monthly",
            fiscalYears,
            accumulatedValues: false);

        monthly.Count.ShouldBe(2);
        monthly[0].FromDate.ShouldBe(new DateTime(2026, 7, 15));
        monthly[0].ToDate.ShouldBe(new DateTime(2026, 7, 31));
        monthly[1].FromDate.ShouldBe(new DateTime(2026, 8, 1));
        monthly[1].ToDate.ShouldBe(new DateTime(2026, 8, 31));

        // 2) Quarterly starting 2026-04-15 to 2026-07-31 in non-accumulated mode:
        //    - First period: 2026-04-15 .. 2026-07-14 (key: "jul_2026")
        //    - Second period: 2026-07-15 .. 2026-07-31 (key disambiguated: "jul_2026_31" per 8c517abc81)
        var quarterlyNonAccumulated = CashFlowStatementAppService.BuildPeriods(
            new DateTime(2026, 4, 15),
            new DateTime(2026, 7, 31),
            "Quarterly",
            fiscalYears,
            accumulatedValues: false);

        quarterlyNonAccumulated.Count.ShouldBe(2);
        quarterlyNonAccumulated[0].FromDate.ShouldBe(new DateTime(2026, 4, 15));
        quarterlyNonAccumulated[0].ToDate.ShouldBe(new DateTime(2026, 7, 14));
        quarterlyNonAccumulated[0].Key.ShouldBe("jul_2026");
        quarterlyNonAccumulated[1].FromDate.ShouldBe(new DateTime(2026, 7, 15));
        quarterlyNonAccumulated[1].ToDate.ShouldBe(new DateTime(2026, 7, 31));
        quarterlyNonAccumulated[1].Key.ShouldBe("jul_2026_31");

        // 3) Quarterly across fiscal year boundary (2026-04-15 .. 2026-07-31) in accumulated mode:
        //    Splits at FY end 2026-06-30 so accumulated periods do not cross fiscal years (2548ec4047)
        var quarterlyAccumulated = CashFlowStatementAppService.BuildPeriods(
            new DateTime(2026, 4, 15),
            new DateTime(2026, 7, 31),
            "Quarterly",
            fiscalYears,
            accumulatedValues: true);

        quarterlyAccumulated.Count.ShouldBe(2);
        quarterlyAccumulated[0].FromDate.ShouldBe(new DateTime(2026, 4, 15));
        quarterlyAccumulated[0].ToDate.ShouldBe(new DateTime(2026, 6, 30));
        quarterlyAccumulated[0].EffectiveStartDate.ShouldBe(new DateTime(2026, 4, 15));
        quarterlyAccumulated[1].FromDate.ShouldBe(new DateTime(2026, 7, 1));
        quarterlyAccumulated[1].ToDate.ShouldBe(new DateTime(2026, 7, 31));
        quarterlyAccumulated[1].EffectiveStartDate.ShouldBe(new DateTime(2026, 7, 1));
    }
}
