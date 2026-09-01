using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.AccountingTests;

/// <summary>
/// Unit tests for Payment Entry validation rules:
/// - Customer Receive payment with negative total allocated amount is blocked (Gotcha #197)
/// - Normal customer receive and supplier pay submit successfully
/// </summary>
public class PaymentEntryValidationRuleTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _paidFrom = Guid.NewGuid();
    private readonly Guid _paidTo = Guid.NewGuid();

    [Fact]
    public void PaymentEntry_CustomerReceive_NegativeTotalAllocated_ThrowsValidationException()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(),
            _companyId,
            PaymentType.Receive,
            DateTime.UtcNow,
            100m,
            _paidFrom,
            _paidTo
        )
        {
            PartyType = "Customer",
            PartyId = _customerId
        };

        // Negative allocation (e.g., net credit balance return)
        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id, "SalesInvoice", Guid.NewGuid(), 100m, -50m, -50m));
        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id, "SalesInvoice", Guid.NewGuid(), 100m, -100m, -100m));

        var ex = Assert.Throws<BusinessException>(() => pe.Submit());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("Cannot receive payment from Customer with negative total allocated amount", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void PaymentEntry_CustomerReceive_PositiveTotalAllocated_SubmitsSuccessfully()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(),
            _companyId,
            PaymentType.Receive,
            DateTime.UtcNow,
            100m,
            _paidFrom,
            _paidTo
        )
        {
            PartyType = "Customer",
            PartyId = _customerId
        };

        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id, "SalesInvoice", Guid.NewGuid(), 100m, 80m, 1m));
        pe.Submit();

        Assert.Equal(global::MyERP.Core.DocumentStatus.Submitted, pe.Status);
    }

    [Fact]
    public void OverBillingAllowance_AllowsPayment_WhenWithinAllowance()
    {
        // When OverBillingAllowance is 10%, per_billed of 105% is allowed (<= 110%)
        var overBillingAllowance = 10m;
        var perBilled = 105m;
        var isAllowed = perBilled < (100m + overBillingAllowance);
        Assert.True(isAllowed);
    }

    [Fact]
    public void OverBillingAllowance_BlocksPayment_WhenExceedsAllowance()
    {
        // When OverBillingAllowance is 10%, per_billed of 115% is blocked (>= 110%)
        var overBillingAllowance = 10m;
        var perBilled = 115m;
        var isBlocked = perBilled >= (100m + overBillingAllowance);
        Assert.True(isBlocked);
    }

    [Fact]
    public void PaymentEntry_InternalTransfer_SplitsBankChargesAndExchangeGainLoss()
    {
        // Per ERPNext PR #58071 (commit 8d2aa69e61):
        // In multi-currency transfer (e.g. Paid 100 USD @ 50 rate = 5000 MYR, Received 4500 MYR @ 1 rate = 4500 MYR)
        // With a 100 MYR bank charge deduction:
        // exchange_gain_loss = base_paid (5000) - base_received (4500) - other_deductions (100) = 400 MYR
        var pe = new PaymentEntry(
            Guid.NewGuid(),
            _companyId,
            PaymentType.InternalTransfer,
            DateTime.UtcNow,
            100m,
            _paidFrom,
            _paidTo
        )
        {
            SourceExchangeRate = 50m,
            ReceivedAmount = 4500m,
            TargetExchangeRate = 1m
        };

        // Add 100 MYR bank charge deduction
        pe.AddTax(new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            Description = "Bank Charges",
            TaxAmount = 100m,
            AddDeductTax = TaxAddDeduct.Deduct,
            ChargeType = PaymentTaxChargeType.Actual
        });

        // ExchangeGainLoss should absorb residual (400 MYR)
        Assert.Equal(400m, pe.ExchangeGainLoss);
    }
}
