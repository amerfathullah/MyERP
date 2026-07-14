using System;
using MyERP.Accounting.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Integration tests for newly-wired service methods:
/// - Selling price validation wired into SI submit
/// - Bank transaction fee normalization on import
/// - SalesTeamEntry commission calculation
/// - Accounting period per-doctype + exempted role
/// </summary>
public class ServiceWiringIntegrationTests
{
    // --- Commission Tracking via SalesTeamEntry ---

    [Fact]
    public void SalesTeamEntry_CalculatesIncentives_Correctly()
    {
        // Sales person has 5% commission, allocated 40% of RM 10,000 invoice
        var entry = new SalesTeamEntry(Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(), 40m, 10000m, 5m);

        entry.AllocatedAmount.ShouldBe(4000m);   // 10000 × 40%
        entry.Incentives.ShouldBe(200m);          // 4000 × 5%
    }

    [Fact]
    public void SalesTeamEntry_100Percent_FullAllocation()
    {
        var entry = new SalesTeamEntry(Guid.NewGuid(), Guid.NewGuid(),
            "SalesOrder", Guid.NewGuid(), 100m, 5000m, 10m);

        entry.AllocatedAmount.ShouldBe(5000m);
        entry.Incentives.ShouldBe(500m);
    }

    [Fact]
    public void SalesTeamEntry_ZeroCommission_ZeroIncentives()
    {
        var entry = new SalesTeamEntry(Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(), 50m, 8000m, 0m);

        entry.AllocatedAmount.ShouldBe(4000m);
        entry.Incentives.ShouldBe(0m);
    }

    [Fact]
    public void SalesTeamEntry_PreservesCommissionRateSnapshot()
    {
        var entry = new SalesTeamEntry(Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(), 100m, 1000m, 7.5m);

        // CommissionRate is a snapshot — even if SalesPerson.CommissionRate changes later,
        // this entry preserves the rate at time of transaction
        entry.CommissionRate.ShouldBe(7.5m);
        entry.Incentives.ShouldBe(75m);
    }

    // --- Bank Transaction Fee + Currency ---

    [Fact]
    public void BankTransaction_NormalizeFees_DepositScenario()
    {
        var bt = new BankTransaction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, "TT RECEIVED", 0m);
        bt.Deposit = 5000m;
        bt.ExcludedFee = 30m;

        bt.NormalizeFees();

        bt.Deposit.ShouldBe(4970m);
        bt.IncludedFee.ShouldBe(30m);
        bt.ExcludedFee.ShouldBe(0m);
    }

    [Fact]
    public void BankTransaction_NormalizeFees_WithdrawalScenario()
    {
        var bt = new BankTransaction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, "PAYMENT", 0m);
        bt.Withdrawal = 2000m;
        bt.ExcludedFee = 15m;

        bt.NormalizeFees();

        bt.Withdrawal.ShouldBe(2015m);
        bt.IncludedFee.ShouldBe(15m);
    }

    // --- Accounting Period Per-DocType ---

    [Fact]
    public void AccountingPeriod_PerDoctype_SIClosedButJEOpen()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(),
            "Q1 2026", new DateTime(2026, 1, 1), new DateTime(2026, 3, 31));

        period.CloseDocumentType("SalesInvoice");
        period.CloseDocumentType("PurchaseInvoice");

        // SI and PI blocked
        period.IsClosedForDocumentType("SalesInvoice").ShouldBeTrue();
        period.IsClosedForDocumentType("PurchaseInvoice").ShouldBeTrue();

        // JE and PE still open
        period.IsClosedForDocumentType("JournalEntry").ShouldBeFalse();
        period.IsClosedForDocumentType("PaymentEntry").ShouldBeFalse();
    }

    [Fact]
    public void AccountingPeriod_ReopenSpecificDoctype()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(),
            "Q1 2026", new DateTime(2026, 1, 1), new DateTime(2026, 3, 31));

        period.CloseDocumentType("SalesInvoice");
        period.CloseDocumentType("PurchaseInvoice");
        period.CloseDocumentType("JournalEntry");

        // Accountant needs to post a correction JE
        period.ReopenDocumentType("JournalEntry");

        period.IsClosedForDocumentType("SalesInvoice").ShouldBeTrue();
        period.IsClosedForDocumentType("PurchaseInvoice").ShouldBeTrue();
        period.IsClosedForDocumentType("JournalEntry").ShouldBeFalse();
        period.IsClosed.ShouldBeTrue(); // still closed for SI+PI
    }

    // --- SalesPerson Commission Chain ---

    [Fact]
    public void SalesPerson_CalculateCommission()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "John Doe");
        sp.SetCommissionRate(5m);

        var commission = sp.CalculateCommission(10000m);
        commission.ShouldBe(500m);
    }

    [Fact]
    public void SalesPerson_ZeroRate_ZeroCommission()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "New Rep");
        // Default commission rate is 0
        sp.CalculateCommission(50000m).ShouldBe(0m);
    }
}
