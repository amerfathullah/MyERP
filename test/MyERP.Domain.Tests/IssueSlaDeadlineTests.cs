using System;
using MyERP.Support.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for ServiceLevelAgreement.ComputeDeadline and its wiring into Issue.ApplySla —
/// previously dead code (zero callers), the resulting deadline is informational only and
/// does not change the existing elapsed-vs-target-hours breach check in Issue.Resolve().
/// </summary>
public class IssueSlaDeadlineTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Fact]
    public void ComputeDeadline_WithNoServiceDays_FallsBackTo24x7()
    {
        var sla = new ServiceLevelAgreement(Guid.NewGuid(), CompanyId, "Default SLA", 24, 4);
        var from = new DateTime(2026, 8, 24, 9, 0, 0); // Monday 9am

        var deadline = sla.ComputeDeadline(from, 4);

        Assert.Equal(from.AddHours(4), deadline);
    }

    [Fact]
    public void ComputeDeadline_SkipsToNextConfiguredWorkingDay_WhenTargetFallsAfterHours()
    {
        var sla = new ServiceLevelAgreement(Guid.NewGuid(), CompanyId, "Business Hours SLA", 24, 8);
        sla.AddServiceDay(new ServiceDay(Guid.NewGuid(), sla.Id, DayOfWeek.Friday, TimeSpan.FromHours(9), TimeSpan.FromHours(17)));
        sla.AddServiceDay(new ServiceDay(Guid.NewGuid(), sla.Id, DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17)));

        var fridayAfternoon = new DateTime(2026, 8, 21, 16, 0, 0); // Friday 4pm, 8 working hours target
        var deadline = sla.ComputeDeadline(fridayAfternoon, 8);

        // 1 hour left in Friday's window (4pm-5pm), remaining 7 hours roll to Monday 9am + 7h = 4pm.
        Assert.Equal(new DateTime(2026, 8, 24, 16, 0, 0), deadline);
    }

    [Fact]
    public void ApplySla_StoresComputedDeadlines_AlongsideTargetHours()
    {
        var issue = new Issue(Guid.NewGuid(), CompanyId, "Test issue");
        var sla = new ServiceLevelAgreement(Guid.NewGuid(), CompanyId, "Default SLA", 24, 4);
        var responseBy = sla.ComputeDeadline(issue.OpeningDate, 4);
        var resolutionBy = sla.ComputeDeadline(issue.OpeningDate, 24);

        issue.ApplySla(sla.Id, 4, 24, responseBy, resolutionBy);

        Assert.Equal(4, issue.FirstResponseTime);
        Assert.Equal(24, issue.ResolutionTime);
        Assert.Equal(responseBy, issue.ResponseByDate);
        Assert.Equal(resolutionBy, issue.ResolutionByDate);
    }

    [Fact]
    public void ApplySla_WithoutDeadlines_LeavesThemNull()
    {
        var issue = new Issue(Guid.NewGuid(), CompanyId, "Test issue");

        issue.ApplySla(Guid.NewGuid(), 4, 24);

        Assert.Null(issue.ResponseByDate);
        Assert.Null(issue.ResolutionByDate);
    }
}
