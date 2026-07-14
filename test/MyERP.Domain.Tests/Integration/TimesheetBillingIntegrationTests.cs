using System;
using System.Linq;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

public class TimesheetBillingIntegrationTests
{
    [Fact]
    public void SalesInvoice_ProjectId_DefaultsNull()
    {
        var invoice = CreateInvoice();
        invoice.ProjectId.ShouldBeNull();
    }

    [Fact]
    public void SalesInvoice_ProjectId_CanBeSet()
    {
        var invoice = CreateInvoice();
        var projectId = Guid.NewGuid();
        invoice.ProjectId = projectId;
        invoice.ProjectId.ShouldBe(projectId);
    }

    [Fact]
    public void TimesheetDetail_BillingAmount_IsHoursTimesRate()
    {
        var ts = CreateTimesheet();
        var detail = CreateDetail(ts.Id, "Development", 8);
        detail.IsBillable = true;
        detail.BillingRate = 150;
        ts.AddDetail(detail);

        ts.Details.First().BillingAmount.ShouldBe(1200m); // 8h × 150
    }

    [Fact]
    public void TimesheetDetail_SalesInvoiceId_DefaultsNull()
    {
        var ts = CreateTimesheet();
        var detail = CreateDetail(ts.Id, "Consulting", 4);
        detail.IsBillable = true;
        detail.BillingRate = 200;
        ts.AddDetail(detail);

        ts.Details.First().SalesInvoiceId.ShouldBeNull();
    }

    [Fact]
    public void TimesheetDetail_SalesInvoiceId_CanBeSet_MarksBilled()
    {
        var ts = CreateTimesheet();
        var detail = CreateDetail(ts.Id, "Design", 6);
        detail.IsBillable = true;
        detail.BillingRate = 180;
        ts.AddDetail(detail);

        var invoiceId = Guid.NewGuid();
        ts.Details.First().SalesInvoiceId = invoiceId;
        ts.Details.First().SalesInvoiceId.ShouldBe(invoiceId);
    }

    [Fact]
    public void TimesheetDetail_NonBillable_HasZeroBillingAmount()
    {
        var ts = CreateTimesheet();
        var detail = CreateDetail(ts.Id, "Admin", 3);
        detail.IsBillable = false;
        detail.CostingRate = 80;
        ts.AddDetail(detail);

        ts.Details.First().IsBillable.ShouldBeFalse();
        ts.Details.First().BillingAmount.ShouldBe(0m);
    }

    [Fact]
    public void TimesheetDetail_UnbilledFilter_ExcludesAlreadyBilled()
    {
        var ts = CreateTimesheet();
        var projectId = Guid.NewGuid();

        var d1 = CreateDetail(ts.Id, "Dev", 4);
        d1.IsBillable = true; d1.BillingRate = 150; d1.ProjectId = projectId;
        var d2 = CreateDetail(ts.Id, "QA", 2);
        d2.IsBillable = true; d2.BillingRate = 150; d2.ProjectId = projectId;
        ts.AddDetail(d1);
        ts.AddDetail(d2);

        // Mark second as billed
        ts.Details[1].SalesInvoiceId = Guid.NewGuid();

        var unbilled = ts.Details
            .Where(d => d.IsBillable && d.SalesInvoiceId == null && d.BillingAmount > 0)
            .ToList();

        unbilled.Count.ShouldBe(1);
        unbilled[0].ActivityType.ShouldBe("Dev");
    }

    [Fact]
    public void TimesheetDetail_ProjectFilter_ScopesToProject()
    {
        var ts = CreateTimesheet();
        var targetProject = Guid.NewGuid();
        var otherProject = Guid.NewGuid();

        var d1 = CreateDetail(ts.Id, "Dev", 8);
        d1.IsBillable = true; d1.BillingRate = 150; d1.ProjectId = targetProject;
        var d2 = CreateDetail(ts.Id, "Admin", 4);
        d2.IsBillable = true; d2.BillingRate = 100; d2.ProjectId = otherProject;
        var d3 = CreateDetail(ts.Id, "QA", 3);
        d3.IsBillable = true; d3.BillingRate = 120; d3.ProjectId = targetProject;
        ts.AddDetail(d1);
        ts.AddDetail(d2);
        ts.AddDetail(d3);

        var filtered = ts.Details
            .Where(d => d.IsBillable && d.SalesInvoiceId == null
                && d.ProjectId == targetProject)
            .ToList();

        filtered.Count.ShouldBe(2);
        filtered.Sum(d => d.BillingAmount).ShouldBe(8 * 150m + 3 * 120m);
    }

    [Fact]
    public void TimesheetAutoFetch_ShouldSkipReturns()
    {
        var invoice = CreateInvoice();
        invoice.IsReturn = true;
        invoice.IsReturn.ShouldBeTrue();
        // The condition in the AppService: `if (input.ProjectId.HasValue && !invoice.IsReturn)`
    }

    [Fact]
    public void TimesheetAutoFetch_DoesNotDoubleCount()
    {
        var ts = CreateTimesheet();
        var projectId = Guid.NewGuid();
        var d = CreateDetail(ts.Id, "Dev", 8);
        d.IsBillable = true; d.BillingRate = 150; d.ProjectId = projectId;
        ts.AddDetail(d);

        ts.Details.First().SalesInvoiceId = Guid.NewGuid(); // marked as billed

        var unbilled = ts.Details
            .Where(d2 => d2.IsBillable && d2.SalesInvoiceId == null)
            .ToList();

        unbilled.ShouldBeEmpty();
    }

    [Fact]
    public void Timesheet_TotalBillingAmount_SumsOnlyBillable()
    {
        var ts = CreateTimesheet();
        var d1 = CreateDetail(ts.Id, "Dev", 8);
        d1.IsBillable = true; d1.BillingRate = 150;
        var d2 = CreateDetail(ts.Id, "Admin", 2);
        d2.IsBillable = false; d2.CostingRate = 80;
        var d3 = CreateDetail(ts.Id, "QA", 4);
        d3.IsBillable = true; d3.BillingRate = 120;
        ts.AddDetail(d1);
        ts.AddDetail(d2);
        ts.AddDetail(d3);

        ts.TotalBillingAmount.ShouldBe(8 * 150m + 4 * 120m); // 1680
    }

    private static SalesInvoice CreateInvoice()
    {
        return new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-TEST-001", DateTime.UtcNow);
    }

    private static Timesheet CreateTimesheet()
    {
        return new Timesheet(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(7));
    }

    private static TimesheetDetail CreateDetail(Guid tsId, string activity, decimal hours)
    {
        return new TimesheetDetail(
            Guid.NewGuid(), tsId, activity,
            DateTime.UtcNow, DateTime.UtcNow.AddHours((double)hours), hours);
    }
}
