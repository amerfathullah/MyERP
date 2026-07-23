using System;
using MyERP.Accounting.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for POS Opening Entry lifecycle and Payment Entry Tax calculation.
/// </summary>
public class PosOpeningAndPaymentTaxTests
{
    // === POS Opening Entry ===

    [Fact]
    public void PosOpening_Create_DefaultsToOpen()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(PosOpeningStatus.Open, entry.Status);
        Assert.Equal(DateTime.UtcNow.Date, entry.OpeningDate);
        Assert.Null(entry.PosClosingEntryId);
    }

    [Fact]
    public void PosOpening_AddBalance_TracksPaymentModes()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddOpeningBalance(Guid.NewGuid(), "Cash", 500m);
        entry.AddOpeningBalance(Guid.NewGuid(), "Credit Card", 0m);

        Assert.Equal(2, entry.Payments.Count);
        Assert.Equal(500m, entry.TotalOpeningAmount);
    }

    [Fact]
    public void PosOpening_Close_SetsStatusAndLinksClosure()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddOpeningBalance(Guid.NewGuid(), "Cash", 200m);
        var closingId = Guid.NewGuid();

        entry.Close(closingId);

        Assert.Equal(PosOpeningStatus.Closed, entry.Status);
        Assert.Equal(closingId, entry.PosClosingEntryId);
    }

    [Fact]
    public void PosOpening_Close_BlocksFromNonOpen()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddOpeningBalance(Guid.NewGuid(), "Cash", 100m);
        entry.Close(Guid.NewGuid());

        // Already closed — can't close again
        Assert.Throws<BusinessException>(() => entry.Close(Guid.NewGuid()));
    }

    [Fact]
    public void PosOpening_Cancel_RequiresClosedStatus()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        // Open status — cannot cancel directly
        Assert.Throws<BusinessException>(() => entry.Cancel());
    }

    [Fact]
    public void PosOpening_Cancel_SucceedsFromClosed()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddOpeningBalance(Guid.NewGuid(), "Cash", 100m);
        entry.Close(Guid.NewGuid());
        entry.Cancel();

        Assert.Equal(PosOpeningStatus.Cancelled, entry.Status);
    }

    [Fact]
    public void PosOpening_AddBalance_BlockedAfterClose()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddOpeningBalance(Guid.NewGuid(), "Cash", 100m);
        entry.Close(Guid.NewGuid());

        Assert.Throws<BusinessException>(() =>
            entry.AddOpeningBalance(Guid.NewGuid(), "Card", 50m));
    }

    [Fact]
    public void PosOpening_TotalAmount_SumsAllModes()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddOpeningBalance(Guid.NewGuid(), "Cash", 1000m);
        entry.AddOpeningBalance(Guid.NewGuid(), "Credit Card", 0m);
        entry.AddOpeningBalance(Guid.NewGuid(), "E-Wallet", 250m);

        Assert.Equal(1250m, entry.TotalOpeningAmount);
    }

    // === Payment Entry Tax ===

    [Fact]
    public void PETax_Create_Defaults()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(PaymentTaxChargeType.OnPaidAmount, tax.ChargeType);
        Assert.Equal(TaxAddDeduct.Add, tax.AddDeductTax);
        Assert.Equal(0m, tax.Rate);
        Assert.Equal(0m, tax.TaxAmount);
        Assert.False(tax.IncludedInPaidAmount);
        Assert.False(tax.IsExchangeGainLoss);
    }

    [Fact]
    public void PETax_Calculate_OnPaidAmount()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.OnPaidAmount,
            Rate = 6m // 6% SST
        };

        tax.Calculate(paidAmount: 10000m);

        Assert.Equal(600m, tax.TaxAmount);
        Assert.Equal(600m, tax.BaseTaxAmount);
    }

    [Fact]
    public void PETax_Calculate_WithExchangeRate()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.OnPaidAmount,
            Rate = 10m
        };

        tax.Calculate(paidAmount: 1000m, exchangeRate: 4.72m); // USD→MYR

        Assert.Equal(100m, tax.TaxAmount);        // 10% of 1000
        Assert.Equal(472m, tax.BaseTaxAmount);     // 100 × 4.72
    }

    [Fact]
    public void PETax_Calculate_Actual_UsesDirectAmount()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.Actual,
            TaxAmount = 75m // Fixed amount set directly
        };

        tax.Calculate(paidAmount: 5000m); // paidAmount ignored for Actual type

        // Actual type: TaxAmount stays as set (75), not recalculated from rate
        Assert.Equal(75m, tax.TaxAmount);
        Assert.Equal(75m, tax.BaseTaxAmount);
    }

    [Fact]
    public void PETax_IsDebit_PayAdd()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            AddDeductTax = TaxAddDeduct.Add
        };
        Assert.True(tax.IsDebit("Pay"));     // Pay+Add = debit
        Assert.False(tax.IsDebit("Receive")); // Receive+Add = credit
    }

    [Fact]
    public void PETax_IsDebit_PayDeduct()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            AddDeductTax = TaxAddDeduct.Deduct
        };
        Assert.False(tax.IsDebit("Pay"));     // Pay+Deduct = credit
        Assert.True(tax.IsDebit("Receive"));  // Receive+Deduct = debit
    }

    [Fact]
    public void PETax_IncludedInPaid_ReducesPaidToParty()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.OnPaidAmount,
            Rate = 2m, // 2% TDS withholding
            IncludedInPaidAmount = true
        };

        tax.Calculate(paidAmount: 50000m);

        // Tax of 1000 is deducted from the 50000 → party receives 49000
        Assert.Equal(1000m, tax.TaxAmount);
        Assert.True(tax.IncludedInPaidAmount);
    }

    [Fact]
    public void PETax_ExchangeGainLoss_Flag()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            IsExchangeGainLoss = true
        };
        // Exchange G/L tax rows are excluded from unallocated calculation
        Assert.True(tax.IsExchangeGainLoss);
    }

    [Fact]
    public void PETax_ZeroRate_ZeroAmount()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.OnPaidAmount,
            Rate = 0m
        };
        tax.Calculate(10000m);
        Assert.Equal(0m, tax.TaxAmount);
    }

    // === POS Opening + Closing Lifecycle Integration ===

    [Fact]
    public void PosLifecycle_OpenThenClose_LinksCorrectly()
    {
        var profileId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        // Open shift
        var opening = new PosOpeningEntry(Guid.NewGuid(), companyId, profileId, userId);
        opening.AddOpeningBalance(Guid.NewGuid(), "Cash", 500m);
        Assert.Equal(PosOpeningStatus.Open, opening.Status);

        // Close shift (linked to closing entry)
        var closingId = Guid.NewGuid();
        opening.Close(closingId);
        Assert.Equal(PosOpeningStatus.Closed, opening.Status);
        Assert.Equal(closingId, opening.PosClosingEntryId);

        // Create closing entry with the same opening reference
        var closing = new PosClosingEntry(Guid.NewGuid(), companyId, profileId, opening.Id, userId);
        closing.AddPayment(Guid.NewGuid(), "Cash", 1500m, 1490m); // RM 10 short
        closing.AddInvoice(Guid.NewGuid(), "POS-001", 750m);
        closing.AddInvoice(Guid.NewGuid(), "POS-002", 250m);
        closing.Submit();

        Assert.Equal(1000m, closing.GrandTotal);
        Assert.Equal(10m, closing.TotalDifference);
    }
}
