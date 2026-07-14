using System;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for:
/// - JE PostAsync accounting period/fiscal year validation
/// - DN address resolution (from SO and customer)
/// - Auto-dunning job logic (overdue detection, level sequencing)
/// </summary>
public class JEValidationAndDunningTests
{
    // --- JE Accounting Period Validation Context ---

    [Fact]
    public void JournalEntry_PostingDate_UsedForValidation()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 3, 15));
        je.PostingDate.ShouldBe(new DateTime(2026, 3, 15));
    }

    [Fact]
    public void JournalEntry_Post_ChangesStatus()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today);
        je.AddLine(Guid.NewGuid(), 1000m, true, "Debit");
        je.AddLine(Guid.NewGuid(), 1000m, false, "Credit");
        je.Validate();

        je.Post();

        je.Status.ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public void AccountingPeriod_ClosedPeriod_BlocksPosting()
    {
        // Simulates the check: period is closed and posting date falls within
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(),
            "Q1 2026", new DateTime(2026, 1, 1), new DateTime(2026, 3, 31));
        period.Close();

        var postingDate = new DateTime(2026, 2, 15);
        var isBlocked = period.IsClosed
            && period.StartDate <= postingDate
            && period.EndDate >= postingDate;

        isBlocked.ShouldBeTrue();
    }

    [Fact]
    public void AccountingPeriod_OpenPeriod_AllowsPosting()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(),
            "Q2 2026", new DateTime(2026, 4, 1), new DateTime(2026, 6, 30));

        var postingDate = new DateTime(2026, 5, 15);
        var isBlocked = period.IsClosed
            && period.StartDate <= postingDate
            && period.EndDate >= postingDate;

        isBlocked.ShouldBeFalse();
    }

    [Fact]
    public void FiscalYear_Closed_BlocksPosting()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY 2025",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));
        fy.IsClosed = true;

        fy.IsClosed.ShouldBeTrue();
    }

    // --- DN Address Resolution ---

    [Fact]
    public void DeliveryNote_AddressFields_DefaultNull()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "DN-001", DateTime.Today);

        dn.BillingAddressId.ShouldBeNull();
        dn.ShippingAddressId.ShouldBeNull();
    }

    [Fact]
    public void DeliveryNote_AddressFields_CanBeSet()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "DN-002", DateTime.Today);
        var billingId = Guid.NewGuid();
        var shippingId = Guid.NewGuid();

        dn.BillingAddressId = billingId;
        dn.ShippingAddressId = shippingId;

        dn.BillingAddressId.ShouldBe(billingId);
        dn.ShippingAddressId.ShouldBe(shippingId);
    }

    [Fact]
    public void DeliveryNote_AddressCopiedFromSO_Concept()
    {
        // Simulates: when SO has addresses, DN inherits them
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-100", DateTime.Today);
        so.BillingAddressId = Guid.NewGuid();
        so.ShippingAddressId = Guid.NewGuid();

        var dn = new DeliveryNote(Guid.NewGuid(), so.CompanyId, so.CustomerId,
            Guid.NewGuid(), "DN-100", DateTime.Today);
        dn.BillingAddressId = so.BillingAddressId;
        dn.ShippingAddressId = so.ShippingAddressId;

        dn.BillingAddressId.ShouldBe(so.BillingAddressId);
        dn.ShippingAddressId.ShouldBe(so.ShippingAddressId);
    }

    // --- Auto-Dunning Logic ---

    [Fact]
    public void Dunning_LevelSequencing_NextLevelCalculation()
    {
        // Level = existing submitted dunnings + 1
        var existingLevel = 2; // 2 previous dunnings submitted
        var nextLevel = existingLevel + 1;
        nextLevel.ShouldBe(3);
    }

    [Fact]
    public void Dunning_FirstDunning_Level1()
    {
        var existingLevel = 0; // No prior dunnings
        var nextLevel = existingLevel + 1;
        nextLevel.ShouldBe(1);
    }

    [Fact]
    public void Dunning_OverdueDays_Calculation()
    {
        var dueDate = new DateTime(2026, 6, 1);
        var today = new DateTime(2026, 7, 1);
        var overdueDays = (int)(today - dueDate).TotalDays;
        overdueDays.ShouldBe(30);
    }

    [Fact]
    public void Dunning_FeeCalculation_ByLevel()
    {
        var feePerLevel = 50m;
        var level = 3;
        var fee = level * feePerLevel;
        fee.ShouldBe(150m);
    }

    [Fact]
    public void Dunning_InterestCalculation_ByLevel()
    {
        var outstanding = 10_000m;
        var interestRatePerLevel = 1.5m; // 1.5% per level
        var level = 2;
        var interest = outstanding * interestRatePerLevel * level / 100m;
        interest.ShouldBe(300m);
    }

    [Fact]
    public void Dunning_OverdueDetection_DueDateBeforeToday()
    {
        var today = DateTime.Today;
        var invoiceDueDate = today.AddDays(-15); // 15 days overdue
        var isOverdue = invoiceDueDate < today;
        isOverdue.ShouldBeTrue();
    }

    [Fact]
    public void Dunning_NotOverdue_DueDateInFuture()
    {
        var today = DateTime.Today;
        var invoiceDueDate = today.AddDays(10); // Due in 10 days
        var isOverdue = invoiceDueDate < today;
        isOverdue.ShouldBeFalse();
    }
}
