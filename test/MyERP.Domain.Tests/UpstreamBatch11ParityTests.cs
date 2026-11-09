using System;
using MyERP.Core.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 11 (PR #64936, commits ca02eb09c5, 9ecfac159f):
/// - ca02eb09c5: Filter party default accounts by billing currency
/// - 9ecfac159f: Offer only accounts the server accepts for the party's billing currency
/// - Customer.DefaultCurrency property support
/// </summary>
public class UpstreamBatch11ParityTests
{
    private readonly PartyValidationService _partyValidationService = new();

    [Fact]
    public void PartyValidationService_ValidatePartyAccountCurrency_WithoutPartyBillingCurrency_AcceptsCompanyCurrency()
    {
        // When customer/supplier has no custom billing currency set, account currency must match company currency.
        _partyValidationService.ValidatePartyAccountCurrency(
            partyAccountCurrency: "MYR",
            advanceAccountCurrency: null,
            partyBillingCurrency: null,
            companyCurrency: "MYR");
    }

    [Fact]
    public void PartyValidationService_ValidatePartyAccountCurrency_WithoutPartyBillingCurrency_RejectsForeignCurrency()
    {
        // When customer/supplier has no custom billing currency, foreign account currency throws PartyAccountCurrencyMismatch
        var ex = Should.Throw<BusinessException>(() =>
            _partyValidationService.ValidatePartyAccountCurrency(
                partyAccountCurrency: "USD",
                advanceAccountCurrency: null,
                partyBillingCurrency: null,
                companyCurrency: "MYR"));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.PartyAccountCurrencyMismatch);
        ex.Data["partyCurrency"]?.ToString().ShouldBe("MYR");
        ex.Data["accountCurrency"]?.ToString().ShouldBe("USD");
    }

    [Fact]
    public void PartyValidationService_ValidatePartyAccountCurrency_WithPartyBillingCurrency_AcceptsBillingCurrencyOrCompanyCurrency()
    {
        // With party billing currency = USD and company = MYR:
        // Account in USD passes:
        _partyValidationService.ValidatePartyAccountCurrency(
            partyAccountCurrency: "USD",
            advanceAccountCurrency: null,
            partyBillingCurrency: "USD",
            companyCurrency: "MYR");

        // Account in company currency (MYR) also passes:
        _partyValidationService.ValidatePartyAccountCurrency(
            partyAccountCurrency: "MYR",
            advanceAccountCurrency: null,
            partyBillingCurrency: "USD",
            companyCurrency: "MYR");
    }

    [Fact]
    public void PartyValidationService_ValidatePartyAccountCurrency_WithPartyBillingCurrency_RejectsThirdCurrency()
    {
        // With party billing currency = USD and company = MYR:
        // Account in EUR must be rejected
        var ex = Should.Throw<BusinessException>(() =>
            _partyValidationService.ValidatePartyAccountCurrency(
                partyAccountCurrency: "EUR",
                advanceAccountCurrency: null,
                partyBillingCurrency: "USD",
                companyCurrency: "MYR"));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.PartyAccountCurrencyMismatch);
        ex.Data["accountCurrency"]?.ToString().ShouldBe("EUR");
        ex.Data["partyCurrency"]?.ToString().ShouldBe("USD");
    }

    [Fact]
    public void Customer_Entity_HasDefaultCurrencyProperty()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Cust 01")
        {
            DefaultCurrency = "USD"
        };

        customer.DefaultCurrency.ShouldBe("USD");
    }
}
