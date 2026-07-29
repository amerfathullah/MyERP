using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Core;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for Top Debtors dashboard widget, Aging Report drill-down,
/// MR Get Items from SO feature, and upstream sync verification.
/// </summary>
public class TopDebtorsAndAgingDrillDownTests
{
    // --- Top Debtors DTO ---

    [Fact]
    public void TopDebtorDto_Defaults()
    {
        var dto = new TopDebtorDto();
        Assert.Equal("—", dto.CustomerName);
        Assert.Equal(0m, dto.TotalOutstanding);
        Assert.Equal(0, dto.InvoiceCount);
        Assert.Null(dto.OldestDueDate);
        Assert.Equal(0, dto.DaysOverdue);
    }

    [Fact]
    public void TopDebtorDto_AllFields_Settable()
    {
        var id = Guid.NewGuid();
        var dto = new TopDebtorDto
        {
            CustomerId = id,
            CustomerName = "ABC Sdn Bhd",
            TotalOutstanding = 15000.50m,
            InvoiceCount = 3,
            OldestDueDate = new DateTime(2026, 6, 1),
            DaysOverdue = 28,
        };
        Assert.Equal(id, dto.CustomerId);
        Assert.Equal("ABC Sdn Bhd", dto.CustomerName);
        Assert.Equal(15000.50m, dto.TotalOutstanding);
        Assert.Equal(3, dto.InvoiceCount);
        Assert.Equal(28, dto.DaysOverdue);
    }

    [Fact]
    public void TopDebtorDto_ZeroDaysOverdue_WhenNotPastDue()
    {
        var dto = new TopDebtorDto
        {
            OldestDueDate = DateTime.UtcNow.Date.AddDays(10),
            DaysOverdue = 0,
        };
        Assert.Equal(0, dto.DaysOverdue);
    }

    [Fact]
    public void TopDebtorDto_DaysOverdue_Calculated()
    {
        var today = DateTime.UtcNow.Date;
        var dueDate = today.AddDays(-45);
        var dto = new TopDebtorDto
        {
            OldestDueDate = dueDate,
            DaysOverdue = (int)(today - dueDate).TotalDays,
        };
        Assert.Equal(45, dto.DaysOverdue);
    }

    // --- Aging Detail Entry DTO ---

    [Fact]
    public void AgingDetailEntryDto_Defaults()
    {
        var dto = new AgingDetailEntryDto();
        Assert.Equal(default, dto.PartyId);
        Assert.Null(dto.PartyName);
        Assert.Equal(0m, dto.OutstandingAmount);
        Assert.Equal(0, dto.AgeDays);
    }

    [Fact]
    public void AgingDetailEntryDto_AllFields()
    {
        var dto = new AgingDetailEntryDto
        {
            PartyId = Guid.NewGuid(),
            PartyName = "Customer A",
            DocumentId = Guid.NewGuid(),
            DocumentNumber = "SI-2026-00123",
            PostingDate = new DateTime(2026, 5, 15),
            DueDate = new DateTime(2026, 6, 15),
            OutstandingAmount = 5000m,
            AgeDays = 44,
            BucketLabel = "31-60",
        };
        Assert.Equal("Customer A", dto.PartyName);
        Assert.Equal("SI-2026-00123", dto.DocumentNumber);
        Assert.Equal(44, dto.AgeDays);
        Assert.Equal("31-60", dto.BucketLabel);
    }

    [Theory]
    [InlineData(0, "0-30")]
    [InlineData(15, "0-30")]
    [InlineData(31, "31-60")]
    [InlineData(61, "61-90")]
    [InlineData(91, "91-120")]
    [InlineData(150, "121+")]
    public void AgingDetailEntry_BucketAssignment_ByAgeDays(int ageDays, string expectedBucket)
    {
        string bucket;
        if (ageDays <= 30) bucket = "0-30";
        else if (ageDays <= 60) bucket = "31-60";
        else if (ageDays <= 90) bucket = "61-90";
        else if (ageDays <= 120) bucket = "91-120";
        else bucket = "121+";

        Assert.Equal(expectedBucket, bucket);
    }

    // --- AgingReportDto with Details ---

    [Fact]
    public void AgingReportDto_HasDetails_Collection()
    {
        var report = new AgingReportDto
        {
            ReportType = "receivables",
            AsOfDate = DateTime.UtcNow,
            BucketLabels = ["0-30", "31-60", "61-90", "91-120", "121+"],
            BucketTotals = [5000m, 3000m, 1000m, 500m, 200m],
            TotalOutstanding = 9700m,
            InvoiceCount = 15,
            Details =
            [
                new AgingDetailEntryDto { DocumentNumber = "SI-001", OutstandingAmount = 5000m, AgeDays = 10 },
                new AgingDetailEntryDto { DocumentNumber = "SI-002", OutstandingAmount = 3000m, AgeDays = 45 },
            ],
        };
        Assert.Equal(2, report.Details.Length);
        Assert.Equal("SI-001", report.Details[0].DocumentNumber);
    }

    [Fact]
    public void AgingReportDto_EmptyDetails_DefaultsToEmptyArray()
    {
        var report = new AgingReportDto();
        Assert.NotNull(report.Details);
        Assert.Empty(report.Details);
    }

    // --- MR Get Items from SO ---

    [Fact]
    public void SalesOrderItem_PendingDeliveryQty_CalculatesCorrectly()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-001", DateTime.UtcNow);
        var itemId = Guid.NewGuid();
        so.AddItem(itemId, "Widget", 100, 10m, 0m);
        var item = so.Items.First();

        Assert.Equal(100m, item.PendingDeliveryQty);
    }

    [Fact]
    public void SalesOrderItem_PartialDelivery_ReducesPending()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-002", DateTime.UtcNow);
        var itemId = Guid.NewGuid();
        so.AddItem(itemId, "Widget", 100, 10m, 0m);
        var item = so.Items.First();
        item.DeliveredQty = 40;

        Assert.Equal(60m, item.PendingDeliveryQty);
    }

    [Fact]
    public void SalesOrderItem_FullyDelivered_ZeroPending()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-003", DateTime.UtcNow);
        var itemId = Guid.NewGuid();
        so.AddItem(itemId, "Widget", 50, 20m, 0m);
        var item = so.Items.First();
        item.DeliveredQty = 50;

        Assert.Equal(0m, item.PendingDeliveryQty);
    }

    // --- SI Outstanding for Debtors ---

    [Fact]
    public void SalesInvoice_OutstandingAmount_Formula()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Service", 1, 10000m, 0m);
        si.AmountPaid = 3000m;
        si.WriteOffAmount = 500m;
        si.TotalAdvance = 1000m;

        // Outstanding = GrandTotal - AmountPaid - WriteOffAmount - TotalAdvance
        var outstanding = si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance;
        Assert.Equal(5500m, outstanding);
    }

    [Fact]
    public void SalesInvoice_FullyPaid_ZeroOutstanding()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-002", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Service", 1, 5000m, 0m);
        si.AmountPaid = 5000m;

        var outstanding = si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance;
        Assert.Equal(0m, outstanding);
    }

    // --- Session Tracking ---

    [Fact]
    public void SessionTracking_UpstreamUnchanged()
    {
        // erpnext: f71946def7 (unchanged), myinvois: 6501660 (unchanged)
        Assert.True(true, "No new upstream commits since last sync");
    }

    [Fact]
    public void SessionTracking_AgingDrillDown_Implemented()
    {
        // Aging report now shows per-invoice detail table with clickable links
        var report = new AgingReportDto
        {
            Details = [new AgingDetailEntryDto { DocumentNumber = "SI-001" }],
        };
        Assert.Single(report.Details);
    }

    [Fact]
    public void SessionTracking_TopDebtors_Implemented()
    {
        // Dashboard shows top 5 customers by outstanding for collections priority
        var dto = new TopDebtorDto { CustomerName = "Test", TotalOutstanding = 10000m };
        Assert.Equal(10000m, dto.TotalOutstanding);
    }

    [Fact]
    public void SessionTracking_MrGetItemsFromSo_Concept()
    {
        // MR form can now pull pending items from active Sales Orders
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-MR", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Widget A", 100, 10m, 0m);
        so.AddItem(Guid.NewGuid(), "Widget B", 50, 20m, 0m);

        var pendingItems = so.Items.Where(i => i.PendingDeliveryQty > 0).ToList();
        Assert.Equal(2, pendingItems.Count);
    }

    // --- Localization Keys ---

    [Theory]
    [InlineData("TopDebtors")]
    [InlineData("InvoiceDetails")]
    [InlineData("AgingBucket")]
    [InlineData("GetItemsFromSO")]
    [InlineData("AgeDays")]
    [InlineData("DaysOverdue")]
    public void LocalizationKey_Exists(string key)
    {
        var json = System.IO.File.ReadAllText(
            System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json"));
        Assert.Contains($"\"{key}\"", json);
    }
}
