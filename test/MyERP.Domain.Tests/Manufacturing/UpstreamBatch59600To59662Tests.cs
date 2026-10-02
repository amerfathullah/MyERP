using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing.Services;
using MyERP.Purchasing;
using MyERP.Purchasing.DTOs;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.ObjectMapping;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class UpstreamBatch59600To59662Tests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _wipWarehouseId = Guid.NewGuid();

    // =========================================================================
    // PR #59615: Validate Combined Material Request Quantities for Production Plan
    // =========================================================================
    [Fact]
    public async Task MaterialRequestAppService_CreateAsync_Throws_WhenCombinedProductionPlanItemQtyExceedsAvailable()
    {
        var planId = Guid.NewGuid();
        var planItemId = Guid.NewGuid();

        var plan = new ProductionPlan(planId, _companyId, "PP-001", DateTime.UtcNow);
        var mrItem = new ProductionPlanMrItem(planItemId, planId, _itemId, "Raw Material", 10m)
        {
            PlannedQty = 10m,
            WarehouseId = _warehouseId
        };
        plan.AddMaterialRequirement(mrItem);

        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var planQuery = new List<ProductionPlan> { plan }.AsQueryable();
        planRepo.GetQueryableAsync().Returns(Task.FromResult(planQuery));

        var mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        var mrQuery = new List<MaterialRequest>().AsQueryable();
        mrRepo.GetQueryableAsync().Returns(Task.FromResult(mrQuery));

        var numGen = Substitute.For<IDocumentNumberGenerator>();
        numGen.GenerateAsync("MR", _companyId).Returns(Task.FromResult("MR-0001"));

        var appService = new MaterialRequestAppService(
            mrRepo,
            Substitute.For<IRepository<Item, Guid>>(),
            Substitute.For<IRepository<FiscalYear, Guid>>(),
            numGen,
            Substitute.For<BudgetValidationService>(
                Substitute.For<IRepository<Budget, Guid>>(),
                Substitute.For<IRepository<JournalEntry, Guid>>(),
                Substitute.For<IRepository<FiscalYear, Guid>>()),
            Substitute.For<ItemDefaultsResolutionService>(
                Substitute.For<IRepository<Item, Guid>>(),
                Substitute.For<IRepository<ItemGroup, Guid>>(),
                null));

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { new Item(_itemId, _companyId, "RM-01", "Raw Material", ItemType.Goods) { IsActive = true } }));
        var itemValidation = new ItemTransactionValidationService(itemRepo);

        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        whRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Warehouse, bool>>>())
            .Returns(Task.FromResult(new List<Warehouse> { new Warehouse(_warehouseId, _companyId, "Stores") }));

        var ppItemRepo = Substitute.For<IRepository<ProductionPlanMrItem, Guid>>();
        ppItemRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ProductionPlanMrItem, bool>>>())
            .Returns(Task.FromResult(new List<ProductionPlanMrItem> { mrItem }));

        var companyRestriction = Substitute.For<CompanyRestrictionValidationService>(null, null, null, null, null, null, null);

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<ProductionPlan, Guid>>().Returns(planRepo);
        lazySp.LazyGetRequiredService<IRepository<ProductionPlanMrItem, Guid>>().Returns(ppItemRepo);
        lazySp.LazyGetRequiredService<ItemTransactionValidationService>().Returns(itemValidation);
        lazySp.LazyGetRequiredService<IRepository<Warehouse, Guid>>().Returns(whRepo);
        lazySp.LazyGetRequiredService<CompanyRestrictionValidationService>().Returns(companyRestriction);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(appService, lazySp);

        // Two lines referencing the SAME plan item: 6 + 5 = 11 > 10 available
        var input = new CreateMaterialRequestDto
        {
            CompanyId = _companyId,
            Items = new List<CreateMaterialRequestItemDto>
            {
                new CreateMaterialRequestItemDto
                {
                    ItemId = _itemId,
                    ItemName = "Raw Material",
                    Quantity = 6m,
                    Uom = "Unit",
                    WarehouseId = _warehouseId,
                    ProductionPlanId = planId,
                    ProductionPlanMrItemId = planItemId
                },
                new CreateMaterialRequestItemDto
                {
                    ItemId = _itemId,
                    ItemName = "Raw Material",
                    Quantity = 5m,
                    Uom = "Unit",
                    WarehouseId = _warehouseId,
                    ProductionPlanId = planId,
                    ProductionPlanMrItemId = planItemId
                }
            }
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => appService.CreateAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task MaterialRequestAppService_CreateAsync_Succeeds_WhenCombinedProductionPlanItemQtyWithinAvailable()
    {
        var planId = Guid.NewGuid();
        var planItemId = Guid.NewGuid();

        var plan = new ProductionPlan(planId, _companyId, "PP-001", DateTime.UtcNow);
        var mrItem = new ProductionPlanMrItem(planItemId, planId, _itemId, "Raw Material", 10m)
        {
            PlannedQty = 10m,
            WarehouseId = _warehouseId
        };
        plan.AddMaterialRequirement(mrItem);

        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var planQuery = new List<ProductionPlan> { plan }.AsQueryable();
        planRepo.GetQueryableAsync().Returns(Task.FromResult(planQuery));

        var mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        var mrQuery = new List<MaterialRequest>().AsQueryable();
        mrRepo.GetQueryableAsync().Returns(Task.FromResult(mrQuery));

        var numGen = Substitute.For<IDocumentNumberGenerator>();
        numGen.GenerateAsync("MR", _companyId).Returns(Task.FromResult("MR-0001"));

        var appService = new MaterialRequestAppService(
            mrRepo,
            Substitute.For<IRepository<Item, Guid>>(),
            Substitute.For<IRepository<FiscalYear, Guid>>(),
            numGen,
            Substitute.For<BudgetValidationService>(
                Substitute.For<IRepository<Budget, Guid>>(),
                Substitute.For<IRepository<JournalEntry, Guid>>(),
                Substitute.For<IRepository<FiscalYear, Guid>>()),
            Substitute.For<ItemDefaultsResolutionService>(
                Substitute.For<IRepository<Item, Guid>>(),
                Substitute.For<IRepository<ItemGroup, Guid>>(),
                null));

        var mapper = Substitute.For<IObjectMapper>();
        mapper.Map<MaterialRequest, MaterialRequestDto>(Arg.Any<MaterialRequest>())
            .Returns(ci =>
            {
                var src = ci.Arg<MaterialRequest>();
                return new MaterialRequestDto
                {
                    Id = src.Id,
                    RequestNumber = src.RequestNumber,
                    CompanyId = src.CompanyId,
                    Items = src.Items.Select(i => new MaterialRequestItemDto
                    {
                        Id = i.Id,
                        ItemId = i.ItemId,
                        Quantity = i.Quantity,
                        ProductionPlanId = i.ProductionPlanId,
                        ProductionPlanMrItemId = i.ProductionPlanMrItemId
                    }).ToList()
                };
            });

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { new Item(_itemId, _companyId, "RM-01", "Raw Material", ItemType.Goods) { IsActive = true } }));
        var itemValidation = new ItemTransactionValidationService(itemRepo);

        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        whRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Warehouse, bool>>>())
            .Returns(Task.FromResult(new List<Warehouse> { new Warehouse(_warehouseId, _companyId, "Stores") }));

        var ppItemRepo = Substitute.For<IRepository<ProductionPlanMrItem, Guid>>();
        ppItemRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ProductionPlanMrItem, bool>>>())
            .Returns(Task.FromResult(new List<ProductionPlanMrItem> { mrItem }));

        var companyRestriction = Substitute.For<CompanyRestrictionValidationService>(null, null, null, null, null, null, null);

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<ProductionPlan, Guid>>().Returns(planRepo);
        lazySp.LazyGetRequiredService<IRepository<ProductionPlanMrItem, Guid>>().Returns(ppItemRepo);
        lazySp.LazyGetRequiredService<ItemTransactionValidationService>().Returns(itemValidation);
        lazySp.LazyGetRequiredService<IRepository<Warehouse, Guid>>().Returns(whRepo);
        lazySp.LazyGetRequiredService<CompanyRestrictionValidationService>().Returns(companyRestriction);
        lazySp.LazyGetService(typeof(IObjectMapper)).Returns(mapper);
        lazySp.LazyGetService(typeof(IObjectMapper), Arg.Any<object>()).Returns(mapper);
        lazySp.LazyGetService(typeof(IObjectMapper), Arg.Any<Func<IServiceProvider, object>>()).Returns(mapper);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(appService, lazySp);

        MaterialRequest? savedEntity = null;
        await mrRepo.InsertAsync(Arg.Do<MaterialRequest>(e => savedEntity = e));

        // Two lines referencing the SAME plan item: 4 + 5 = 9 <= 10 available
        var input = new CreateMaterialRequestDto
        {
            CompanyId = _companyId,
            Items = new List<CreateMaterialRequestItemDto>
            {
                new CreateMaterialRequestItemDto
                {
                    ItemId = _itemId,
                    ItemName = "Raw Material",
                    Quantity = 4m,
                    Uom = "Unit",
                    WarehouseId = _warehouseId,
                    ProductionPlanId = planId,
                    ProductionPlanMrItemId = planItemId
                },
                new CreateMaterialRequestItemDto
                {
                    ItemId = _itemId,
                    ItemName = "Raw Material",
                    Quantity = 5m,
                    Uom = "Unit",
                    WarehouseId = _warehouseId,
                    ProductionPlanId = planId,
                    ProductionPlanMrItemId = planItemId
                }
            }
        };

        var result = await appService.CreateAsync(input);
        savedEntity.ShouldNotBeNull();
        savedEntity.Items.Count.ShouldBe(2);
        savedEntity.Items.Sum(i => i.Quantity).ShouldBe(9m);
        savedEntity.Items.All(i => i.ProductionPlanId == planId && i.ProductionPlanMrItemId == planItemId).ShouldBeTrue();
    }

    // =========================================================================
    // PR #59600: Consume batches from every transfer in manufacture entries
    // =========================================================================
    [Fact]
    public void WorkOrderProductionService_CalculateTransferredBatchConsumption_MultipleTransfers()
    {
        var service = new WorkOrderProductionService(
            Substitute.For<IRepository<WorkOrder, Guid>>());

        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();

        // Transfer 1: Batch 1 (10)
        // Transfer 2: Batch 2 (5)
        var transferred = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, 10m, _warehouseId, _wipWarehouseId) { BatchId = batch1 },
            new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, 5m, _warehouseId, _wipWarehouseId) { BatchId = batch2 }
        };

        // First manufacture: 12 units -> Batch 1 (10), Batch 2 (2)
        var firstRun = service.CalculateTransferredBatchConsumption(
            _itemId, 12m, transferred, Array.Empty<StockEntryItem>());

        firstRun.Count.ShouldBe(2);
        firstRun.ShouldContain(x => x.BatchId == batch1 && x.Quantity == 10m);
        firstRun.ShouldContain(x => x.BatchId == batch2 && x.Quantity == 2m);

        // Second manufacture: prior consumed = Batch 1 (10), Batch 2 (2). Required = 3.
        var priorConsumed = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, 10m, _wipWarehouseId, null) { BatchId = batch1 },
            new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, 2m, _wipWarehouseId, null) { BatchId = batch2 }
        };

        var secondRun = service.CalculateTransferredBatchConsumption(
            _itemId, 3m, transferred, priorConsumed);

        secondRun.Count.ShouldBe(1);
        secondRun[0].BatchId.ShouldBe(batch2);
        secondRun[0].Quantity.ShouldBe(3m);
    }

    [Fact]
    public void WorkOrderProductionService_CalculateTransferredBatchConsumption_SingleMultiBatchTransfer()
    {
        var service = new WorkOrderProductionService(
            Substitute.For<IRepository<WorkOrder, Guid>>());

        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();

        // One transfer with two batches: Batch 1 (10), Batch 2 (5)
        var transferred = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, 10m, _warehouseId, _wipWarehouseId) { BatchId = batch1 },
            new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, 5m, _warehouseId, _wipWarehouseId) { BatchId = batch2 }
        };

        var result = service.CalculateTransferredBatchConsumption(
            _itemId, 15m, transferred, Array.Empty<StockEntryItem>());

        result.Count.ShouldBe(2);
        result.ShouldContain(x => x.BatchId == batch1 && x.Quantity == 10m);
        result.ShouldContain(x => x.BatchId == batch2 && x.Quantity == 5m);
    }

    // =========================================================================
    // PR #59601: Skip batch qty reserved by other vouchers in manufacture entries
    // =========================================================================
    [Fact]
    public void WorkOrderProductionService_CalculateTransferredBatchConsumption_SkipsReservedBatchQtyWhenCovered()
    {
        var service = new WorkOrderProductionService(
            Substitute.For<IRepository<WorkOrder, Guid>>());

        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();

        // Transferred: Batch 1 (15), Batch 2 (10)
        var transferred = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, 15m, _warehouseId, _wipWarehouseId) { BatchId = batch1 },
            new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, 10m, _warehouseId, _wipWarehouseId) { BatchId = batch2 }
        };

        // Another voucher reserved 7 of Batch 1 -> unreserved Batch 1 = 8, Batch 2 = 10. Total unreserved = 18 >= 15
        var unreserved = new Dictionary<Guid, decimal>
        {
            [batch1] = 8m,
            [batch2] = 10m
        };

        var result = service.CalculateTransferredBatchConsumption(
            _itemId, 15m, transferred, Array.Empty<StockEntryItem>(), unreserved);

        // Required 15: caps Batch 1 at 8, takes 7 from Batch 2
        result.Count.ShouldBe(2);
        result.ShouldContain(x => x.BatchId == batch1 && x.Quantity == 8m);
        result.ShouldContain(x => x.BatchId == batch2 && x.Quantity == 7m);
    }

    [Fact]
    public void WorkOrderProductionService_CalculateTransferredBatchConsumption_KeepsBatchesWhenUnreservedShort()
    {
        var service = new WorkOrderProductionService(
            Substitute.For<IRepository<WorkOrder, Guid>>());

        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();

        // Transferred: Batch 1 (15), Batch 2 (10)
        var transferred = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, 15m, _warehouseId, _wipWarehouseId) { BatchId = batch1 },
            new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, 10m, _warehouseId, _wipWarehouseId) { BatchId = batch2 }
        };

        // Another voucher reserved 12 of Batch 1 and 7 of Batch 2 -> unreserved Batch 1 = 3, Batch 2 = 3. Total unreserved = 6 < 15
        var unreserved = new Dictionary<Guid, decimal>
        {
            [batch1] = 3m,
            [batch2] = 3m
        };

        var result = service.CalculateTransferredBatchConsumption(
            _itemId, 15m, transferred, Array.Empty<StockEntryItem>(), unreserved);

        // When unreserved is short, keeps original transferred batches (takes 15 from Batch 1)
        // so submission reports reservation conflict
        result.Count.ShouldBe(1);
        result[0].BatchId.ShouldBe(batch1);
        result[0].Quantity.ShouldBe(15m);
    }

    // =========================================================================
    // PR #59616: Net Work Order Component Returns off Transferred Materials
    // =========================================================================
    [Fact]
    public void WorkOrderProductionService_CalculateTransferredBatchConsumption_NetsOffReturnedBatches()
    {
        var service = new WorkOrderProductionService(
            Substitute.For<IRepository<WorkOrder, Guid>>());

        var batch1 = Guid.NewGuid();

        // Transferred: Batch 1 (4 units)
        var transferred = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, 4m, _warehouseId, _wipWarehouseId) { BatchId = batch1 }
        };

        // Returned: Batch 1 (1 unit) - passed in priorConsumed / returned items
        var priorReturnsOrConsumed = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, 1m, _wipWarehouseId, _warehouseId) { BatchId = batch1 }
        };

        // When requesting remaining 3 units, Batch 1 satisfies 3 units (4 - 1 = 3 available)
        var result = service.CalculateTransferredBatchConsumption(
            _itemId, 3m, transferred, priorReturnsOrConsumed);

        result.Count.ShouldBe(1);
        result[0].BatchId.ShouldBe(batch1);
        result[0].Quantity.ShouldBe(3m);
    }
}
