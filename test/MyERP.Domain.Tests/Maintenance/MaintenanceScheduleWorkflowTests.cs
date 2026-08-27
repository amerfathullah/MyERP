using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Maintenance;
using MyERP.Maintenance.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Maintenance;

/// <summary>
/// Unit tests for Maintenance Schedule workflow, visit pre-population, and summary metrics.
/// Verifies rules from erpnext/maintenance/doctype/maintenance_schedule (#6009).
/// </summary>
public class MaintenanceScheduleWorkflowTests
{
    private readonly IRepository<MaintenanceSchedule, Guid> _scheduleRepo = Substitute.For<IRepository<MaintenanceSchedule, Guid>>();
    private readonly MaintenanceScheduleAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public MaintenanceScheduleWorkflowTests()
    {
        _appService = new MaintenanceScheduleAppService(_scheduleRepo);
    }

    [Fact]
    public async Task MakeMaintenanceVisitAsync_SubmittedSchedule_CreatesPrePopulatedVisitDto()
    {
        var scheduleId = Guid.NewGuid();
        var schedule = new MaintenanceSchedule(scheduleId, _companyId, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddYears(1), "Quarterly")
        {
            CustomerId = _customerId,
            ItemId = _itemId
        };
        var detailId = Guid.NewGuid();
        var scheduledDate = DateTime.UtcNow.Date.AddMonths(3);
        schedule.AddDetail(new MaintenanceScheduleDetail(detailId, scheduleId, scheduledDate));
        schedule.Submit();

        _scheduleRepo.GetAsync(scheduleId).Returns(schedule);

        var input = new MakeMaintenanceVisitInput
        {
            ScheduleDetailId = detailId
        };

        var result = await _appService.MakeMaintenanceVisitAsync(scheduleId, input);

        Assert.NotNull(result);
        Assert.Equal(_companyId, result.CompanyId);
        Assert.Equal(_customerId, result.CustomerId);
        Assert.Equal(scheduleId, result.MaintenanceScheduleId);
        Assert.Equal(detailId, result.MaintenanceScheduleDetailId);
        Assert.Equal(scheduledDate, result.VisitDate);
        Assert.Single(result.Purposes);
        Assert.Equal(_itemId, result.Purposes[0].ItemId);
    }

    [Fact]
    public async Task MakeMaintenanceVisitAsync_UnsubmittedSchedule_ThrowsValidationException()
    {
        var scheduleId = Guid.NewGuid();
        var draftSchedule = new MaintenanceSchedule(scheduleId, _companyId, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddYears(1), "Quarterly")
        {
            CustomerId = _customerId,
            ItemId = _itemId
        }; // Draft status (not submitted)

        _scheduleRepo.GetAsync(scheduleId).Returns(draftSchedule);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.MakeMaintenanceVisitAsync(scheduleId));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsAccurateMetrics()
    {
        var scheduleId = Guid.NewGuid();
        var schedule = new MaintenanceSchedule(scheduleId, _companyId, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddYears(1), "Quarterly");
        var d1 = new MaintenanceScheduleDetail(Guid.NewGuid(), scheduleId, DateTime.UtcNow.Date.AddDays(-30)) { IsCompleted = true };
        var d2 = new MaintenanceScheduleDetail(Guid.NewGuid(), scheduleId, DateTime.UtcNow.Date.AddDays(30)) { IsCompleted = false };
        var d3 = new MaintenanceScheduleDetail(Guid.NewGuid(), scheduleId, DateTime.UtcNow.Date.AddDays(90)) { IsCompleted = false };
        var d4 = new MaintenanceScheduleDetail(Guid.NewGuid(), scheduleId, DateTime.UtcNow.Date.AddDays(180)) { IsCompleted = false };

        schedule.AddDetail(d1);
        schedule.AddDetail(d2);
        schedule.AddDetail(d3);
        schedule.AddDetail(d4);

        _scheduleRepo.GetAsync(scheduleId).Returns(schedule);

        var summary = await _appService.GetSummaryAsync(scheduleId);

        Assert.NotNull(summary);
        Assert.Equal(scheduleId, summary.ScheduleId);
        Assert.Equal(4, summary.TotalVisits);
        Assert.Equal(1, summary.CompletedVisits);
        Assert.Equal(3, summary.PendingVisits);
        Assert.Equal(25m, summary.CompletionPercentage);
        Assert.Equal(d2.ScheduledDate, summary.NextScheduledDate);
    }
}
