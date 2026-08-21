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
}
