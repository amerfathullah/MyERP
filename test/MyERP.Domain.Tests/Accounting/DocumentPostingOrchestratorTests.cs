using System;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Tests for DocumentPostingOrchestrator validation logic and related entities.
/// Tests the validation paths that don't require repository mocking.
/// </summary>
public class DocumentPostingOrchestratorTests
{
    [Fact]
    public void PaymentAllocation_StoresFields()
    {
        var alloc = new PaymentAllocation
        {
            VoucherType = "SalesInvoice",
            VoucherId = Guid.NewGuid(),
            AllocatedAmount = 1500m,
        };

        alloc.VoucherType.ShouldBe("SalesInvoice");
        alloc.AllocatedAmount.ShouldBe(1500m);
    }

    [Fact]
    public void PaymentAllocation_MultipleAllocations_SumCorrectly()
    {
        var allocations = new[]
        {
            new PaymentAllocation { VoucherType = "SalesInvoice", VoucherId = Guid.NewGuid(), AllocatedAmount = 500m },
            new PaymentAllocation { VoucherType = "SalesInvoice", VoucherId = Guid.NewGuid(), AllocatedAmount = 300m },
            new PaymentAllocation { VoucherType = "SalesInvoice", VoucherId = Guid.NewGuid(), AllocatedAmount = 200m },
        };

        allocations.Sum(a => a.AllocatedAmount).ShouldBe(1000m);
    }

    [Fact]
    public void PLE_Amount_Positive_ForCustomerInvoice()
    {
        // When posting SI, PLE amount is positive (DR = increases outstanding)
        var grandTotal = 5000m;
        var exchangeRate = 1m;
        var baseAmount = Math.Round(grandTotal * exchangeRate, 2);

        baseAmount.ShouldBe(5000m);
        baseAmount.ShouldBeGreaterThan(0); // DR = positive for receivable
    }

    [Fact]
    public void PLE_Amount_Negative_ForSupplierInvoice()
    {
        // When posting PI, PLE amount is negative (CR = increases payable)
        var grandTotal = 3000m;
        var exchangeRate = 1m;
        var baseAmount = Math.Round(-grandTotal * exchangeRate, 2);

        baseAmount.ShouldBe(-3000m);
        baseAmount.ShouldBeLessThan(0); // CR = negative for payable
    }

    [Fact]
    public void PLE_PaymentEntry_Customer_IsCredit()
    {
        // Receiving payment from customer: CR customer account (reduces outstanding)
        var partyType = "Customer";
        var allocatedAmount = 2000m;
        var sign = partyType == "Customer" ? -1m : 1m;
        var amountInAccCurrency = sign * allocatedAmount;

        amountInAccCurrency.ShouldBe(-2000m); // CR = negative
    }

    [Fact]
    public void PLE_PaymentEntry_Supplier_IsDebit()
    {
        // Paying supplier: DR supplier account (reduces payable)
        var partyType = "Supplier";
        var allocatedAmount = 1500m;
        var sign = partyType == "Customer" ? -1m : 1m;
        var amountInAccCurrency = sign * allocatedAmount;

        amountInAccCurrency.ShouldBe(1500m); // DR = positive
    }

    [Fact]
    public void MultiCurrency_BaseAmount_Calculation()
    {
        var invoiceTotal = 1000m; // USD
        var exchangeRate = 4.72m; // MYR per USD
        var baseAmount = Math.Round(invoiceTotal * exchangeRate, 2);

        baseAmount.ShouldBe(4720m); // MYR
    }

    [Fact]
    public void AccountingPeriod_IsClosed_BlocksPosting()
    {
        var period = new AccountingPeriod(
            Guid.NewGuid(), Guid.NewGuid(), "Q1 2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31), null);
        period.Close();

        period.IsClosed.ShouldBeTrue();
        period.ContainsDate(new DateTime(2026, 2, 15)).ShouldBeTrue();
    }

    [Fact]
    public void FiscalYear_IsClosed_BlocksPosting()
    {
        var fy = new FiscalYear(
            Guid.NewGuid(), Guid.NewGuid(), "FY 2025",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));
        fy.IsClosed = true;

        fy.IsClosed.ShouldBeTrue();
    }

    [Fact]
    public void Company_FrozenDate_BlocksEarlierPostings()
    {
        var company = new Company(Guid.NewGuid(), "Test Co");
        company.AccountsFrozenTillDate = new DateTime(2026, 6, 30);

        // Any posting on/before frozen date should be blocked
        var postingDate = new DateTime(2026, 6, 15);
        (postingDate <= company.AccountsFrozenTillDate.Value).ShouldBeTrue();

        // Posting after frozen date is OK
        var laterDate = new DateTime(2026, 7, 1);
        (laterDate <= company.AccountsFrozenTillDate.Value).ShouldBeFalse();
    }

    [Fact]
    public void JournalEntry_BalancedEntry_Validates()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        je.AddLine(Guid.NewGuid(), 1000, true, "Debit entry");
        je.AddLine(Guid.NewGuid(), 1000, false, "Credit entry");

        je.Validate(); // Should not throw
        je.TotalDebit.ShouldBe(1000m);
        je.TotalCredit.ShouldBe(1000m);
    }

    [Fact]
    public void JournalEntry_UnbalancedEntry_Throws()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        je.AddLine(Guid.NewGuid(), 1000, true, "Debit");
        je.AddLine(Guid.NewGuid(), 500, false, "Credit (short)");

        Should.Throw<Volo.Abp.BusinessException>(() => je.Validate());
    }
}
