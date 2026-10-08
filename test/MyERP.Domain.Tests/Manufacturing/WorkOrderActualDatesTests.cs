using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Unit tests verifying ERPNext PR #60196 (commits 1e3b9e39be / e3306050b5):
/// Derive actual start date, actual end date, and lead time from submitted Stock Entries,
/// ignore unsubmitted drafts/cancelled entries, and recompute on cancellation.
/// </summary>
public class WorkOrderActualDatesTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _bomId = Guid.NewGuid();

    private WorkOrderManager CreateManager()
    {
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();
        return new WorkOrderManager(itemRepo, bomRepo, settingsRepo);
    }

    [Fact]
    public async Task ActualDates_Ignore_Unsubmitted_Stock_Entries()
    {
        var manager = CreateManager();
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-ACT-001", _itemId, _bomId, 2m);
        wo.Submit();
        wo.Start();

        var today = DateTime.UtcNow.Date;
        var entries = new List<StockEntry>();

        // Entry 1: Cancelled at 02:00
        var se1 = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, today.AddHours(2))
        {
            WorkOrderId = wo.Id,
        };
        se1.AddItem(_itemId, 1m, targetWarehouseId: Guid.NewGuid());
        se1.Submit();
        se1.Post();
        se1.Cancel();
        entries.Add(se1);

        // Entry 2: Draft at 01:30 (unsubmitted)
        var seDraft = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, today.AddHours(1.5))
        {
            WorkOrderId = wo.Id,
        };
        seDraft.AddItem(_itemId, 1m, targetWarehouseId: Guid.NewGuid());
        entries.Add(seDraft);

        // Entry 3: Submitted (Posted) at 05:00 with qty 2, completing the WO
        var seSubmitted = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, today.AddHours(5))
        {
            WorkOrderId = wo.Id,
        };
        seSubmitted.AddItem(_itemId, 2m, targetWarehouseId: Guid.NewGuid());
        seSubmitted.Submit();
        seSubmitted.Post();
        entries.Add(seSubmitted);

        var seRepo = Substitute.For<IRepository<StockEntry, Guid>>();
        seRepo.GetQueryableAsync().Returns(Task.FromResult(entries.AsQueryable()));

        // Record production to complete the WO
        wo.RecordProduction(2m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);

        await manager.UpdateActualDatesAsync(wo, seRepo);

        // Actual start date and end date should be 05:00 (ignoring cancelled at 02:00 and draft at 01:30)
        wo.ActualStartDate.ShouldBe(today.AddHours(5));
        wo.ActualEndDate.ShouldBe(today.AddHours(5));
        wo.LeadTime.ShouldBe(0m);
    }

    [Fact]
    public async Task ActualDates_Recompute_On_Cancellation()
    {
        var manager = CreateManager();
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-ACT-002", _itemId, _bomId, 2m);
        wo.Submit();
        wo.Start();

        var today = DateTime.UtcNow.Date;
        var entries = new List<StockEntry>();

        // First manufacture at 03:00, qty 1
        var se1 = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, today.AddHours(3))
        {
            WorkOrderId = wo.Id,
        };
        se1.AddItem(_itemId, 1m, targetWarehouseId: Guid.NewGuid());
        se1.Submit();
        se1.Post();
        entries.Add(se1);

        // Second manufacture at 05:00, qty 1
        var se2 = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, today.AddHours(5))
        {
            WorkOrderId = wo.Id,
        };
        se2.AddItem(_itemId, 1m, targetWarehouseId: Guid.NewGuid());
        se2.Submit();
        se2.Post();
        entries.Add(se2);

        var seRepo = Substitute.For<IRepository<StockEntry, Guid>>();
        seRepo.GetQueryableAsync().Returns(Task.FromResult(entries.AsQueryable()));

        wo.RecordProduction(1m);
        wo.RecordProduction(1m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);

        await manager.UpdateActualDatesAsync(wo, seRepo);

        // Start = 03:00, End = 05:00, LeadTime = 120 mins
        wo.ActualStartDate.ShouldBe(today.AddHours(3));
        wo.ActualEndDate.ShouldBe(today.AddHours(5));
        wo.LeadTime.ShouldBe(120m);

        // Cancel first entry (03:00)
        se1.Cancel();
        wo.ReverseProduction(1m);
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);

        await manager.UpdateActualDatesAsync(wo, seRepo, currentStockEntry: se1);

        // Actual start date recomputes to 05:00; actual end date is null (not Completed); lead time is null
        wo.ActualStartDate.ShouldBe(today.AddHours(5));
        wo.ActualEndDate.ShouldBeNull();
        wo.LeadTime.ShouldBeNull();

        // Cancel second entry (05:00)
        se2.Cancel();
        wo.ReverseProduction(1m);

        await manager.UpdateActualDatesAsync(wo, seRepo, currentStockEntry: se2);

        // Both are null
        wo.ActualStartDate.ShouldBeNull();
        wo.ActualEndDate.ShouldBeNull();
        wo.LeadTime.ShouldBeNull();
    }

    [Fact]
    public async Task ActualDates_Derive_From_Job_Cards_When_Present()
    {
        var manager = CreateManager();
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-ACT-003", _itemId, _bomId, 10m);
        wo.Submit();
        wo.Start();

        var today = DateTime.UtcNow.Date;

        var jc1 = new JobCard(Guid.NewGuid(), _companyId, wo.Id, Guid.NewGuid(), 10m, 1)
        {
            StartedAt = today.AddHours(8),
            CompletedAt = today.AddHours(10)
        };

        var jc2 = new JobCard(Guid.NewGuid(), _companyId, wo.Id, Guid.NewGuid(), 10m, 2)
        {
            StartedAt = today.AddHours(10.5),
            CompletedAt = today.AddHours(12.5)
        };

        var jobCards = new List<JobCard> { jc1, jc2 };
        var jcRepo = Substitute.For<IRepository<JobCard, Guid>>();
        jcRepo.GetQueryableAsync().Returns(Task.FromResult(jobCards.AsQueryable()));

        var seRepo = Substitute.For<IRepository<StockEntry, Guid>>();
        seRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockEntry>().AsQueryable()));

        wo.RecordProduction(10m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);

        await manager.UpdateActualDatesAsync(wo, seRepo, jcRepo);

        // Start = 08:00, End = 12:30, LeadTime = 270 mins (4.5 hours)
        wo.ActualStartDate.ShouldBe(today.AddHours(8));
        wo.ActualEndDate.ShouldBe(today.AddHours(12.5));
        wo.LeadTime.ShouldBe(270m);
    }

    [Fact]
    public void WorkOrder_CalculateLeadTime_Direct()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-ACT-004", _itemId, _bomId, 5m);
        var start = new DateTime(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 8, 10, 30, 0, DateTimeKind.Utc);

        wo.SetActualDates(start, end);
        wo.ActualStartDate.ShouldBe(start);
        wo.ActualEndDate.ShouldBe(end);
        wo.LeadTime.ShouldBe(90m);

        // Reset
        wo.SetActualDates(null, null);
        wo.ActualStartDate.ShouldBeNull();
        wo.ActualEndDate.ShouldBeNull();
        wo.LeadTime.ShouldBeNull();
    }
}
