using System;
using System.Collections.Generic;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Core;

/// <summary>
/// Unit tests for PartyValidationService (Gotchas #1004, #1130).
/// </summary>
public class PartyValidationServiceTests
{
    private readonly PartyValidationService _service = new();

    [Fact]
    public void ValidatePartyStatus_CustomerDisabled_ThrowsPartyDisabled()
    {
        var ex = Assert.Throws<BusinessException>(() =>
            _service.ValidatePartyStatus("Customer", isFrozen: false, isDisabled: true, "Acme Corp"));

        Assert.Equal(MyERPDomainErrorCodes.PartyDisabled, ex.Code);
    }

    [Fact]
    public void ValidatePartyStatus_CustomerFrozen_ThrowsPartyFrozen()
    {
        var ex = Assert.Throws<BusinessException>(() =>
            _service.ValidatePartyStatus("Customer", isFrozen: true, isDisabled: false, "Acme Corp"));

        Assert.Equal(MyERPDomainErrorCodes.PartyFrozen, ex.Code);
    }

    [Fact]
    public void ValidatePartyStatus_EmployeeDisabled_DoesNotThrow()
    {
        // Gotcha #1130: Employee disabled check is WARNING only, not hard throw
        _service.ValidatePartyStatus("Employee", isFrozen: false, isDisabled: true, "John Doe");
    }

    [Fact]
    public void ValidatePartyAccounts_ItemAccountMatchesPartyAccount_ThrowsException()
    {
        var partyAccountId = Guid.NewGuid();
        var itemAccounts = new List<Guid> { Guid.NewGuid(), partyAccountId, Guid.NewGuid() };

        var ex = Assert.Throws<BusinessException>(() =>
            _service.ValidatePartyAccounts(partyAccountId, itemAccounts));

        Assert.Equal(MyERPDomainErrorCodes.ItemAccountCannotBePartyAccount, ex.Code);
    }

    [Fact]
    public void ValidatePartyAccounts_ItemAccountsDistinct_Passes()
    {
        var partyAccountId = Guid.NewGuid();
        var itemAccounts = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        _service.ValidatePartyAccounts(partyAccountId, itemAccounts);
    }

    [Fact]
    public void ValidatePartyAccountCurrency_3WayMatch_ValidCurrencies_Passes()
    {
        // Party billing currency = USD, company currency = MYR
        // Party account is USD, advance account is USD -> Valid
        _service.ValidatePartyAccountCurrency("USD", "USD", "USD", "MYR");

        // Party account is MYR, advance account is MYR -> Valid
        _service.ValidatePartyAccountCurrency("MYR", "MYR", "USD", "MYR");
    }

    [Fact]
    public void ValidatePartyAccountCurrency_PartyAccountMismatch_ThrowsException()
    {
        // Party billing currency = USD, company = MYR, party account = EUR
        var ex = Assert.Throws<BusinessException>(() =>
            _service.ValidatePartyAccountCurrency("EUR", "USD", "USD", "MYR"));

        Assert.Equal(MyERPDomainErrorCodes.PartyAccountCurrencyMismatch, ex.Code);
    }

    [Fact]
    public void ValidatePartyAccountCurrency_AdvanceAccountMismatchWithPartyAccount_ThrowsException()
    {
        // Party account is USD, advance account is MYR (both valid individually, but not equal)
        var ex = Assert.Throws<BusinessException>(() =>
            _service.ValidatePartyAccountCurrency("USD", "MYR", "USD", "MYR"));

        Assert.Equal(MyERPDomainErrorCodes.PartyAccountCurrencyMismatch, ex.Code);
    }

    [Fact]
    public void ValidatePartyAddress_BelongsToDifferentParty_ThrowsException()
    {
        var partyId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();
        var address = new Address(Guid.NewGuid(), "Billing", "Customer", otherPartyId, "123 Main St", "Malaysia");

        var ex = Assert.Throws<BusinessException>(() =>
            _service.ValidatePartyAddress(address, "Customer", partyId));

        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public void ValidateCompanyAddress_BelongsToDifferentCompany_ThrowsException()
    {
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var address = new Address(Guid.NewGuid(), "Office", "Company", otherCompanyId, "456 Corporate Ave", "Malaysia");

        var ex = Assert.Throws<BusinessException>(() =>
            _service.ValidateCompanyAddress(address, companyId));

        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }
}
