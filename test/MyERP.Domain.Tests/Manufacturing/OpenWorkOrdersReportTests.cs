using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class OpenWorkOrdersReportTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _bomId = Guid.NewGuid();

    private WorkOrder CreateWo(string number, WorkOrderStatus status, decimal qty, decimal producedQty)
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, number, _itemId, _bomId, qty);
        typeof(WorkOrder).GetProperty(nameof(WorkOrder.Status))!.SetValue(wo, status);
        wo.ProducedQuantity = producedQty;
        return wo;
    }

    private static ManufacturingAppService CreateService(
        IRepository<BillOfMaterials, Guid> bomRepo,
        IRepository<WorkOrder, Guid> woRepo,
        IRepository<MaterialRequest, Guid> mrRepo,
        IDocumentNumberGenerator numGen,
        IRepository<StockEntry, Guid> seRepo,
        IRepository<Item, Guid> itemRepo)
    {
        var valService = (StockValuationService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(StockValuationService));
        var binService = (BinService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(BinService));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<StockEntry, Guid>>().Returns(seRepo);
        lazyProvider.LazyGetRequiredService<IRepository<Item, Guid>>().Returns(itemRepo);

        return new ManufacturingAppService(bomRepo, woRepo, mrRepo, numGen, valService, binService)
        {
            LazyServiceProvider = lazyProvider
        };
    }

    [Fact]
    public async Task OpenWorkOrders_ExcludesInactiveStatuses_AndCompletedProduction()
    {
        // Per ERPNext PR #60657 / commit ff9e9f88af:
        // Excludes Closed, Stopped, Completed, Cancelled, Draft.
        // Excludes if produced_qty >= qty.
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        var numGen = Substitute.For<IDocumentNumberGenerator>();
        var seRepo = Substitute.For<IRepository<StockEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();

        var woOpen = CreateWo("WO-OPEN", WorkOrderStatus.Submitted, 10m, 0m);
        var woClosed = CreateWo("WO-CLOSED", WorkOrderStatus.Closed, 10m, 0m);
        var woStopped = CreateWo("WO-STOPPED", WorkOrderStatus.Stopped, 10m, 0m);
        var woCompleted = CreateWo("WO-COMPLETED", WorkOrderStatus.Completed, 10m, 10m);
        var woFullyProduced = CreateWo("WO-PRODUCED", WorkOrderStatus.InProcess, 10m, 10m);
        var woDraft = CreateWo("WO-DRAFT", WorkOrderStatus.Draft, 10m, 0m);

        var workOrders = new List<WorkOrder> { woOpen, woClosed, woStopped, woCompleted, woFullyProduced, woDraft };
        woRepo.GetQueryableAsync().Returns(Task.FromResult(workOrders.AsQueryable()));
        seRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockEntry>().AsQueryable()));

        var item = new Item(_itemId, _companyId, "FG-001", "Finished Good Item", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var service = CreateService(bomRepo, woRepo, mrRepo, numGen, seRepo, itemRepo);

        var result = await service.GetOpenWorkOrdersReportAsync(_companyId);

        result.Count.ShouldBe(1);
        result[0].WorkOrderNumber.ShouldBe("WO-OPEN");
        result[0].RemainingQuantity.ShouldBe(10m);
    }

    [Fact]
    public async Task OpenWorkOrders_ExcludesWorkOrdersWithSubmittedStockEntry_ButIncludesDraftStockEntry()
    {
        // Per ERPNext PR #60657 / commit ff9e9f88af:
        // AND NOT EXISTS (SELECT name from Stock Entry where work_order = Work Order and docstatus = 1)
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        var numGen = Substitute.For<IDocumentNumberGenerator>();
        var seRepo = Substitute.For<IRepository<StockEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();

        var woWithSubmittedSe = CreateWo("WO-SE-SUBMITTED", WorkOrderStatus.InProcess, 20m, 5m);
        var woWithDraftSe = CreateWo("WO-SE-DRAFT", WorkOrderStatus.Submitted, 15m, 0m);

        var workOrders = new List<WorkOrder> { woWithSubmittedSe, woWithDraftSe };
        woRepo.GetQueryableAsync().Returns(Task.FromResult(workOrders.AsQueryable()));

        // Submitted stock entry for woWithSubmittedSe
        var submittedSe = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = woWithSubmittedSe.Id
        };
        typeof(StockEntry).GetProperty(nameof(StockEntry.Status))!.SetValue(submittedSe, DocumentStatus.Submitted);

        // Draft stock entry for woWithDraftSe (should NOT disqualify it from open work orders)
        var draftSe = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = woWithDraftSe.Id
        };
        typeof(StockEntry).GetProperty(nameof(StockEntry.Status))!.SetValue(draftSe, DocumentStatus.Draft);

        seRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockEntry> { submittedSe, draftSe }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "FG-001", "Finished Good Item", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var service = CreateService(bomRepo, woRepo, mrRepo, numGen, seRepo, itemRepo);

        var result = await service.GetOpenWorkOrdersReportAsync(_companyId);

        // Only woWithDraftSe remains open because it has NO submitted stock entry
        result.Count.ShouldBe(1);
        result[0].WorkOrderNumber.ShouldBe("WO-SE-DRAFT");
    }

    [Fact]
    public async Task WorkOrdersInProgress_IncludesOnlyOrdersWithSubmittedStockEntry_AndExcludesStoppedOrClosed()
    {
        // Per ERPNext PR #60657 / commit ff9e9f88af:
        // Work Orders in Progress requires EXISTS (submitted Stock Entry) and excludes Stopped, Closed.
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        var numGen = Substitute.For<IDocumentNumberGenerator>();
        var seRepo = Substitute.For<IRepository<StockEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();

        var woInProgress = CreateWo("WO-IN-PROGRESS", WorkOrderStatus.InProcess, 50m, 10m);
        var woNoStockEntry = CreateWo("WO-NO-SE", WorkOrderStatus.Submitted, 30m, 0m);
        var woStoppedWithSe = CreateWo("WO-STOPPED", WorkOrderStatus.Stopped, 20m, 5m);

        var workOrders = new List<WorkOrder> { woInProgress, woNoStockEntry, woStoppedWithSe };
        woRepo.GetQueryableAsync().Returns(Task.FromResult(workOrders.AsQueryable()));

        var se1 = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = woInProgress.Id
        };
        typeof(StockEntry).GetProperty(nameof(StockEntry.Status))!.SetValue(se1, DocumentStatus.Submitted);

        var se2 = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = woStoppedWithSe.Id
        };
        typeof(StockEntry).GetProperty(nameof(StockEntry.Status))!.SetValue(se2, DocumentStatus.Submitted);

        seRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockEntry> { se1, se2 }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "FG-001", "Finished Good Item", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var service = CreateService(bomRepo, woRepo, mrRepo, numGen, seRepo, itemRepo);

        var result = await service.GetWorkOrdersInProgressReportAsync(_companyId);

        // Only woInProgress is in progress: woNoStockEntry has no stock entry, woStoppedWithSe is stopped
        result.Count.ShouldBe(1);
        result[0].WorkOrderNumber.ShouldBe("WO-IN-PROGRESS");
        result[0].StockEntryCount.ShouldBe(1);
        result[0].RemainingQuantity.ShouldBe(40m);
    }
}
