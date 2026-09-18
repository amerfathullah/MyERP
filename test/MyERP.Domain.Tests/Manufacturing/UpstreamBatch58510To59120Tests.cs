using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class UpstreamBatch58510To59120Tests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    // =========================================================================
    // PR #58923: Item Group default inventory account must be leaf Asset Stock
    // =========================================================================

    [Fact]
    public async Task ItemGroupAppService_CreateAsync_Throws_WhenDefaultInventoryAccountIsNotAsset()
    {
        var itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
        var accRepo = Substitute.For<IRepository<Account, Guid>>();

        var accId = Guid.NewGuid();
        // Expense account instead of Asset
        var expenseAcc = new Account(accId, _companyId, "5100", "Cost of Goods Sold", AccountType.Expense)
        {
            IsGroup = false
        };
        accRepo.FindAsync(accId).Returns(expenseAcc);

        var appService = new ItemGroupAppService(itemGroupRepo);
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<Account, Guid>>().Returns(accRepo);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(appService, lazySp);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await appService.CreateAsync(new CreateItemGroupDto
            {
                Name = "Raw Materials",
                DefaultInventoryAccountId = accId
            });
        });

        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidAccountType);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("must be an Asset Stock account");
    }

    [Fact]
    public async Task ItemGroupAppService_CreateAsync_Throws_WhenDefaultInventoryAccountHasNonStockSubType()
    {
        var itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
        var accRepo = Substitute.For<IRepository<Account, Guid>>();

        var accId = Guid.NewGuid();
        // Asset account but BankAccount sub-type instead of Stock
        var bankAcc = new Account(accId, _companyId, "1200", "Bank Account", AccountType.Asset)
        {
            IsGroup = false,
            AccountSubType = AccountSubType.BankAccount
        };
        accRepo.FindAsync(accId).Returns(bankAcc);

        var appService = new ItemGroupAppService(itemGroupRepo);
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<Account, Guid>>().Returns(accRepo);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(appService, lazySp);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await appService.CreateAsync(new CreateItemGroupDto
            {
                Name = "Raw Materials",
                DefaultInventoryAccountId = accId
            });
        });

        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidAccountType);
    }

    // =========================================================================
    // PR #58510 & #58511: Production Plan Work Orders Missing BOM Validation
    // =========================================================================

    [Fact]
    public async Task ProductionPlan_GenerateWorkOrders_ThrowsBomNotFound_WhenItemLacksBOM()
    {
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        var mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        var numberGen = Substitute.For<IDocumentNumberGenerator>();
        var bomValService = new BomValidationService(bomRepo, Substitute.For<IRepository<Item, Guid>>());

        var planId = Guid.NewGuid();
        var plan = new ProductionPlan(planId, _companyId, "PP-001", DateTime.UtcNow);
        // Add planned item with Guid.Empty as BomId
        var plannedItem = new ProductionPlanItem(Guid.NewGuid(), planId, _itemId, "Finished Good A", Guid.Empty, 10m);
        plan.AddPlannedItem(plannedItem);
        plan.Submit();

        planRepo.GetAsync(planId, includeDetails: true).Returns(plan);
        woRepo.GetQueryableAsync().Returns(Task.FromResult(new List<WorkOrder>().AsQueryable()));
        bomRepo.WithDetailsAsync().Returns(Task.FromResult(new List<BillOfMaterials>().AsQueryable()));

        var appService = new ProductionPlanAppService(planRepo, bomRepo, woRepo, mrRepo, numberGen, bomValService);
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        var compRepo = Substitute.For<IRepository<Company, Guid>>();
        lazySp.LazyGetRequiredService<IRepository<Company, Guid>>().Returns(compRepo);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(appService, lazySp);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await appService.GenerateWorkOrdersAsync(planId);
        });

        ex.Code.ShouldBe(MyERPDomainErrorCodes.BomNotFound);
        (ex.Data["items"]?.ToString() ?? string.Empty).ShouldContain("Finished Good A");
    }

    [Fact]
    public async Task ProductionPlan_GenerateWorkOrders_ThrowsBomNotFound_WhenBOMIsInactive()
    {
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        var mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        var numberGen = Substitute.For<IDocumentNumberGenerator>();
        var bomValService = new BomValidationService(bomRepo, Substitute.For<IRepository<Item, Guid>>());

        var planId = Guid.NewGuid();
        var bomId = Guid.NewGuid();
        var plan = new ProductionPlan(planId, _companyId, "PP-002", DateTime.UtcNow);
        var plannedItem = new ProductionPlanItem(Guid.NewGuid(), planId, _itemId, "Finished Good Inactive BOM", bomId, 10m);
        plan.AddPlannedItem(plannedItem);
        plan.Submit();

        var inactiveBom = new BillOfMaterials(bomId, _companyId, "BOM-001", _itemId)
        {
            Quantity = 1m,
            IsActive = false
        };

        planRepo.GetAsync(planId, includeDetails: true).Returns(plan);
        woRepo.GetQueryableAsync().Returns(Task.FromResult(new List<WorkOrder>().AsQueryable()));
        bomRepo.WithDetailsAsync().Returns(Task.FromResult(new List<BillOfMaterials> { inactiveBom }.AsQueryable()));

        var appService = new ProductionPlanAppService(planRepo, bomRepo, woRepo, mrRepo, numberGen, bomValService);
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        var compRepo = Substitute.For<IRepository<Company, Guid>>();
        lazySp.LazyGetRequiredService<IRepository<Company, Guid>>().Returns(compRepo);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(appService, lazySp);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await appService.GenerateWorkOrdersAsync(planId);
        });

        ex.Code.ShouldBe(MyERPDomainErrorCodes.BomNotFound);
        (ex.Data["items"]?.ToString() ?? string.Empty).ShouldContain("Finished Good Inactive BOM");
    }

    [Fact]
    public async Task ProductionPlan_CalculateMaterialRequirements_ClassifiesManufactureItemsWithoutSubBom()
    {
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();

        var planId = Guid.NewGuid();
        var bomId = Guid.NewGuid();
        var plan = new ProductionPlan(planId, _companyId, "PP-003", DateTime.UtcNow);
        var plannedItem = new ProductionPlanItem(Guid.NewGuid(), planId, _itemId, "FG Item", bomId, 5m);
        plan.AddPlannedItem(plannedItem);

        var bom = new BillOfMaterials(bomId, _companyId, "BOM-001", _itemId) { Quantity = 1m };
        var comp1Id = Guid.NewGuid(); // manufactured component without subBom
        var comp2Id = Guid.NewGuid(); // regular purchased raw material

        bom.Items.Add(new BomItem(Guid.NewGuid(), bomId, comp1Id, "Sub Component", 1m, 10m, "Unit"));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bomId, comp2Id, "Raw Material", 2m, 5m, "Unit"));

        var mfgItem = new Item(comp1Id, _companyId, "COMP-1", "Sub Component", ItemType.Goods)
        {
            DefaultMaterialRequestType = MyERP.Purchasing.MaterialRequestType.Manufacture
        };

        var purchaseItem = new Item(comp2Id, _companyId, "RAW-1", "Raw Material", ItemType.Goods)
        {
            DefaultMaterialRequestType = MyERP.Purchasing.MaterialRequestType.Purchase
        };

        planRepo.GetAsync(planId, includeDetails: true).Returns(plan);
        bomRepo.GetAsync(bomId).Returns(bom);
        bomRepo.GetQueryableAsync().Returns(Task.FromResult(new List<BillOfMaterials> { bom }.AsQueryable()));
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { mfgItem, purchaseItem }.AsQueryable()));
        binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin>().AsQueryable()));

        var bomValService = new BomValidationService(bomRepo, itemRepo);

        var appService = new ProductionPlanAppService(
            planRepo, bomRepo, Substitute.For<IRepository<WorkOrder, Guid>>(),
            Substitute.For<IRepository<MaterialRequest, Guid>>(),
            Substitute.For<IDocumentNumberGenerator>(), bomValService);

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<Item, Guid>>().Returns(itemRepo);
        lazySp.LazyGetRequiredService<IRepository<Bin, Guid>>().Returns(binRepo);

        var objectMapper = Substitute.For<Volo.Abp.ObjectMapping.IObjectMapper>();
        objectMapper.Map<ProductionPlan, ProductionPlanDto>(Arg.Any<ProductionPlan>())
            .Returns(callInfo => new ProductionPlanDto { Id = planId });

        lazySp.LazyGetService<Volo.Abp.ObjectMapping.IObjectMapper>(Arg.Any<Func<IServiceProvider, object>>()).Returns(objectMapper);
        lazySp.LazyGetService<Volo.Abp.ObjectMapping.IObjectMapper>().Returns(objectMapper);
        lazySp.LazyGetRequiredService<Volo.Abp.ObjectMapping.IObjectMapper>().Returns(objectMapper);
        lazySp.LazyGetService(typeof(Volo.Abp.ObjectMapping.IObjectMapper)).Returns(objectMapper);
        lazySp.LazyGetRequiredService(typeof(Volo.Abp.ObjectMapping.IObjectMapper)).Returns(objectMapper);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(appService, lazySp);

        await appService.CalculateMaterialRequirementsAsync(planId);

        plan.MaterialRequirements.Count.ShouldBe(2);
        var subAssemblyRow = plan.MaterialRequirements.FirstOrDefault(r => r.ItemId == comp1Id);
        subAssemblyRow.ShouldNotBeNull();
        subAssemblyRow.ProcurementType.ShouldBe(SubAssemblyType.InHouseManufacturing);

        var purchaseRow = plan.MaterialRequirements.FirstOrDefault(r => r.ItemId == comp2Id);
        purchaseRow.ShouldNotBeNull();
        purchaseRow.ProcurementType.ShouldBe(SubAssemblyType.MaterialRequest);
    }

    // =========================================================================
    // PR #59120: Sales Order cancellation resets item OrderedQty
    // =========================================================================

    [Fact]
    public async Task SalesOrderAppService_CancelAsync_ResetsItemOrderedQtyToZero()
    {
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();

        var appService = new SalesOrderAppService(
            soRepo, customerRepo, null!, null!, null!, null!, null!, null!);

        var soId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, customerId, "SO-2026-001", DateTime.UtcNow);
        so.AddItem(_itemId, "Drop Ship Widget", 10m, 50m, 0m);
        so.Submit();

        // Simulate drop-ship / back-to-back PO ordered qty
        so.Items[0].OrderedQty = 10m;
        so.Items[0].OrderedQty.ShouldBe(10m);

        soRepo.GetAsync(soId).Returns(so);

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        var soManager = Substitute.For<SalesOrderManager>(Substitute.For<IRepository<SalesOrder, Guid>>());
        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        var resManager = Substitute.For<StockReservationManager>(
            Substitute.For<IRepository<StockReservationEntry, Guid>>(),
            Substitute.For<IRepository<Bin, Guid>>(),
            Substitute.For<IRepository<StockLedgerEntry, Guid>>());
        var bundleDecomp = Substitute.For<ProductBundleDecompositionService>(
            Substitute.For<IRepository<ProductBundle, Guid>>());

        poRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseOrder>().AsQueryable()));
        dnRepo.GetQueryableAsync().Returns(Task.FromResult(new List<DeliveryNote>().AsQueryable()));
        siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice>().AsQueryable()));

        lazySp.LazyGetRequiredService<SalesOrderManager>().Returns(soManager);
        lazySp.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>().Returns(dnRepo);
        lazySp.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>().Returns(siRepo);
        lazySp.LazyGetRequiredService<IRepository<PurchaseOrder, Guid>>().Returns(poRepo);
        lazySp.LazyGetRequiredService<StockReservationManager>().Returns(resManager);
        lazySp.LazyGetRequiredService<ProductBundleDecompositionService>().Returns(bundleDecomp);

        var objectMapper = Substitute.For<Volo.Abp.ObjectMapping.IObjectMapper>();
        objectMapper.Map<SalesOrder, SalesOrderDto>(Arg.Any<SalesOrder>())
            .Returns(callInfo => new SalesOrderDto { Id = soId });

        lazySp.LazyGetService<Volo.Abp.ObjectMapping.IObjectMapper>(Arg.Any<Func<IServiceProvider, object>>()).Returns(objectMapper);
        lazySp.LazyGetService<Volo.Abp.ObjectMapping.IObjectMapper>().Returns(objectMapper);
        lazySp.LazyGetRequiredService<Volo.Abp.ObjectMapping.IObjectMapper>().Returns(objectMapper);
        lazySp.LazyGetService(typeof(Volo.Abp.ObjectMapping.IObjectMapper)).Returns(objectMapper);
        lazySp.LazyGetRequiredService(typeof(Volo.Abp.ObjectMapping.IObjectMapper)).Returns(objectMapper);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(appService, lazySp);

        await appService.CancelAsync(soId);

        // Assert: OrderedQty must be reset to 0 per ERPNext PR #59120
        so.Status.ShouldBe(DocumentStatus.Cancelled);
        so.Items[0].OrderedQty.ShouldBe(0m);
    }
}
