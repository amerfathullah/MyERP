using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Manufacturing;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

/// <summary>
/// Unit tests for Master Production Schedule Production Plan creation and summary metrics.
/// Verifies rules from erpnext/manufacturing/doctype/master_production_schedule (#6006).
/// </summary>
public class MasterProductionScheduleWorkflowTests
{
    private readonly IRepository<MasterProductionSchedule, Guid> _mpsRepo = Substitute.For<IRepository<MasterProductionSchedule, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<MaterialRequest, Guid> _mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
    private readonly IRepository<ProductionPlan, Guid> _ppRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
    private readonly IRepository<BillOfMaterials, Guid> _bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
    private readonly IDocumentNumberGenerator _numGen = Substitute.For<IDocumentNumberGenerator>();
    private readonly MasterProductionScheduleService _leadTimeService;

    private readonly MasterProductionScheduleAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _bomId = Guid.NewGuid();

    public MasterProductionScheduleWorkflowTests()
    {
        var itemRepoForLeadTime = Substitute.For<IRepository<global::MyERP.Inventory.Entities.Item, Guid>>();
        var bomRepoForLeadTime = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        _leadTimeService = new MasterProductionScheduleService(itemRepoForLeadTime, bomRepoForLeadTime);

        _appService = new MasterProductionScheduleAppService(
            _mpsRepo, _soRepo, _mrRepo, _ppRepo, _bomRepo, _numGen, _leadTimeService);

        _numGen.GenerateAsync(Arg.Any<string>(), Arg.Any<Guid>())
            .Returns(Task.FromResult("PP-2026-00001"));
    }

    [Fact]
    public async Task MakeProductionPlanAsync_ValidItems_CreatesDraftProductionPlan()
    {
        var scheduleId = Guid.NewGuid();
        var schedule = new MasterProductionSchedule(scheduleId, _companyId, "MPS-2026-00001", new DateTime(2026, 6, 1), new DateTime(2026, 6, 1));
        var item = new MasterProductionScheduleItem(
            Guid.NewGuid(), scheduleId, _itemId, "Finished Widget", new DateTime(2026, 6, 20), 50m, 5)
        {
            BomId = _bomId
        };
        schedule.SetItems(new List<MasterProductionScheduleItem> { item });

        _mpsRepo.GetAsync(scheduleId, includeDetails: true).Returns(Task.FromResult(schedule));

        var boms = new List<BillOfMaterials>();
        _bomRepo.GetQueryableAsync().Returns(Task.FromResult(boms.AsQueryable()));

        ProductionPlan? savedPlan = null;
        await _ppRepo.InsertAsync(Arg.Do<ProductionPlan>(p => savedPlan = p), autoSave: true);

        var planId = await _appService.MakeProductionPlanAsync(scheduleId);

        Assert.NotEqual(Guid.Empty, planId);
        Assert.NotNull(savedPlan);
        Assert.Equal(_companyId, savedPlan.CompanyId);
        Assert.Single(savedPlan.PlannedItems);
        var planItem = savedPlan.PlannedItems[0];
        Assert.Equal(_itemId, planItem.ItemId);
        Assert.Equal(_bomId, planItem.BomId);
        Assert.Equal(50m, planItem.PlannedQty);
        Assert.Equal(item.OrderReleaseDate, planItem.PlannedStartDate);
    }

    [Fact]
    public async Task MakeProductionPlanAsync_NoItems_ThrowsValidationException()
    {
        var scheduleId = Guid.NewGuid();
        var schedule = new MasterProductionSchedule(scheduleId, _companyId, "MPS-2026-00001", new DateTime(2026, 6, 1), new DateTime(2026, 6, 1));
        _mpsRepo.GetAsync(scheduleId, includeDetails: true).Returns(Task.FromResult(schedule));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.MakeProductionPlanAsync(scheduleId));
        Assert.Equal(MyERPDomainErrorCodes.MasterProductionScheduleHasNoItems, ex.Code);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsAccurateMetrics()
    {
        var scheduleId = Guid.NewGuid();
        var schedule = new MasterProductionSchedule(scheduleId, _companyId, "MPS-2026-00001", new DateTime(2026, 6, 1), new DateTime(2026, 6, 1));
        var item1 = new MasterProductionScheduleItem(
            Guid.NewGuid(), scheduleId, _itemId, "Widget A", new DateTime(2026, 6, 20), 30m, 5);
        var item2 = new MasterProductionScheduleItem(
            Guid.NewGuid(), scheduleId, Guid.NewGuid(), "Widget B", new DateTime(2026, 6, 25), 20m, 10);
        schedule.SetItems(new List<MasterProductionScheduleItem> { item1, item2 });

        _mpsRepo.GetAsync(scheduleId, includeDetails: true).Returns(Task.FromResult(schedule));

        var summary = await _appService.GetSummaryAsync(scheduleId);

        Assert.NotNull(summary);
        Assert.Equal(scheduleId, summary.Id);
        Assert.Equal(2, summary.TotalItemsCount);
        Assert.Equal(50m, summary.TotalPlannedQty);
        Assert.Equal(new DateTime(2026, 6, 15), summary.EarliestReleaseDate);
        Assert.Equal(new DateTime(2026, 6, 25), summary.LatestDeliveryDate);
    }
}
