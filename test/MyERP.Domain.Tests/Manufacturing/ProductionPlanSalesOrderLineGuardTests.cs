using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class ProductionPlanSalesOrderLineGuardTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _bomId = Guid.NewGuid();

    private ProductionPlanAppService CreateAppService(
        IRepository<ProductionPlan, Guid> planRepo,
        IRepository<SalesOrder, Guid> soRepo,
        IRepository<ManufacturingSettings, Guid>? settingsRepo = null,
        IRepository<Item, Guid>? itemRepo = null,
        IRepository<Customer, Guid>? custRepo = null,
        IRepository<BillOfMaterials, Guid>? bomRepo = null,
        IRepository<WorkOrder, Guid>? woRepo = null,
        IRepository<Warehouse, Guid>? whRepo = null,
        IRepository<Bin, Guid>? binRepo = null)
    {
        bomRepo ??= Substitute.For<IRepository<BillOfMaterials, Guid>>();
        woRepo ??= Substitute.For<IRepository<WorkOrder, Guid>>();
        var mrRepo = Substitute.For<IRepository<Purchasing.Entities.MaterialRequest, Guid>>();
        var numberGen = Substitute.For<IDocumentNumberGenerator>();
        var bomValService = new BomValidationService(bomRepo, Substitute.For<IRepository<Item, Guid>>());

        settingsRepo ??= Substitute.For<IRepository<ManufacturingSettings, Guid>>();
        itemRepo ??= Substitute.For<IRepository<Item, Guid>>();
        custRepo ??= Substitute.For<IRepository<Customer, Guid>>();
        whRepo ??= Substitute.For<IRepository<Warehouse, Guid>>();
        binRepo ??= Substitute.For<IRepository<Bin, Guid>>();

        var activityLogRepo = Substitute.For<IRepository<DocumentActivityLog, Guid>>();

        var appService = new ProductionPlanAppService(planRepo, bomRepo, woRepo, mrRepo, numberGen, bomValService);
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<SalesOrder, Guid>>().Returns(soRepo);
        lazySp.LazyGetRequiredService<IRepository<ManufacturingSettings, Guid>>().Returns(settingsRepo);
        lazySp.LazyGetRequiredService<IRepository<Item, Guid>>().Returns(itemRepo);
        lazySp.LazyGetRequiredService<IRepository<Customer, Guid>>().Returns(custRepo);
        lazySp.LazyGetRequiredService<IRepository<DocumentActivityLog, Guid>>().Returns(activityLogRepo);
        lazySp.LazyGetRequiredService<IRepository<Warehouse, Guid>>().Returns(whRepo);
        lazySp.LazyGetRequiredService<IRepository<Bin, Guid>>().Returns(binRepo);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(appService, lazySp);

        return appService;
    }

    [Fact]
    public async Task SubmitAsync_ExceedsSalesOrderLineQty_ThrowsSalesOrderQtyExceeded()
    {
        // Arrange (ERPNext PR #60271 / commit f3ba7ca638)
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();

        var soId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        var soItem = new SalesOrderItem(Guid.NewGuid(), soId, _itemId, "Widget", quantity: 10m, unitPrice: 100m, taxAmount: 0m, uom: "Unit")
        {
            ConversionFactor = 1m
        };
        so.Items.ShouldNotBeNull();
        typeof(SalesOrder).GetField("_items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(so, new List<SalesOrderItem> { soItem });

        var planId = Guid.NewGuid();
        var plan = new ProductionPlan(planId, _companyId, "PP-001", DateTime.UtcNow);
        var ppItem = new ProductionPlanItem(Guid.NewGuid(), planId, _itemId, "Widget", _bomId, plannedQty: 15m)
        {
            SalesOrderId = soId,
            SalesOrderItemId = soItem.Id
        };
        plan.AddPlannedItem(ppItem);

        planRepo.GetAsync(planId, includeDetails: true).Returns(plan);
        planRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ProductionPlan> { plan }.AsQueryable()));
        soRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        var invItem = new Item(_itemId, _companyId, "WIDGET", "Widget", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { invItem }.AsQueryable()));
        settingsRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ManufacturingSettings>().AsQueryable())); // 0% allowance

        var appService = CreateAppService(planRepo, soRepo, settingsRepo, itemRepo);

        // Act & Assert
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await appService.SubmitAsync(planId);
        });

        ex.Code.ShouldBe(MyERPDomainErrorCodes.SalesOrderQtyExceeded);
        ex.Data["plannedQty"].ShouldBe(15m);
        ex.Data["unplannedQty"].ShouldBe(10m);
        ex.Data["itemCode"].ShouldBe("WIDGET");
        ex.Data["salesOrderNumber"].ShouldBe("SO-001");
    }

    [Fact]
    public async Task SubmitAsync_WithinOverproductionAllowance_Succeeds()
    {
        // Arrange: SO qty is 10, allowance is 20%, so allowed is 12. Planned is 12 -> succeeds.
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();

        var soId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, Guid.NewGuid(), "SO-002", DateTime.UtcNow);
        var soItem = new SalesOrderItem(Guid.NewGuid(), soId, _itemId, "Widget", quantity: 10m, unitPrice: 100m, taxAmount: 0m, uom: "Unit")
        {
            ConversionFactor = 1m
        };
        typeof(SalesOrder).GetField("_items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(so, new List<SalesOrderItem> { soItem });

        var planId = Guid.NewGuid();
        var plan = new ProductionPlan(planId, _companyId, "PP-002", DateTime.UtcNow);
        var ppItem = new ProductionPlanItem(Guid.NewGuid(), planId, _itemId, "Widget", _bomId, plannedQty: 12m)
        {
            SalesOrderId = soId,
            SalesOrderItemId = soItem.Id
        };
        plan.AddPlannedItem(ppItem);

        planRepo.GetAsync(planId, includeDetails: true).Returns(plan);
        planRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ProductionPlan> { plan }.AsQueryable()));
        soRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        var invItem = new Item(_itemId, _companyId, "WIDGET", "Widget", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { invItem }.AsQueryable()));

        var settings = new ManufacturingSettings(Guid.NewGuid(), _companyId)
        {
            OverproductionPercentageForSalesOrder = 20m
        };
        settingsRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ManufacturingSettings> { settings }.AsQueryable()));

        var appService = CreateAppService(planRepo, soRepo, settingsRepo, itemRepo);

        // Act & Assert
        await appService.SubmitAsync(planId);
        plan.Status.ShouldBe(ProductionPlanStatus.Submitted);
    }

    [Fact]
    public async Task SubmitAsync_DeductsAlreadyPlannedFromOtherSubmittedPlans()
    {
        // Arrange: SO qty is 10. Another submitted plan already planned 6. Remaining unplanned is 4.
        // Planning 5 should throw SalesOrderQtyExceeded.
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();

        var soId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, Guid.NewGuid(), "SO-003", DateTime.UtcNow);
        var soItem = new SalesOrderItem(Guid.NewGuid(), soId, _itemId, "Widget", quantity: 10m, unitPrice: 100m, taxAmount: 0m, uom: "Unit")
        {
            ConversionFactor = 1m
        };
        typeof(SalesOrder).GetField("_items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(so, new List<SalesOrderItem> { soItem });

        // Existing submitted plan planning 6
        var otherPlan = new ProductionPlan(Guid.NewGuid(), _companyId, "PP-OTHER", DateTime.UtcNow);
        var otherItem = new ProductionPlanItem(Guid.NewGuid(), otherPlan.Id, _itemId, "Widget", _bomId, plannedQty: 6m)
        {
            SalesOrderId = soId,
            SalesOrderItemId = soItem.Id
        };
        otherPlan.AddPlannedItem(otherItem);
        otherPlan.Submit();

        // Current plan planning 5 (total 11 > 10)
        var planId = Guid.NewGuid();
        var plan = new ProductionPlan(planId, _companyId, "PP-003", DateTime.UtcNow);
        var ppItem = new ProductionPlanItem(Guid.NewGuid(), planId, _itemId, "Widget", _bomId, plannedQty: 5m)
        {
            SalesOrderId = soId,
            SalesOrderItemId = soItem.Id
        };
        plan.AddPlannedItem(ppItem);

        planRepo.GetAsync(planId, includeDetails: true).Returns(plan);
        planRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ProductionPlan> { otherPlan, plan }.AsQueryable()));
        soRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        var invItem = new Item(_itemId, _companyId, "WIDGET", "Widget", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { invItem }.AsQueryable()));
        settingsRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ManufacturingSettings>().AsQueryable()));

        var appService = CreateAppService(planRepo, soRepo, settingsRepo, itemRepo);

        // Act & Assert
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await appService.SubmitAsync(planId);
        });

        ex.Code.ShouldBe(MyERPDomainErrorCodes.SalesOrderQtyExceeded);
        ex.Data["plannedQty"].ShouldBe(5m);
        ex.Data["unplannedQty"].ShouldBe(4m); // 10 - 6 = 4 left
    }

    [Fact]
    public async Task SubmitAsync_IgnoresProductBundleComponents()
    {
        // Product bundle component items are excluded from SO line guard per PR #60271
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();

        var planId = Guid.NewGuid();
        var plan = new ProductionPlan(planId, _companyId, "PP-BUNDLE", DateTime.UtcNow);
        var bundleCompItem = new ProductionPlanItem(Guid.NewGuid(), planId, _itemId, "Sub Part", _bomId, plannedQty: 50m)
        {
            SalesOrderId = Guid.NewGuid(),
            SalesOrderItemId = Guid.NewGuid(),
            IsProductBundleItem = true
        };
        plan.AddPlannedItem(bundleCompItem);

        planRepo.GetAsync(planId, includeDetails: true).Returns(plan);

        var appService = CreateAppService(planRepo, soRepo);

        // Act & Assert: does not throw even though SO does not exist
        await appService.SubmitAsync(planId);
        plan.Status.ShouldBe(ProductionPlanStatus.Submitted);
    }

    [Fact]
    public async Task GetOpenSalesOrderItemsAsync_CalculatesRemainingUnplannedQty()
    {
        // Arrange
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();

        var custId = Guid.NewGuid();
        var cust = new Customer(custId, _companyId, "Acme Corp");

        var soId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, custId, "SO-OPEN-1", DateTime.UtcNow);
        var item1 = new SalesOrderItem(Guid.NewGuid(), soId, _itemId, "Widget A", quantity: 10m, unitPrice: 100m, taxAmount: 0m, uom: "Unit")
        {
            ConversionFactor = 1m
        };
        var item2Id = Guid.NewGuid();
        var item2 = new SalesOrderItem(Guid.NewGuid(), soId, item2Id, "Widget B", quantity: 5m, unitPrice: 50m, taxAmount: 0m, uom: "Unit")
        {
            ConversionFactor = 1m
        };
        typeof(SalesOrder).GetField("_items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(so, new List<SalesOrderItem> { item1, item2 });
        so.Submit();

        // Already planned: 6 units for item1
        var otherPlan = new ProductionPlan(Guid.NewGuid(), _companyId, "PP-EXIST", DateTime.UtcNow);
        var planItem1 = new ProductionPlanItem(Guid.NewGuid(), otherPlan.Id, _itemId, "Widget A", _bomId, plannedQty: 6m)
        {
            SalesOrderId = soId,
            SalesOrderItemId = item1.Id
        };
        otherPlan.AddPlannedItem(planItem1);
        otherPlan.Submit();

        soRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));
        planRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ProductionPlan, object>>>())
            .Returns(Task.FromResult(new List<ProductionPlan> { otherPlan }.AsQueryable()));

        var invItem1 = new Item(_itemId, _companyId, "WIDGET-A", "Widget A", ItemType.Goods);
        var invItem2 = new Item(item2Id, _companyId, "WIDGET-B", "Widget B", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { invItem1, invItem2 }.AsQueryable()));
        custRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { cust }.AsQueryable()));

        var bom = new BillOfMaterials(_bomId, _companyId, "BOM-A", _itemId) { IsActive = true, IsDefault = true };
        bomRepo.GetQueryableAsync().Returns(Task.FromResult(new List<BillOfMaterials> { bom }.AsQueryable()));

        var appService = CreateAppService(planRepo, soRepo, null, itemRepo, custRepo, bomRepo);

        // Act
        var openItems = await appService.GetOpenSalesOrderItemsAsync(_companyId);

        // Assert
        openItems.Count.ShouldBe(2);

        var rowA = openItems.First(r => r.ItemId == _itemId);
        rowA.StockQty.ShouldBe(10m);
        rowA.AlreadyPlannedQty.ShouldBe(6m);
        rowA.UnplannedQty.ShouldBe(4m); // 10 - 6 = 4
        rowA.CustomerName.ShouldBe("Acme Corp");
        rowA.BomId.ShouldBe(_bomId);

        var rowB = openItems.First(r => r.ItemId == item2Id);
        rowB.StockQty.ShouldBe(5m);
        rowB.AlreadyPlannedQty.ShouldBe(0m);
        rowB.UnplannedQty.ShouldBe(5m);
    }

    [Fact]
    public async Task GetOpenSalesOrderItemsAsync_ExistingWorkOrdersAndDeliveries_ReduceUnplannedQty()
    {
        // Arrange: SO line qty 10. Existing WO = 3, Delivered = 2. Max(3, 2) = 3.
        // Already planned from another plan = 0. Remaining unplanned should be 7 (ERPNext PR #55685a08be).
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();

        var custId = Guid.NewGuid();
        var cust = new Customer(custId, _companyId, "Acme Corp");

        var soId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, custId, "SO-WO-1", DateTime.UtcNow);
        var item = new SalesOrderItem(Guid.NewGuid(), soId, _itemId, "Widget A", quantity: 10m, unitPrice: 100m, taxAmount: 0m, uom: "Unit")
        {
            ConversionFactor = 1m,
            DeliveredQty = 2m
        };
        typeof(SalesOrder).GetField("_items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(so, new List<SalesOrderItem> { item });
        so.Submit();

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _itemId, _bomId, 3m)
        {
            SalesOrderId = soId,
            SalesOrderItemId = item.Id
        };

        soRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));
        planRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ProductionPlan, object>>>())
            .Returns(Task.FromResult(new List<ProductionPlan>().AsQueryable()));
        woRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<WorkOrder> { wo }.AsQueryable()));

        var invItem = new Item(_itemId, _companyId, "WIDGET-A", "Widget A", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { invItem }.AsQueryable()));
        custRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { cust }.AsQueryable()));

        var bom = new BillOfMaterials(_bomId, _companyId, "BOM-A", _itemId) { IsActive = true, IsDefault = true };
        bomRepo.GetQueryableAsync().Returns(Task.FromResult(new List<BillOfMaterials> { bom }.AsQueryable()));

        var appService = CreateAppService(planRepo, soRepo, null, itemRepo, custRepo, bomRepo, woRepo);

        // Act
        var openItems = await appService.GetOpenSalesOrderItemsAsync(_companyId);

        // Assert: 10 - max(3, 2) = 7
        openItems.Count.ShouldBe(1);
        openItems[0].StockQty.ShouldBe(10m);
        openItems[0].UnplannedQty.ShouldBe(7m);
    }

    [Fact]
    public async Task SubmitAsync_ExistingWorkOrdersDeductFromAllowance_ThrowsIfExceeded()
    {
        // Arrange: SO line qty 10. Allowance = 0%. Existing WO = 6. Remaining allowed = 4.
        // Planning 5 should throw SalesOrderQtyExceeded.
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();

        var soId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, Guid.NewGuid(), "SO-WO-2", DateTime.UtcNow);
        var soItem = new SalesOrderItem(Guid.NewGuid(), soId, _itemId, "Widget", quantity: 10m, unitPrice: 100m, taxAmount: 0m, uom: "Unit")
        {
            ConversionFactor = 1m
        };
        typeof(SalesOrder).GetField("_items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(so, new List<SalesOrderItem> { soItem });

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-EXIST", _itemId, _bomId, 6m)
        {
            SalesOrderId = soId,
            SalesOrderItemId = soItem.Id
        };

        var planId = Guid.NewGuid();
        var plan = new ProductionPlan(planId, _companyId, "PP-WO-FAIL", DateTime.UtcNow);
        var ppItem = new ProductionPlanItem(Guid.NewGuid(), planId, _itemId, "Widget", _bomId, plannedQty: 5m)
        {
            SalesOrderId = soId,
            SalesOrderItemId = soItem.Id
        };
        plan.AddPlannedItem(ppItem);

        planRepo.GetAsync(planId, includeDetails: true).Returns(plan);
        planRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ProductionPlan> { plan }.AsQueryable()));
        soRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));
        woRepo.GetQueryableAsync().Returns(Task.FromResult(new List<WorkOrder> { wo }.AsQueryable()));

        var invItem = new Item(_itemId, _companyId, "WIDGET", "Widget", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { invItem }.AsQueryable()));

        var appService = CreateAppService(planRepo, soRepo, settingsRepo, itemRepo, null, null, woRepo);

        // Act & Assert: Planning 5 when only 4 allowed throws SalesOrderQtyExceeded
        var ex = await Should.ThrowAsync<BusinessException>(() => appService.SubmitAsync(planId));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.SalesOrderQtyExceeded);
    }

    [Fact]
    public async Task GetPooledBinDetailsAsync_GroupWarehouse_SumsAcrossChildWarehouses()
    {
        // Arrange: Group warehouse with two child warehouses
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();

        var groupWhId = Guid.NewGuid();
        var childWh1Id = Guid.NewGuid();
        var childWh2Id = Guid.NewGuid();

        var groupWh = new Warehouse(groupWhId, _companyId, "All Warehouses") { IsGroup = true };
        var childWh1 = new Warehouse(childWh1Id, _companyId, "Stores 1") { ParentWarehouseId = groupWhId };
        var childWh2 = new Warehouse(childWh2Id, _companyId, "Stores 2") { ParentWarehouseId = groupWhId };

        whRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Warehouse, bool>>>())
            .Returns(Task.FromResult(new List<Warehouse> { groupWh, childWh1, childWh2 }));

        var bin1 = new Bin(Guid.NewGuid(), _itemId, childWh1Id)
        {
            ActualQty = 15m,
            OrderedQty = 5m // ProjectedQty = 15 + 5 = 20
        };
        var bin2 = new Bin(Guid.NewGuid(), _itemId, childWh2Id)
        {
            ActualQty = 25m,
            OrderedQty = 5m // ProjectedQty = 25 + 5 = 30
        };

        binRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Bin> { bin1, bin2 }.AsQueryable()));

        var appService = CreateAppService(planRepo, soRepo, null, null, null, null, null, whRepo, binRepo);

        // Act: Request pooled details for group warehouse
        var result = await appService.GetPooledBinDetailsAsync(_companyId, _itemId, groupWhId);

        // Assert: 15 + 25 = 40 Actual, 20 + 30 = 50 Projected (PR #60718 / commit 4f7f2aabc3)
        result.ActualQty.ShouldBe(40m);
        result.ProjectedQty.ShouldBe(50m);
    }
}
