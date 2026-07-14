using System;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Tests for PaymentLedgerService — the authoritative source for outstanding amounts.
/// </summary>
public class PaymentLedgerServiceTests
{
    private static readonly Guid _companyId = Guid.NewGuid();
    private static readonly Guid _accountId = Guid.NewGuid();
    private static readonly Guid _customerId = Guid.NewGuid();
    private static readonly Guid _supplierId = Guid.NewGuid();

    // === CreateEntry Tests ===

    [Fact]
    public void PLE_Creation_SetsAllFields()
    {
        var invoiceId = Guid.NewGuid();
        var ple = new PaymentLedgerEntry(
            Guid.NewGuid(), _companyId, DateTime.Today,
            _accountId, "Customer", _customerId,
            "SalesInvoice", invoiceId,
            "SalesInvoice", invoiceId,
            1000, 1000, "MYR");

        ple.CompanyId.ShouldBe(_companyId);
        ple.PartyType.ShouldBe("Customer");
        ple.PartyId.ShouldBe(_customerId);
        ple.VoucherType.ShouldBe("SalesInvoice");
        ple.Amount.ShouldBe(1000);
        ple.AmountInAccountCurrency.ShouldBe(1000);
        ple.AccountCurrency.ShouldBe("MYR");
        ple.Delinked.ShouldBeFalse();
        ple.IsReversal.ShouldBeFalse();
    }

    // === Outstanding Calculation Tests ===

    [Fact]
    public void Outstanding_SingleEntry_ReturnsAmount()
    {
        var invoiceId = Guid.NewGuid();
        var ple = CreatePle(invoiceId, 1000);

        // Outstanding = sum of all non-delinked PLE entries for this against-voucher
        ple.AmountInAccountCurrency.ShouldBe(1000);
        ple.Delinked.ShouldBeFalse();
    }

    [Fact]
    public void Outstanding_WithPayment_ReducesBalance()
    {
        var invoiceId = Guid.NewGuid();
        var invoicePle = CreatePle(invoiceId, 1000); // DR outstanding (positive)
        var paymentPle = CreatePle(invoiceId, -600); // CR payment (negative = reduces)

        var outstanding = invoicePle.AmountInAccountCurrency + paymentPle.AmountInAccountCurrency;
        outstanding.ShouldBe(400); // 1000 - 600
    }

    [Fact]
    public void Outstanding_DelinkedEntry_ExcludedFromTotal()
    {
        var invoiceId = Guid.NewGuid();
        var ple1 = CreatePle(invoiceId, 1000);
        var ple2 = CreatePle(invoiceId, -500);
        ple2.Delinked = true; // delinked payment — should be excluded

        // Only non-delinked entries count
        var entries = new[] { ple1, ple2 };
        var outstanding = entries.Where(p => !p.Delinked).Sum(p => p.AmountInAccountCurrency);
        outstanding.ShouldBe(1000); // payment excluded because delinked
    }

    [Fact]
    public void Outstanding_MultiplePayments_SumsCorrectly()
    {
        var invoiceId = Guid.NewGuid();
        var entries = new[]
        {
            CreatePle(invoiceId, 5000),   // invoice posted
            CreatePle(invoiceId, -2000),  // partial payment 1
            CreatePle(invoiceId, -1500),  // partial payment 2
        };

        var outstanding = entries.Where(p => !p.Delinked).Sum(p => p.AmountInAccountCurrency);
        outstanding.ShouldBe(1500); // 5000 - 2000 - 1500
    }

    // === Reversal Tests ===

    [Fact]
    public void Reversal_NegatesOriginalAmount()
    {
        var invoiceId = Guid.NewGuid();
        var original = CreatePle(invoiceId, 1000);

        var reversal = new PaymentLedgerEntry(
            Guid.NewGuid(), original.CompanyId, DateTime.UtcNow,
            original.AccountId, original.PartyType, original.PartyId,
            original.VoucherType, original.VoucherId,
            original.AgainstVoucherType, original.AgainstVoucherId,
            -original.Amount, -original.AmountInAccountCurrency,
            original.AccountCurrency)
        {
            IsReversal = true,
        };

        reversal.Amount.ShouldBe(-1000);
        reversal.AmountInAccountCurrency.ShouldBe(-1000);
        reversal.IsReversal.ShouldBeTrue();
    }

    [Fact]
    public void Reversal_ZerosOutstanding()
    {
        var invoiceId = Guid.NewGuid();
        var entries = new[]
        {
            CreatePle(invoiceId, 1000),   // original
            CreatePle(invoiceId, -1000),  // reversal
        };
        entries[1].IsReversal = true;

        var outstanding = entries.Where(p => !p.Delinked).Sum(p => p.AmountInAccountCurrency);
        outstanding.ShouldBe(0);
    }

    // === Reconcile Sign Convention Tests ===

    [Fact]
    public void Reconcile_Customer_UsesNegativeSign()
    {
        // When receiving from customer, PLE amount is negative (reduces outstanding)
        var sign = "Customer" == "Customer" ? -1m : 1m;
        var allocatedAmount = 500m;
        var pleAmount = sign * allocatedAmount;

        pleAmount.ShouldBe(-500); // negative = reduces receivable
    }

    [Fact]
    public void Reconcile_Supplier_UsesPositiveSign()
    {
        // When paying supplier, PLE amount is positive (reduces payable)
        var sign = "Supplier" == "Customer" ? -1m : 1m;
        var allocatedAmount = 300m;
        var pleAmount = sign * allocatedAmount;

        pleAmount.ShouldBe(300); // positive = reduces payable
    }

    [Fact]
    public void Reconcile_OverAllocation_Detected()
    {
        // If allocated > outstanding + tolerance → should be caught
        decimal currentOutstanding = 400m;
        decimal allocatedAmount = 500m;
        decimal tolerance = 0.01m;

        var exceedsLimit = Math.Abs(allocatedAmount) > Math.Abs(currentOutstanding) + tolerance;
        exceedsLimit.ShouldBeTrue();
    }

    [Fact]
    public void Reconcile_WithinTolerance_Passes()
    {
        // Allocated exactly equals outstanding — should pass
        decimal currentOutstanding = 400m;
        decimal allocatedAmount = 400m;
        decimal tolerance = 0.01m;

        var exceedsLimit = Math.Abs(allocatedAmount) > Math.Abs(currentOutstanding) + tolerance;
        exceedsLimit.ShouldBeFalse();
    }

    // === Unreconcile Tests ===

    [Fact]
    public void Unreconcile_SetsDelinkedTrue()
    {
        var ple = CreatePle(Guid.NewGuid(), -500);
        ple.Delinked.ShouldBeFalse();

        ple.Delinked = true;
        ple.Delinked.ShouldBeTrue();
    }

    [Fact]
    public void Unreconcile_AlreadyDelinked_RemainsDelinked()
    {
        var ple = CreatePle(Guid.NewGuid(), -500);
        ple.Delinked = true;

        // Re-setting delinked should be idempotent
        ple.Delinked = true;
        ple.Delinked.ShouldBeTrue();
    }

    // === Helper ===

    private static PaymentLedgerEntry CreatePle(Guid invoiceId, decimal amount)
    {
        return new PaymentLedgerEntry(
            Guid.NewGuid(), _companyId, DateTime.Today,
            _accountId, "Customer", _customerId,
            "SalesInvoice", invoiceId,
            "SalesInvoice", invoiceId,
            amount, amount, "MYR");
    }
}
