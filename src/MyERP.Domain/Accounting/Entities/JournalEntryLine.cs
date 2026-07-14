using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Individual line in a journal entry (one debit or credit posting).
/// Maps to ERPNext accounts/doctype/journal_entry_account (GL Entry).
/// Supports multi-currency: Amount is in COMPANY currency, AmountInAccountCurrency is in the account's currency.
/// </summary>
public class JournalEntryLine : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>Amount in company (base) currency. Always positive. Direction determined by IsDebit.</summary>
    public decimal Amount { get; set; }

    /// <summary>True = Debit, False = Credit.</summary>
    public bool IsDebit { get; set; }

    /// <summary>Account currency code (e.g., "USD", "MYR"). When null, defaults to company currency.</summary>
    public string? AccountCurrency { get; set; }

    /// <summary>
    /// Amount in the account's native currency. For same-currency, equals Amount.
    /// For foreign currency accounts: Amount = AmountInAccountCurrency × ExchangeRate.
    /// </summary>
    public decimal AmountInAccountCurrency { get; set; }

    /// <summary>Exchange rate used: account_currency → company_currency. Default 1.0 for same-currency.</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    public string? Description { get; set; }

    /// <summary>Optional: party reference (customer/supplier) for subledger tracking.</summary>
    public Guid? PartyId { get; set; }

    /// <summary>Party type: "Customer" or "Supplier".</summary>
    public string? PartyType { get; set; }

    /// <summary>Cost center for departmental reporting (P&L accounts only).</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Project for project-wise P&L reporting.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Against voucher type — the document being settled (for advance/reconciliation entries).
    /// e.g., When a payment JE settles an invoice: AgainstVoucherType = "SalesInvoice".
    /// </summary>
    public string? AgainstVoucherType { get; set; }

    /// <summary>Against voucher ID (the document being settled).</summary>
    public Guid? AgainstVoucherId { get; set; }

    /// <summary>Indicates this line is an advance payment entry.</summary>
    public bool IsAdvance { get; set; }

    /// <summary>Finance book for multi-depreciation support (null = default book).</summary>
    public string? FinanceBook { get; set; }

    protected JournalEntryLine() { }

    public JournalEntryLine(Guid id, Guid journalEntryId, Guid accountId, decimal amount, bool isDebit, string? description = null)
        : base(id)
    {
        JournalEntryId = journalEntryId;
        AccountId = accountId;
        Amount = amount;
        IsDebit = isDebit;
        Description = description;
        AmountInAccountCurrency = amount; // Same currency by default
    }

    /// <summary>
    /// Creates a multi-currency GL line. Amount is in company currency.
    /// AmountInAccountCurrency is in the account's native currency.
    /// </summary>
    public JournalEntryLine(Guid id, Guid journalEntryId, Guid accountId, decimal amountInCompanyCurrency,
        bool isDebit, string accountCurrency, decimal amountInAccountCurrency, decimal exchangeRate, string? description = null)
        : base(id)
    {
        JournalEntryId = journalEntryId;
        AccountId = accountId;
        Amount = amountInCompanyCurrency;
        IsDebit = isDebit;
        Description = description;
        AccountCurrency = accountCurrency;
        AmountInAccountCurrency = amountInAccountCurrency;
        ExchangeRate = exchangeRate;
    }
}
