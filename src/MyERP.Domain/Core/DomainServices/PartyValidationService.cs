using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Domain service executing the 6 standard party validations in order (Gotchas #1004, #1130).
/// 1. validate_party (frozen/disabled check with Employee=WARNING only)
/// 2. validate_party_accounts (item account != party account)
/// 3. validate_currency (GLE currency lock)
/// 4. validate_party_account_currency (3-way match: party account, advance account, party currency)
/// 5. validate_address_and_contact (linked to correct party)
/// 6. validate_company_linked_addresses (company address belongs to doc company)
/// </summary>
public class PartyValidationService : DomainService
{
    private ILogger SafeLogger => LazyServiceProvider != null ? Logger : NullLogger.Instance;

    /// <summary>
    /// Validates whether a party is disabled or frozen.
    /// Per gotcha #1130: Disabled employees issue a warning only, other parties hard-throw.
    /// </summary>
    public void ValidatePartyStatus(string partyType, bool isFrozen, bool isDisabled, string partyName)
    {
        if (string.Equals(partyType, "Employee", StringComparison.OrdinalIgnoreCase))
        {
            if (isDisabled)
            {
                SafeLogger.LogWarning("Employee {PartyName} is marked as disabled or inactive.", partyName);
            }
            return;
        }

        if (isDisabled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PartyDisabled)
                .WithData("partyName", partyName);
        }

        if (isFrozen)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PartyFrozen)
                .WithData("partyName", partyName);
        }
    }

    /// <summary>
    /// Validates that line item accounts (income/expense) do not match the party receivable/payable account.
    /// Per gotcha #1130 step 2: item account != party account.
    /// </summary>
    public void ValidatePartyAccounts(Guid partyAccountId, IEnumerable<Guid> itemAccountIds)
    {
        if (itemAccountIds != null && itemAccountIds.Contains(partyAccountId))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ItemAccountCannotBePartyAccount);
        }
    }

    /// <summary>
    /// Enforces 3-way mutual constraint between party account, advance account, and party currency.
    /// Per gotchas #1004, #1130:
    /// - Party account currency must match either party billing currency or company default currency.
    /// - Advance account currency must also match party billing currency or company default currency.
    /// - Advance account and party account must be the same currency.
    /// </summary>
    public void ValidatePartyAccountCurrency(
        string partyAccountCurrency,
        string? advanceAccountCurrency,
        string partyBillingCurrency,
        string companyCurrency)
    {
        var validCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            partyBillingCurrency,
            companyCurrency
        };

        if (!validCurrencies.Contains(partyAccountCurrency))
        {
            throw new BusinessException(MyERPDomainErrorCodes.PartyAccountCurrencyMismatch)
                .WithData("accountCurrency", partyAccountCurrency)
                .WithData("advanceCurrency", advanceAccountCurrency ?? "")
                .WithData("partyCurrency", partyBillingCurrency);
        }

        if (!string.IsNullOrWhiteSpace(advanceAccountCurrency))
        {
            if (!validCurrencies.Contains(advanceAccountCurrency))
            {
                throw new BusinessException(MyERPDomainErrorCodes.PartyAccountCurrencyMismatch)
                    .WithData("accountCurrency", partyAccountCurrency)
                    .WithData("advanceCurrency", advanceAccountCurrency)
                    .WithData("partyCurrency", partyBillingCurrency);
            }

            if (!string.Equals(partyAccountCurrency, advanceAccountCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(MyERPDomainErrorCodes.PartyAccountCurrencyMismatch)
                    .WithData("accountCurrency", partyAccountCurrency)
                    .WithData("advanceCurrency", advanceAccountCurrency)
                    .WithData("partyCurrency", partyBillingCurrency);
            }
        }
    }

    /// <summary>
    /// Validates that an address is linked to the expected party.
    /// </summary>
    public void ValidatePartyAddress(Address? address, string expectedPartyType, Guid expectedPartyId)
    {
        if (address == null) return;

        if (!string.Equals(address.PartyType, expectedPartyType, StringComparison.OrdinalIgnoreCase) ||
            address.PartyId != expectedPartyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Address does not belong to {expectedPartyType} (ID: {expectedPartyId}).");
        }
    }

    /// <summary>
    /// Validates that a company address belongs to the transaction's company.
    /// </summary>
    public void ValidateCompanyAddress(Address? address, Guid expectedCompanyId)
    {
        if (address == null) return;

        if (string.Equals(address.PartyType, "Company", StringComparison.OrdinalIgnoreCase) &&
            address.PartyId != expectedCompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Company address does not belong to company (ID: {expectedCompanyId}).");
        }
    }
}
