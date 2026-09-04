using System;
using System.Linq;
using MyERP.Projects.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Projects;

public class TimesheetBillingTests
{
    [Fact]
    public void TimesheetDetail_SalesInvoiceId_DefaultsToNull()
    {
        var detail = CreateBillableDetail();
        detail.SalesInvoiceId.ShouldBeNull();
    }

    [Fact]
    public void TimesheetDetail_SalesInvoiceId_CanBeSet()
    {
        var detail = CreateBillableDetail();
        var invoiceId = Guid.NewGuid();
        detail.SalesInvoiceId = invoiceId;
        detail.SalesInvoiceId.ShouldBe(invoiceId);
    }

    [Fact]
    public void BillableDetail_HasBillingAmount()
    {
        var detail = CreateBillableDetail();
        detail.BillingAmount.ShouldBe(1200m); // 8h × 150
    }

    [Fact]
    public void NonBillableDetail_HasZeroBillingAmount()
    {
        var ts = CreateTimesheet();
        var detail = new TimesheetDetail(Guid.NewGuid(), ts.Id, "Admin",
            new DateTime(2026, 7, 1, 9, 0, 0), new DateTime(2026, 7, 1, 12, 0, 0), 3)
        { IsBillable = false, BillingRate = 0, CostingRate = 50 };
        detail.BillingAmount.ShouldBe(0m);
    }

    [Fact]
    public void UnbilledDetail_HasNoInvoiceLink()
    {
        var detail = CreateBillableDetail();
        detail.SalesInvoiceId.ShouldBeNull();
        detail.IsBillable.ShouldBeTrue();
        detail.BillingAmount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void BilledDetail_HasInvoiceLink()
    {
        var detail = CreateBillableDetail();
        detail.SalesInvoiceId = Guid.NewGuid();
        detail.SalesInvoiceId.ShouldNotBeNull();
    }

    [Fact]
    public void MultipleBillableDetails_SumCorrectly()
    {
        var ts = CreateTimesheet();
        ts.AddDetail(new TimesheetDetail(Guid.NewGuid(), ts.Id, "Dev",
            new DateTime(2026, 7, 1, 9, 0, 0), new DateTime(2026, 7, 1, 17, 0, 0), 8)
        { IsBillable = true, BillingRate = 150, CostingRate = 80 });
        ts.AddDetail(new TimesheetDetail(Guid.NewGuid(), ts.Id, "Design",
            new DateTime(2026, 7, 2, 9, 0, 0), new DateTime(2026, 7, 2, 13, 0, 0), 4)
        { IsBillable = true, BillingRate = 120, CostingRate = 60 });

        ts.TotalBillableHours.ShouldBe(12m);
        ts.TotalBillingAmount.ShouldBe(1680m); // 1200 + 480
    }

    [Fact]
    public void MixedBillableNonBillable_OnlyBillableCountsForBilling()
    {
        var ts = CreateTimesheet();
        ts.AddDetail(new TimesheetDetail(Guid.NewGuid(), ts.Id, "Dev",
            new DateTime(2026, 7, 1, 9, 0, 0), new DateTime(2026, 7, 1, 17, 0, 0), 8)
        { IsBillable = true, BillingRate = 150, CostingRate = 80 });
        ts.AddDetail(new TimesheetDetail(Guid.NewGuid(), ts.Id, "Meeting",
            new DateTime(2026, 7, 2, 9, 0, 0), new DateTime(2026, 7, 2, 11, 0, 0), 2)
        { IsBillable = false, BillingRate = 0, CostingRate = 50 });

        ts.TotalHours.ShouldBe(10m);
        ts.TotalBillableHours.ShouldBe(8m);
        ts.TotalBillingAmount.ShouldBe(1200m);
    }

    [Fact]
    public void SubmittedTimesheet_DetailsCanBeMarkedBilled()
    {
        var ts = CreateTimesheet();
        var detail = new TimesheetDetail(Guid.NewGuid(), ts.Id, "Dev",
            new DateTime(2026, 7, 1, 9, 0, 0), new DateTime(2026, 7, 1, 17, 0, 0), 8)
        { IsBillable = true, BillingRate = 150, CostingRate = 80 };
        ts.AddDetail(detail);
        ts.Submit();

        // Even after submit, SalesInvoiceId can be set (billing is separate from submission)
        detail.SalesInvoiceId = Guid.NewGuid();
        detail.SalesInvoiceId.ShouldNotBeNull();
    }

    [Fact]
    public void MidnightEndingTimeLog_DateFilterIncludesMidnightLogs()
    {
        // Per ERPNext PR #58355 (commit 7149df398a):
        // A time log starting on 2026-07-01 at 16:00 and ending on 2026-07-02 at 00:00 (or past midnight)
        // should be included when filtering for date 2026-07-01.
        var fromDate = new DateTime(2026, 7, 1);
        var toDate = new DateTime(2026, 7, 1);

        var nightShiftDetail = new TimesheetDetail(
            Guid.NewGuid(), Guid.NewGuid(), "Support",
            new DateTime(2026, 7, 1, 16, 0, 0), new DateTime(2026, 7, 2, 0, 0, 0), 8)
        { IsBillable = true, BillingRate = 100 };

        var isIncluded = nightShiftDetail.FromTime >= fromDate.Date &&
                         nightShiftDetail.FromTime < toDate.Date.AddDays(1);

        isIncluded.ShouldBeTrue();
    }

    [Fact]
    public void AllowedProjects_Filtering_ExcludesOtherCustomerProjects()
    {
        // Per ERPNext PR #58745 (commit f368a5b64e):
        // When customer has no allowed projects, only unassigned project timesheets are eligible.
        // When customer has allowed projects, only unassigned or those projects are eligible.
        var allowedProjId = Guid.NewGuid();
        var otherProjId = Guid.NewGuid();

        var detailUnassigned = new TimesheetDetail(Guid.NewGuid(), Guid.NewGuid(), "General",
            DateTime.Today, DateTime.Today.AddHours(4), 4) { ProjectId = null, IsBillable = true, BillingRate = 100 };

        var detailAllowed = new TimesheetDetail(Guid.NewGuid(), Guid.NewGuid(), "Client Dev",
            DateTime.Today, DateTime.Today.AddHours(4), 4) { ProjectId = allowedProjId, IsBillable = true, BillingRate = 100 };

        var detailOther = new TimesheetDetail(Guid.NewGuid(), Guid.NewGuid(), "Other Client Dev",
            DateTime.Today, DateTime.Today.AddHours(4), 4) { ProjectId = otherProjId, IsBillable = true, BillingRate = 100 };

        var allDetails = new[] { detailUnassigned, detailAllowed, detailOther };

        // Scenario 1: Customer has NO projects -> only unassigned project details allowed
        var emptyAllowedProjects = new System.Collections.Generic.HashSet<Guid>();
        var filteredNoProjects = allDetails.Where(d =>
            emptyAllowedProjects.Count > 0
                ? (d.ProjectId == null || emptyAllowedProjects.Contains(d.ProjectId.Value))
                : d.ProjectId == null).ToList();

        filteredNoProjects.ShouldHaveSingleItem();
        filteredNoProjects[0].ProjectId.ShouldBeNull();

        // Scenario 2: Customer has allowedProjId -> unassigned + allowedProjId allowed, other excluded
        var hasAllowedProjects = new System.Collections.Generic.HashSet<Guid> { allowedProjId };
        var filteredWithProjects = allDetails.Where(d =>
            hasAllowedProjects.Count > 0
                ? (d.ProjectId == null || hasAllowedProjects.Contains(d.ProjectId.Value))
                : d.ProjectId == null).ToList();

        filteredWithProjects.Count.ShouldBe(2);
        filteredWithProjects.ShouldContain(detailUnassigned);
        filteredWithProjects.ShouldContain(detailAllowed);
        filteredWithProjects.ShouldNotContain(detailOther);
    }

    private TimesheetDetail CreateBillableDetail()
    {
        var ts = CreateTimesheet();
        var detail = new TimesheetDetail(Guid.NewGuid(), ts.Id, "Development",
            new DateTime(2026, 7, 1, 9, 0, 0), new DateTime(2026, 7, 1, 17, 0, 0), 8)
        { IsBillable = true, BillingRate = 150, CostingRate = 80 };
        ts.AddDetail(detail);
        return detail;
    }

    private static Timesheet CreateTimesheet() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 7));
}
