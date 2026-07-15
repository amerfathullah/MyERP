using System;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Accounting;

public class ReconciliationExchangeGainLossTests
{
    #region Exchange Gain/Loss Calculation

    [Fact]
    public void CalculateExchangeGainLoss_SameRate_ReturnsZero()
    {
        var result = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            allocatedAmount: 1000m,
            paymentExchangeRate: 4.72m,
            invoiceExchangeRate: 4.72m);

        result.ShouldBe(0m);
    }

    [Fact]
    public void CalculateExchangeGainLoss_Gain_PositiveResult()
    {
        // Payment rate higher than invoice rate = gain for receivable
        // Customer owed at 4.50, paid at 4.72 → company gains
        var result = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            allocatedAmount: 1000m,
            paymentExchangeRate: 4.72m,
            invoiceExchangeRate: 4.50m);

        result.ShouldBe(220m); // 1000 × (4.72 - 4.50) = 220
    }

    [Fact]
    public void CalculateExchangeGainLoss_Loss_NegativeResult()
    {
        // Payment rate lower than invoice rate = loss
        // Customer owed at 4.72, paid at 4.50 → company loses
        var result = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            allocatedAmount: 1000m,
            paymentExchangeRate: 4.50m,
            invoiceExchangeRate: 4.72m);

        result.ShouldBe(-220m); // 1000 × (4.50 - 4.72) = -220
    }

    [Fact]
    public void CalculateExchangeGainLoss_SmallDifference_Rounded()
    {
        var result = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            allocatedAmount: 500m,
            paymentExchangeRate: 4.7215m,
            invoiceExchangeRate: 4.7200m);

        // 500 × (4.7215 - 4.72) = 500 × 0.0015 = 0.75
        result.ShouldBe(0.75m);
    }

    [Fact]
    public void CalculateExchangeGainLoss_BaseCurrency_AlwaysZero()
    {
        // Both rates are 1.0 (MYR→MYR) → no gain/loss
        var result = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            allocatedAmount: 5000m,
            paymentExchangeRate: 1m,
            invoiceExchangeRate: 1m);

        result.ShouldBe(0m);
    }

    [Fact]
    public void CalculateExchangeGainLoss_LargeAmount_Precise()
    {
        // Large invoice: USD 100,000 with rate difference of 0.05
        var result = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            allocatedAmount: 100_000m,
            paymentExchangeRate: 4.75m,
            invoiceExchangeRate: 4.70m);

        result.ShouldBe(5000m); // 100,000 × 0.05 = 5000
    }

    #endregion

    #region Reconciliation Result Model

    [Fact]
    public void ReconciliationResult_Default_ZeroCounts()
    {
        var result = new ReconciliationResult();

        result.ReconciledCount.ShouldBe(0);
        result.TotalAllocated.ShouldBe(0m);
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void ReconciliationResult_TracksSuccessAndErrors()
    {
        var result = new ReconciliationResult
        {
            ReconciledCount = 3,
            TotalAllocated = 15000m,
        };
        result.Errors.Add(new ReconciliationError
        {
            InvoiceVoucherId = Guid.NewGuid(),
            Message = "Outstanding changed",
        });

        result.ReconciledCount.ShouldBe(3);
        result.TotalAllocated.ShouldBe(15000m);
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void ReconciliationAllocation_CarriesAllFields()
    {
        var paymentId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        var alloc = new ReconciliationAllocation
        {
            PaymentVoucherType = "PaymentEntry",
            PaymentVoucherId = paymentId,
            InvoiceVoucherType = "SalesInvoice",
            InvoiceVoucherId = invoiceId,
            AllocatedAmount = 5000m,
        };

        alloc.PaymentVoucherType.ShouldBe("PaymentEntry");
        alloc.PaymentVoucherId.ShouldBe(paymentId);
        alloc.InvoiceVoucherType.ShouldBe("SalesInvoice");
        alloc.InvoiceVoucherId.ShouldBe(invoiceId);
        alloc.AllocatedAmount.ShouldBe(5000m);
    }

    #endregion

    #region Unreconciled Payment Model

    [Fact]
    public void UnreconciledPayment_FullyUnallocated()
    {
        var payment = new UnreconciledPayment
        {
            VoucherType = "PaymentEntry",
            VoucherId = Guid.NewGuid(),
            TotalAmount = 10000m,
            UnallocatedAmount = 10000m, // Nothing allocated yet
        };

        payment.UnallocatedAmount.ShouldBe(payment.TotalAmount);
    }

    [Fact]
    public void UnreconciledPayment_PartiallyAllocated()
    {
        var payment = new UnreconciledPayment
        {
            VoucherType = "PaymentEntry",
            VoucherId = Guid.NewGuid(),
            TotalAmount = 10000m,
            UnallocatedAmount = 3000m, // 7000 already allocated
        };

        payment.UnallocatedAmount.ShouldBeLessThan(payment.TotalAmount);
        (payment.TotalAmount - payment.UnallocatedAmount).ShouldBe(7000m);
    }

    #endregion

    #region Exchange Gain/Loss JE Integration

    [Fact]
    public void ExchangeGainLoss_JE_IsBalanced()
    {
        // An exchange gain/loss JE must always be double-entry balanced
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        var exchangeAccountId = Guid.NewGuid();

        // Gain of RM 220 → DR Exchange, CR Exchange (simplified)
        je.AddLine(exchangeAccountId, 220m, true);
        je.AddLine(exchangeAccountId, 220m, false);

        je.Validate(); // Should not throw
        je.TotalDebit.ShouldBe(je.TotalCredit);
    }

    [Fact]
    public void ExchangeGainLoss_JE_HasReconciliationReference()
    {
        var paymentId = Guid.NewGuid();
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        je.ReferenceType = "PaymentReconciliation";
        je.ReferenceId = paymentId;

        je.ReferenceType.ShouldBe("PaymentReconciliation");
        je.ReferenceId.ShouldBe(paymentId);
    }

    [Fact]
    public void ExchangeGainLoss_JE_CancelledOnUnreconcile()
    {
        // Simulates: when unreconciling, related JE should be cancelled
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        var accId = Guid.NewGuid();
        je.AddLine(accId, 100m, true);
        je.AddLine(accId, 100m, false);
        je.Validate();
        je.Post();

        je.Status.ShouldBe(DocumentStatus.Posted);

        // On unreconcile, the JE is cancelled
        je.Cancel();
        je.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void ExchangeGainLoss_BelowThreshold_NoJE()
    {
        // If gain/loss < 0.01, no JE should be created (immaterial)
        var result = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            allocatedAmount: 100m,
            paymentExchangeRate: 4.72001m,
            invoiceExchangeRate: 4.72m);

        // 100 × 0.00001 = 0.001 → rounds to 0.00 → immaterial
        Math.Abs(result).ShouldBeLessThan(0.01m);
    }

    #endregion

    #region PaymentEntry Exchange Rate Fields

    [Fact]
    public void PaymentEntry_ExchangeRate_DefaultsToOne()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.Today, 5000m, Guid.NewGuid(), Guid.NewGuid());

        pe.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void PaymentEntry_SourceExchangeRate_ForComparison()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.Today, 5000m, Guid.NewGuid(), Guid.NewGuid());

        pe.SourceExchangeRate.ShouldBe(1m); // Default
        pe.ExchangeRate = 4.72m;
        pe.SourceExchangeRate = 4.50m;

        // gain_loss = 5000 × (4.72 - 4.50) = 1100
        pe.ExchangeGainLoss.ShouldBe(5000m * (4.72m - 4.50m));
    }

    [Fact]
    public void PaymentEntry_SameCurrency_NoGainLoss()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Pay,
            DateTime.Today, 3000m, Guid.NewGuid(), Guid.NewGuid());

        // Both rates are 1 → MYR payment for MYR invoice
        pe.ExchangeRate = 1m;
        pe.SourceExchangeRate = 1m;

        pe.ExchangeGainLoss.ShouldBe(0m);
    }

    #endregion

    #region Company ExchangeGainLossAccountId

    [Fact]
    public void Company_ExchangeGainLossAccountId_Nullable()
    {
        var company = new Company(Guid.NewGuid(), "Test Co");

        company.ExchangeGainLossAccountId.ShouldBeNull(); // Not set = no gain/loss JE
    }

    [Fact]
    public void Company_ExchangeGainLossAccountId_EnablesJeCreation()
    {
        var company = new Company(Guid.NewGuid(), "Test Co");
        var accountId = Guid.NewGuid();
        company.ExchangeGainLossAccountId = accountId;

        company.ExchangeGainLossAccountId.ShouldBe(accountId);
    }

    #endregion
}
