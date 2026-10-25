using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Users;

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
    /// ERPNext validate_party_frozen_disabled: a disabled party always blocks; a frozen party blocks
    /// unless the current user holds the company's role_allowed_for_frozen_entries.
    /// </summary>
    public async Task ValidatePartyForTransactionAsync(
        string partyType, bool isDisabled, bool isFrozen, string partyName, Guid companyId)
    {
        if (isFrozen && !isDisabled)
        {
            var company = await LazyServiceProvider.LazyGetRequiredService<IRepository<Company, Guid>>().FindAsync(companyId);
            var role = company?.RoleAllowedForFrozenEntries;
            if (!string.IsNullOrWhiteSpace(role) && LazyServiceProvider.LazyGetRequiredService<ICurrentUser>().IsInRole(role))
                isFrozen = false;
        }

        ValidatePartyStatus(partyType, isFrozen, isDisabled, partyName);
    }

    /// <summary>
    /// Validates that a customer is not on hold / blocked (ERPNext PR #59303 / commit d75b957ce0).
    /// Blocks saving or submitting Sales Orders, Delivery Notes, Sales Invoices and POS Invoices.
    /// Quotations are exempt.
    /// </summary>
    public void ValidateCustomerNotBlocked(bool onHold, DateTime? releaseDate, string customerName, DateTime? transactionDate = null)
    {
        if (!onHold) return;

        var asOf = (transactionDate ?? DateTime.UtcNow).Date;
        var isBlocked = !releaseDate.HasValue || asOf <= releaseDate.Value.Date;
        if (isBlocked)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CustomerBlocked)
                .WithData("customerName", customerName)
                .WithData("detail", $"{customerName} is blocked so this transaction cannot proceed.");
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

    private static readonly Dictionary<string, PartyAccountType> StandardPartyTypeDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Customer", PartyAccountType.Receivable },
        { "Supplier", PartyAccountType.Payable },
        { "Employee", PartyAccountType.Payable },
        { "Shareholder", PartyAccountType.Payable },
        { "Member", PartyAccountType.Receivable },
    };

    /// <summary>
    /// Validates that the specified partyType exists in the registered PartyType table.
    /// Maps to ERPNext PR #60804 (commits 91c731fff2, 86bfd5a633).
    /// </summary>
    public async Task<PartyType> ValidatePartyTypeExistsAsync(string partyType, string? contextPrefix = null)
    {
        Check.NotNullOrWhiteSpace(partyType, nameof(partyType));
        var trimmed = partyType.Trim();

        var partyTypeRepo = LazyServiceProvider?.LazyGetService<IRepository<PartyType, Guid>>();
        PartyType? pt = null;
        if (partyTypeRepo != null)
        {
            var query = await partyTypeRepo.GetQueryableAsync();
            pt = query.FirstOrDefault(p => p.Name.ToLower() == trimmed.ToLower());
        }

        if (pt == null)
        {
            if (partyTypeRepo != null)
            {
                var count = await partyTypeRepo.GetCountAsync();
                if (count == 0 && StandardPartyTypeDefaults.TryGetValue(trimmed, out var defaultAccountType))
                {
                    pt = new PartyType(Guid.NewGuid(), trimmed, defaultAccountType);
                }
            }
            else if (StandardPartyTypeDefaults.TryGetValue(trimmed, out var defaultAccountType))
            {
                pt = new PartyType(Guid.NewGuid(), trimmed, defaultAccountType);
            }
        }

        if (pt == null)
        {
            var prefix = contextPrefix ?? string.Empty;
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"{prefix}{trimmed} is not a valid Party Type");
        }

        return pt;
    }

    /// <summary>
    /// Validates that a party account matches the PartyType's configured account type (Receivable vs Payable).
    /// Maps to ERPNext PR #60804 (commit 86bfd5a633).
    /// Exemptions:
    /// 1. Employee party type (no restriction)
    /// 2. Shareholder with Equity account
    /// 3. BookAdvancePaymentsInSeparatePartyAccount enabled
    /// </summary>
    public void ValidatePartyAccountType(string partyType, PartyAccountType expectedAccountType, Account account, bool separateAdvance = false)
    {
        if (string.Equals(partyType, "Employee", StringComparison.OrdinalIgnoreCase))
            return;

        if (separateAdvance)
            return;

        if (string.Equals(partyType, "Shareholder", StringComparison.OrdinalIgnoreCase)
            && (account.AccountType == AccountType.Equity || account.AccountSubType is AccountSubType.ShareCapital or AccountSubType.RetainedEarnings))
        {
            return;
        }

        bool matches = false;
        if (expectedAccountType == PartyAccountType.Receivable)
        {
            matches = account.AccountSubType == AccountSubType.AccountsReceivable
                || (account.AccountType == AccountType.Asset && (account.AccountName.Contains("Receivable", StringComparison.OrdinalIgnoreCase) || account.AccountName.Contains("Debtor", StringComparison.OrdinalIgnoreCase)));
        }
        else if (expectedAccountType == PartyAccountType.Payable)
        {
            matches = account.AccountSubType == AccountSubType.AccountsPayable
                || (account.AccountType == AccountType.Liability && (account.AccountName.Contains("Payable", StringComparison.OrdinalIgnoreCase) || account.AccountName.Contains("Creditor", StringComparison.OrdinalIgnoreCase)));
        }

        if (!matches)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Account {account.AccountName} and Party Type {partyType} have different account types");
        }
    }

    /// <summary>
    /// Validates that the party record exists in its corresponding aggregate table.
    /// Maps to ERPNext PR #60804 (commit 86bfd5a633).
    /// </summary>
    public async Task ValidatePartyEntityExistsAsync(string partyType, Guid partyId)
    {
        if (LazyServiceProvider == null) return;

        bool exists = true;
        if (string.Equals(partyType, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            var custRepo = LazyServiceProvider.LazyGetService<IRepository<Sales.Entities.Customer, Guid>>();
            if (custRepo != null) exists = await custRepo.FindAsync(partyId) != null;
        }
        else if (string.Equals(partyType, "Supplier", StringComparison.OrdinalIgnoreCase))
        {
            var suppRepo = LazyServiceProvider.LazyGetService<IRepository<Purchasing.Entities.Supplier, Guid>>();
            if (suppRepo != null) exists = await suppRepo.FindAsync(partyId) != null;
        }
        else if (string.Equals(partyType, "Employee", StringComparison.OrdinalIgnoreCase))
        {
            var empRepo = LazyServiceProvider.LazyGetService<IRepository<HumanResources.Entities.Employee, Guid>>();
            if (empRepo != null) exists = await empRepo.FindAsync(partyId) != null;
        }
        else if (string.Equals(partyType, "Shareholder", StringComparison.OrdinalIgnoreCase))
        {
            var shRepo = LazyServiceProvider.LazyGetService<IRepository<Shareholder, Guid>>();
            if (shRepo != null) exists = await shRepo.FindAsync(partyId) != null;
        }

        if (!exists)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"{partyType} {partyId} does not exist");
        }
    }
}
