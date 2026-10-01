using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Settings;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class UpstreamBatch59600To59679Tests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    // =========================================================================
    // PR #59555: Planned Qty must be strictly greater than 0 on Production Plan
    // =========================================================================
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ProductionPlan_Submit_Throws_WhenPlannedQtyZeroOrNegative(decimal plannedQty)
    {
        var plan = new ProductionPlan(Guid.NewGuid(), _companyId, "PP-001", DateTime.UtcNow);
        var item = new ProductionPlanItem(Guid.NewGuid(), plan.Id, _itemId, "FG Item", Guid.NewGuid(), plannedQty);
        plan.AddPlannedItem(item);

        var ex = Should.Throw<BusinessException>(() => plan.Submit());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.AmountMustBePositive);
    }

    [Fact]
    public void ProductionPlan_Submit_Succeeds_WhenPlannedQtyPositive()
    {
        var plan = new ProductionPlan(Guid.NewGuid(), _companyId, "PP-001", DateTime.UtcNow);
        var item = new ProductionPlanItem(Guid.NewGuid(), plan.Id, _itemId, "FG Item", Guid.NewGuid(), 10m);

        plan.AddPlannedItem(item);
        plan.Submit();
        plan.Status.ShouldBe(ProductionPlanStatus.Submitted);
        plan.PlannedItems.Count.ShouldBe(1);
        plan.PlannedItems[0].PlannedQty.ShouldBe(10m);
    }

    // =========================================================================
    // PR #59616: Net work order returns off transferred materials / limit return
    // =========================================================================
    [Fact]
    public void StockEntryManager_ValidateTransferQty_Return_ThrowsWhenExceedingUnconsumed()
    {
        var mgr = new StockEntryManager(
            Substitute.For<IRepository<Warehouse, Guid>>(),
            Substitute.For<IRepository<Item, Guid>>(),
            null!);

        // 10 transferred, 7 consumed -> 3 returnable. Requested 5 -> should throw.
        var ex = Should.Throw<BusinessException>(() =>
            mgr.ValidateTransferQty(
                requiredQty: 10m,
                transferredQty: 10m,
                requestedQty: 5m,
                isReturn: true,
                consumedQty: 7m));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void StockEntryManager_ValidateTransferQty_Return_PassesWithinUnconsumed()
    {
        var mgr = new StockEntryManager(
            Substitute.For<IRepository<Warehouse, Guid>>(),
            Substitute.For<IRepository<Item, Guid>>(),
            null!);

        // 10 transferred, 7 consumed -> 3 returnable. Requested 3 -> should pass.
        Should.NotThrow(() =>
            mgr.ValidateTransferQty(
                requiredQty: 10m,
                transferredQty: 10m,
                requestedQty: 3m,
                isReturn: true,
                consumedQty: 7m));
    }

    // =========================================================================
    // PR #59617 & PR #59616: StockEntryAppService Return submit guards
    // =========================================================================
    [Fact]
    public async Task StockEntryAppService_Submit_Return_Throws_WhenWorkOrderIsInProcess()
    {
        var entryRepo = Substitute.For<IRepository<StockEntry, Guid>>();
        var logRepo = Substitute.For<IRepository<DocumentActivityLog, Guid>>();
        var numberGen = Substitute.For<IDocumentNumberGenerator>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var postingService = Substitute.For<StockPostingService>(
            Substitute.For<IRepository<StockLedgerEntry, Guid>>(),
            Substitute.For<IRepository<Company, Guid>>(),
            itemRepo,
            whRepo,
            Substitute.For<BinService>(Substitute.For<IRepository<Bin, Guid>>()),
            Substitute.For<StockValuationService>(
                Substitute.For<IRepository<StockLedgerEntry, Guid>>(),
                itemRepo,
                Substitute.For<ISettingProvider>()));
        var settingProvider = Substitute.For<ISettingProvider>();

        var appService = new StockEntryAppService(
            entryRepo, logRepo, numberGen, postingService, settingProvider);

        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, _companyId, "WO-001", _itemId, Guid.NewGuid(), 10m);
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), woId, _itemId, "RM 1", 10m)
        {
            TransferredQuantity = 10m,
            ConsumedQuantity = 2m
        });
        wo.Submit();
        wo.Start(); // InProcess status

        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        woRepo.GetAsync(woId, includeDetails: true).Returns(wo);
        woRepo.FindAsync(woId).Returns(wo);

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<WorkOrder, Guid>>().Returns(woRepo);
        lazySp.LazyGetRequiredService<IRepository<MyERP.Manufacturing.Entities.JobCard, Guid>>().Returns(Substitute.For<IRepository<MyERP.Manufacturing.Entities.JobCard, Guid>>());
        lazySp.LazyGetRequiredService<StockReservationManager>().Returns(Substitute.For<StockReservationManager>());
        lazySp.LazyGetRequiredService<StockEntryManager>().Returns(Substitute.For<StockEntryManager>());
        lazySp.LazyGetRequiredService<IRepository<DocumentActivityLog, Guid>>().Returns(Substitute.For<IRepository<DocumentActivityLog, Guid>>());

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(appService, lazySp);

        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransferForManufacture, DateTime.UtcNow)
        {
            WorkOrderId = woId,
            IsReturn = true
        };
        entry.AddItem(_itemId, 3m, _warehouseId, Guid.NewGuid(), 100m);
        entryRepo.GetAsync(entry.Id, includeDetails: true).Returns(entry);

        var ex = await Should.ThrowAsync<BusinessException>(() => appService.SubmitAsync(entry.Id));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public async Task StockEntryAppService_Submit_Return_Throws_WhenReturnExceedsUnconsumed()
    {
        var entryRepo = Substitute.For<IRepository<StockEntry, Guid>>();
        var logRepo = Substitute.For<IRepository<DocumentActivityLog, Guid>>();
        var numberGen = Substitute.For<IDocumentNumberGenerator>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var postingService = Substitute.For<StockPostingService>(
            Substitute.For<IRepository<StockLedgerEntry, Guid>>(),
            Substitute.For<IRepository<Company, Guid>>(),
            itemRepo,
            whRepo,
            Substitute.For<BinService>(Substitute.For<IRepository<Bin, Guid>>()),
            Substitute.For<StockValuationService>(
                Substitute.For<IRepository<StockLedgerEntry, Guid>>(),
                itemRepo,
                Substitute.For<ISettingProvider>()));
        var settingProvider = Substitute.For<ISettingProvider>();

        var appService = new StockEntryAppService(
            entryRepo, logRepo, numberGen, postingService, settingProvider);

        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, _companyId, "WO-001", _itemId, Guid.NewGuid(), 10m);
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), woId, _itemId, "RM 1", 10m)
        {
            TransferredQuantity = 10m,
            ConsumedQuantity = 8m // only 2 returnable!
        });
        wo.Submit();
        wo.Start();
        wo.Close(); // Closed status

        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        woRepo.GetAsync(woId, includeDetails: true).Returns(wo);
        woRepo.FindAsync(woId).Returns(wo);

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<WorkOrder, Guid>>().Returns(woRepo);
        lazySp.LazyGetRequiredService<IRepository<MyERP.Manufacturing.Entities.JobCard, Guid>>().Returns(Substitute.For<IRepository<MyERP.Manufacturing.Entities.JobCard, Guid>>());
        lazySp.LazyGetRequiredService<StockReservationManager>().Returns(Substitute.For<StockReservationManager>());
        lazySp.LazyGetRequiredService<StockEntryManager>().Returns(Substitute.For<StockEntryManager>());
        lazySp.LazyGetRequiredService<IRepository<DocumentActivityLog, Guid>>().Returns(Substitute.For<IRepository<DocumentActivityLog, Guid>>());

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(appService, lazySp);

        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransferForManufacture, DateTime.UtcNow)
        {
            WorkOrderId = woId,
            IsReturn = true
        };
        entry.AddItem(_itemId, 5m, _warehouseId, Guid.NewGuid(), 100m); // Attempt 5 > 2
        entryRepo.GetAsync(entry.Id, includeDetails: true).Returns(entry);

        var ex = await Should.ThrowAsync<BusinessException>(() => appService.SubmitAsync(entry.Id));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    // =========================================================================
    // PR #59647: Filter reservation warehouses by company & non-group
    // =========================================================================
    [Fact]
    public async Task StockReservationAppService_CreateAsync_Throws_WhenWarehouseCompanyMismatch()
    {
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(MyERPSettings.Stock.EnableStockReservation).Returns("true");

        var appService = new StockReservationAppService(sreRepo, settingProvider);

        var otherCompanyId = Guid.NewGuid();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var warehouse = new Warehouse(_warehouseId, otherCompanyId, "Cross Company Wh");
        whRepo.GetAsync(_warehouseId).Returns(warehouse);

        var itemValidation = Substitute.For<ItemTransactionValidationService>(
            Substitute.For<IRepository<Item, Guid>>());

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<Warehouse, Guid>>().Returns(whRepo);
        lazySp.LazyGetRequiredService<ItemTransactionValidationService>().Returns(itemValidation);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(appService, lazySp);

        var input = new CreateStockReservationDto
        {
            CompanyId = _companyId,
            ItemId = _itemId,
            WarehouseId = _warehouseId,
            ReservedQty = 10m,
            VoucherType = "SalesOrder",
            VoucherId = Guid.NewGuid()
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => appService.CreateAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.CompanyMismatch);
    }

    [Fact]
    public async Task StockReservationAppService_CreateAsync_Throws_WhenWarehouseIsGroup()
    {
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(MyERPSettings.Stock.EnableStockReservation).Returns("true");

        var appService = new StockReservationAppService(sreRepo, settingProvider);

        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var warehouse = new Warehouse(_warehouseId, _companyId, "Group Wh") { IsGroup = true };
        whRepo.GetAsync(_warehouseId).Returns(warehouse);

        var itemValidation = Substitute.For<ItemTransactionValidationService>(
            Substitute.For<IRepository<Item, Guid>>());

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<Warehouse, Guid>>().Returns(whRepo);
        lazySp.LazyGetRequiredService<ItemTransactionValidationService>().Returns(itemValidation);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(appService, lazySp);

        var input = new CreateStockReservationDto
        {
            CompanyId = _companyId,
            ItemId = _itemId,
            WarehouseId = _warehouseId,
            ReservedQty = 10m,
            VoucherType = "SalesOrder",
            VoucherId = Guid.NewGuid()
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => appService.CreateAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock);
    }
}
