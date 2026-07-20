using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Interface for documents that can be posted to the general ledger.
/// </summary>
public interface IAccountableDocument
{
    Guid Id { get; }
    Guid CompanyId { get; }
    string DocumentType { get; }
    decimal NetTotal { get; }
    decimal GrandTotal { get; }
    decimal TaxAmount { get; }
    Guid? CustomerId { get; }
    Guid? SupplierId { get; }
    DateTime PostingDate { get; }

    /// <summary>Transaction currency code (e.g., "USD"). May differ from company currency.</summary>
    string CurrencyCode { get; }

    /// <summary>Exchange rate: transaction_currency → company_currency. Default 1.0 for same-currency.</summary>
    decimal ExchangeRate { get; }

    /// <summary>
    /// Optional finance book for this document's GL entries.
    /// Null = default book. Named book = entries tagged for multi-book reporting.
    /// Per ERPNext: asset depreciation uses separate finance books (tax vs management).
    /// </summary>
    string? FinanceBook => null;
}

/// <summary>
/// Accounting Rules Engine — generates journal entries from documents using configurable rules.
/// This is the core of the ERP's accounting system. NEVER hardcode GL postings.
/// </summary>
public class AccountingRuleEngine : DomainService
{
    private readonly IRepository<AccountingRule, Guid> _ruleRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public AccountingRuleEngine(
        IRepository<AccountingRule, Guid> ruleRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _ruleRepository = ruleRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Generate a balanced journal entry from a source document using configured accounting rules.
    /// Supports multi-currency: amounts are recorded in both company currency and account currency.
    /// </summary>
    public async Task<JournalEntry> PostDocumentAsync(IAccountableDocument document)
    {
        var rules = await _ruleRepository.GetListAsync(r =>
            r.CompanyId == document.CompanyId &&
            r.DocumentType == document.DocumentType &&
            r.IsActive);

        if (!rules.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.UnbalancedJournalEntry)
                .WithData("documentType", document.DocumentType);
        }

        var fiscalYear = await GetFiscalYearAsync(document.CompanyId, document.PostingDate);
        var company = await _companyRepository.GetAsync(document.CompanyId);

        var journal = new JournalEntry(
            GuidGenerator.Create(),
            document.CompanyId,
            fiscalYear.Id,
            document.PostingDate);

        journal.ReferenceType = document.DocumentType;
        journal.ReferenceId = document.Id;

        var isMultiCurrency = document.ExchangeRate != 1m;

        foreach (var rule in rules.OrderBy(r => r.SortOrder))
        {
            var amountInTransactionCurrency = ResolveAmount(rule.AmountSource, document);
            if (amountInTransactionCurrency <= 0) continue;

            var accountId = ResolveAccountId(rule, company);

            if (isMultiCurrency)
            {
                // Multi-currency: Amount in company currency, AmountInAccountCurrency in transaction currency
                var amountInCompanyCurrency = Math.Round(amountInTransactionCurrency * document.ExchangeRate, 4);
                journal.AddLine(accountId, amountInCompanyCurrency, rule.IsDebit);

                // Set multi-currency fields on the last added line
                var lastLine = journal.Lines[^1];
                lastLine.AccountCurrency = document.CurrencyCode;
                lastLine.AmountInAccountCurrency = amountInTransactionCurrency;
                lastLine.ExchangeRate = document.ExchangeRate;
                lastLine.FinanceBook = document.FinanceBook;
            }
            else
            {
                // Same currency: Amount = AmountInAccountCurrency, ExchangeRate = 1
                journal.AddLine(accountId, amountInTransactionCurrency, rule.IsDebit);

                // Tag with finance book if specified
                if (document.FinanceBook != null)
                    journal.Lines[^1].FinanceBook = document.FinanceBook;
            }
        }

        // Double-entry validation — will throw if unbalanced
        journal.Validate();
        journal.Post();

        return journal;
    }

    private decimal ResolveAmount(AmountSource source, IAccountableDocument document)
    {
        return source switch
        {
            AmountSource.NetTotal => document.NetTotal,
            AmountSource.GrandTotal => document.GrandTotal,
            AmountSource.TaxAmount => document.TaxAmount,
            _ => 0m
        };
    }

    private Guid ResolveAccountId(AccountingRule rule, Company company)
    {
        switch (rule.AccountSource)
        {
            case AccountSource.FixedAccount:
                if (rule.FixedAccountId == null)
                    throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                        .WithData("ruleName", rule.Name);
                return rule.FixedAccountId.Value;

            case AccountSource.CustomerReceivable:
                return company.DefaultReceivableAccountId
                    ?? rule.FixedAccountId
                    ?? throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                        .WithData("ruleName", rule.Name + " (no receivable account configured)");

            case AccountSource.SupplierPayable:
                return company.DefaultPayableAccountId
                    ?? rule.FixedAccountId
                    ?? throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                        .WithData("ruleName", rule.Name + " (no payable account configured)");

            case AccountSource.ItemIncome:
                return company.DefaultIncomeAccountId
                    ?? rule.FixedAccountId
                    ?? throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                        .WithData("ruleName", rule.Name + " (no income account configured)");

            case AccountSource.ItemExpense:
                return company.DefaultExpenseAccountId
                    ?? rule.FixedAccountId
                    ?? throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                        .WithData("ruleName", rule.Name + " (no expense account configured)");

            case AccountSource.TaxPayable:
                return rule.FixedAccountId
                    ?? throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                        .WithData("ruleName", rule.Name + " (no tax account configured)");

            default:
                return rule.FixedAccountId
                    ?? throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                        .WithData("ruleName", rule.Name);
        }
    }

    private async Task<FiscalYear> GetFiscalYearAsync(Guid companyId, DateTime date)
    {
        var fiscalYear = await _fiscalYearRepository.FindAsync(fy =>
            fy.CompanyId == companyId &&
            !fy.IsClosed &&
            fy.StartDate <= date &&
            fy.EndDate >= date);

        if (fiscalYear == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("date", date);
        }

        return fiscalYear;
    }
}
