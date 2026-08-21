using System;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Unit tests for Bank Guarantee party XOR and alignment validation.
/// Verifies rules migrated from erpnext/accounts/doctype/bank_guarantee (Gotcha #4161).
/// </summary>
public class BankGuaranteePartyTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();

    [Fact]
    public void BankGuarantee_BothCustomerAndSupplier_ThrowsValidationException()
    {
        var ex = Assert.Throws<BusinessException>(() => new BankGuarantee(
            Guid.NewGuid(),
            _companyId,
            BankGuaranteeType.Receiving,
            50000m,
            DateTime.UtcNow,
            365,
            customerId: _customerId,
            supplierId: _supplierId));

        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("both Customer and Supplier", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void BankGuarantee_ReceivingWithSupplier_ThrowsValidationException()
    {
        var ex = Assert.Throws<BusinessException>(() => new BankGuarantee(
            Guid.NewGuid(),
            _companyId,
            BankGuaranteeType.Receiving,
            50000m,
            DateTime.UtcNow,
            365,
            supplierId: _supplierId));

        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("must be linked to a Customer", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void BankGuarantee_ProvidingWithCustomer_ThrowsValidationException()
    {
        var ex = Assert.Throws<BusinessException>(() => new BankGuarantee(
            Guid.NewGuid(),
            _companyId,
            BankGuaranteeType.Providing,
            50000m,
            DateTime.UtcNow,
            365,
            customerId: _customerId));

        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("must be linked to a Supplier", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void BankGuarantee_ValidReceivingWithCustomer_Succeeds()
    {
        var bg = new BankGuarantee(
            Guid.NewGuid(),
            _companyId,
            BankGuaranteeType.Receiving,
            50000m,
            DateTime.UtcNow,
            365,
            customerId: _customerId);

        Assert.Equal(BankGuaranteeType.Receiving, bg.BgType);
        Assert.Equal(_customerId, bg.CustomerId);
        Assert.Null(bg.SupplierId);
    }

    [Fact]
    public void BankGuarantee_ValidProvidingWithSupplier_Succeeds()
    {
        var bg = new BankGuarantee(
            Guid.NewGuid(),
            _companyId,
            BankGuaranteeType.Providing,
            50000m,
            DateTime.UtcNow,
            365,
            supplierId: _supplierId);

        Assert.Equal(BankGuaranteeType.Providing, bg.BgType);
        Assert.Equal(_supplierId, bg.SupplierId);
        Assert.Null(bg.CustomerId);
    }
}
