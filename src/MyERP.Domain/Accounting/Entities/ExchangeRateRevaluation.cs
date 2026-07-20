using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Exchange Rate Revaluation document — revalues foreign currency Balance Sheet accounts
/// at period-end to recognize unrealized exchange gain/loss.
/// 
/// Creates TWO separate JEs per ERPNext pattern:
/// 1. Zero-balance JE (for accounts with zeroed-out currency)
/// 2. Revaluation JE (for accounts with balances in both currencies)
/// 
/// Source: erpnext/accounts/doctype/exchange_rate_revaluation/exchange_rate_revaluation.py
/// </summary>
public class ExchangeRateRevaluation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Date of revaluation (typically period-end).</summary>
    public DateTime PostingDate { get; set; }

    /// <summary>Reference to the Unrealized Exchange Gain/Loss account.</summary>
    public Guid ExchangeGainLossAccountId { get; set; }

    /// <summary>Allowance for rounding loss on zero-balance accounts. Range [0, 1).</summary>
    public decimal RoundingLossAllowance { get; set; }

    /// <summary>Total unrealized gain (positive) or loss (negative) from revaluation.</summary>
    public decimal TotalGainLoss { get; set; }

    /// <summary>Document status.</summary>
    public ExchangeRateRevaluationStatus Status { get; private set; } = ExchangeRateRevaluationStatus.Draft;

    /// <summary>Per-account revaluation detail lines.</summary>
    public ICollection<ExchangeRateRevaluationEntry> Entries { get; private set; }
        = new List<ExchangeRateRevaluationEntry>();

    /// <summary>ID of the generated revaluation Journal Entry.</summary>
    public Guid? RevaluationJournalEntryId { get; set; }

    /// <summary>ID of the generated zero-balance Journal Entry.</summary>
    public Guid? ZeroBalanceJournalEntryId { get; set; }

    protected ExchangeRateRevaluation() { }

    public ExchangeRateRevaluation(
        Guid id,
        Guid companyId,
        DateTime postingDate,
        Guid exchangeGainLossAccountId,
        decimal roundingLossAllowance = 0m,
        Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        PostingDate = postingDate;
        ExchangeGainLossAccountId = exchangeGainLossAccountId;
        TenantId = tenantId;

        if (roundingLossAllowance < 0 || roundingLossAllowance >= 1)
        {
            throw new BusinessException("MyERP:02017")
                .WithData("value", roundingLossAllowance);
        }
        RoundingLossAllowance = roundingLossAllowance;
    }

    /// <summary>
    /// Add a revaluation entry for an account.
    /// </summary>
    public void AddEntry(
        Guid accountId,
        string accountCurrency,
        decimal balanceInAccountCurrency,
        decimal currentBalanceInCompanyCurrency,
        decimal newExchangeRate)
    {
        if (Status != ExchangeRateRevaluationStatus.Draft)
            throw new BusinessException("MyERP:01001");

        var newBalanceInCompanyCurrency = balanceInAccountCurrency * newExchangeRate;
        var gainLoss = newBalanceInCompanyCurrency - currentBalanceInCompanyCurrency;

        Entries.Add(new ExchangeRateRevaluationEntry(
            Guid.NewGuid(),
            Id,
            accountId,
            accountCurrency,
            balanceInAccountCurrency,
            currentBalanceInCompanyCurrency,
            newExchangeRate,
            newBalanceInCompanyCurrency,
            gainLoss));
    }

    /// <summary>
    /// Add an entry for a party-specific balance (per-customer/supplier revaluation).
    /// </summary>
    public void AddPartyEntry(
        Guid accountId,
        string accountCurrency,
        string partyType,
        Guid partyId,
        decimal balanceInAccountCurrency,
        decimal currentBalanceInCompanyCurrency,
        decimal newExchangeRate)
    {
        if (Status != ExchangeRateRevaluationStatus.Draft)
            throw new BusinessException("MyERP:01001");

        var newBalanceInCompanyCurrency = balanceInAccountCurrency * newExchangeRate;
        var gainLoss = newBalanceInCompanyCurrency - currentBalanceInCompanyCurrency;

        var entry = new ExchangeRateRevaluationEntry(
            Guid.NewGuid(),
            Id,
            accountId,
            accountCurrency,
            balanceInAccountCurrency,
            currentBalanceInCompanyCurrency,
            newExchangeRate,
            newBalanceInCompanyCurrency,
            gainLoss);

        entry.PartyType = partyType;
        entry.PartyId = partyId;

        Entries.Add(entry);
    }

    /// <summary>
    /// Submit the revaluation. Removes zero-gain entries and validates.
    /// </summary>
    public void Submit()
    {
        if (Status != ExchangeRateRevaluationStatus.Draft)
            throw new BusinessException("MyERP:01001");

        // Remove entries with zero gain/loss (nothing to revalue)
        var zeroEntries = Entries.Where(e => e.GainLoss == 0).ToList();
        foreach (var e in zeroEntries)
            Entries.Remove(e);

        if (!Entries.Any())
        {
            throw new BusinessException("MyERP:02018")
                .WithData("detail", "No accounts have unrealized gain/loss to revalue.");
        }

        TotalGainLoss = Entries.Sum(e => e.GainLoss);
        Status = ExchangeRateRevaluationStatus.Submitted;
    }

    /// <summary>
    /// Cancel the revaluation.
    /// </summary>
    public void Cancel()
    {
        if (Status != ExchangeRateRevaluationStatus.Submitted)
            throw new BusinessException("MyERP:01001");

        Status = ExchangeRateRevaluationStatus.Cancelled;
    }
}

/// <summary>
/// Per-account detail of an Exchange Rate Revaluation.
/// Shows old and new balance + calculated gain/loss per account (or per party).
/// </summary>
public class ExchangeRateRevaluationEntry : Volo.Abp.Domain.Entities.Entity<Guid>
{
    public Guid ExchangeRateRevaluationId { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>Foreign currency of the account (e.g., "USD").</summary>
    public string AccountCurrency { get; set; } = null!;

    /// <summary>Balance in account's currency at revaluation date.</summary>
    public decimal BalanceInAccountCurrency { get; set; }

    /// <summary>Current balance in company currency (from GL at old rate).</summary>
    public decimal CurrentBalanceInCompanyCurrency { get; set; }

    /// <summary>New exchange rate used for revaluation.</summary>
    public decimal NewExchangeRate { get; set; }

    /// <summary>New balance in company currency (balance × new rate).</summary>
    public decimal NewBalanceInCompanyCurrency { get; set; }

    /// <summary>Unrealized gain (positive) or loss (negative).</summary>
    public decimal GainLoss { get; set; }

    /// <summary>Party type for party-specific revaluation (null for non-party accounts).</summary>
    public string? PartyType { get; set; }

    /// <summary>Party ID for party-specific revaluation.</summary>
    public Guid? PartyId { get; set; }

    protected ExchangeRateRevaluationEntry() { }

    public ExchangeRateRevaluationEntry(
        Guid id,
        Guid revaluationId,
        Guid accountId,
        string accountCurrency,
        decimal balanceInAccountCurrency,
        decimal currentBalanceInCompanyCurrency,
        decimal newExchangeRate,
        decimal newBalanceInCompanyCurrency,
        decimal gainLoss) : base(id)
    {
        ExchangeRateRevaluationId = revaluationId;
        AccountId = accountId;
        AccountCurrency = accountCurrency;
        BalanceInAccountCurrency = balanceInAccountCurrency;
        CurrentBalanceInCompanyCurrency = currentBalanceInCompanyCurrency;
        NewExchangeRate = newExchangeRate;
        NewBalanceInCompanyCurrency = newBalanceInCompanyCurrency;
        GainLoss = gainLoss;
    }
}

public enum ExchangeRateRevaluationStatus
{
    Draft = 0,
    Submitted = 1,
    Cancelled = 2
}
