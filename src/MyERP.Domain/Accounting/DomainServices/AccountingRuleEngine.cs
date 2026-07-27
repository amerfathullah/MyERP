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
    /// Total stock cost at valuation rate (for COGS posting on delivery documents).
    /// = SUM(item.StockQty × item.ValuationRate). Default 0 for non-inventory documents.
    /// Per ERPNext: DN GL uses stock_value_difference from SLEs, not selling price.
    /// </summary>
    decimal StockCostTotal => 0;

    /// <summary>
    /// Optional finance book for this document's GL entries.
    /// Null = default book. Named book = entries tagged for multi-book reporting.
    /// Per ERPNext: asset depreciation uses separate finance books (tax vs management).
    /// </summary>
    string? FinanceBook => null;

    /// <summary>
    /// Default cost center for this document's GL entries.
    /// Null = no cost center assigned (caller may distribute later).
    /// Per ERPNext: cost center is typically set from Company defaults or item-level settings.
    /// </summary>
    Guid? CostCenterId => null;
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
    private readonly CostCenterAllocationService _allocationService;

    public AccountingRuleEngine(
        IRepository<AccountingRule, Guid> ruleRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Company, Guid> companyRepository,
        CostCenterAllocationService allocationService)
    {
        _ruleRepository = ruleRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _companyRepository = companyRepository;
        _allocationService = allocationService;
    }

    /// <summary>
    /// Generate a balanced journal entry from a source document using configured accounting rules.
    /// Supports multi-currency: amounts are recorded in both company currency and account currency.
    /// </summary>
    public async Task<JournalEntry> PostDocumentAsync(IAccountableDocument document)
    {
        return await PostDocumentAsync(document, warehouseStockAccountOverride: null);
    }

    /// <summary>
    /// Generate a balanced journal entry with a warehouse-specific stock account override.
    /// When provided, any rule with AccountSource.WarehouseStock will use this ID instead of company default.
    /// Per ERPNext BaseStockGLComposer: uses WarehouseAccountService to resolve per-warehouse GL.
    /// </summary>
    public async Task<JournalEntry> PostDocumentAsync(IAccountableDocument document, Guid? warehouseStockAccountOverride)
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

            var accountId = ResolveAccountId(rule, company, warehouseStockAccountOverride);

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

        // Apply cost center allocation BEFORE validation (distribution may expand lines)
        // Per ERPNext gotcha #418: budget validates MAIN CC, GL posts to split CCs
        // Per gotcha #550: distribution happens before journal posting
        if (document.CostCenterId.HasValue)
        {
            await ApplyCostCenterAllocationAsync(journal, document.CostCenterId.Value, document.PostingDate);
        }

        // Double-entry validation — will throw if unbalanced
        journal.Validate();
        journal.Post();

        return journal;
    }

    /// <summary>
    /// Applies cost center allocation to journal lines.
    /// If an active allocation exists, replaces each line with N distributed lines (one per child CC).
    /// If no allocation exists, assigns the cost center directly to all lines.
    /// Per ERPNext gotcha #418: distributes debit/credit amounts only. Round-off to FIRST sub-CC.
    /// </summary>
    private async Task ApplyCostCenterAllocationAsync(JournalEntry journal, Guid costCenterId, DateTime postingDate)
    {
        var distribution = await _allocationService.DistributeGlAmountsAsync(costCenterId, 1m, 1m, postingDate);

        if (distribution == null)
        {
            // No allocation — assign cost center directly to all lines
            foreach (var line in journal.Lines)
            {
                line.CostCenterId = costCenterId;
            }
            return;
        }

        // Distribution needed — expand each existing line into N lines per allocation entry
        // We need to collect original lines, then add distributed copies.
        // Since JournalEntry._lines is a List and AddLine enforces Draft status,
        // we set CostCenterId on existing lines and add new ones for the remaining entries.

        var originalLines = journal.Lines.ToList();

        foreach (var originalLine in originalLines)
        {
            // Calculate proportional amounts per child CC
            var lineDebit = originalLine.IsDebit ? originalLine.Amount : 0m;
            var lineCredit = originalLine.IsDebit ? 0m : originalLine.Amount;

            decimal totalDistributed = 0m;

            for (int i = 0; i < distribution.Count; i++)
            {
                var (childCcId, pctDebit, pctCredit) = distribution[i];
                // Use the distribution ratios: pctDebit/pctCredit are already scaled to total=1
                var distAmount = originalLine.IsDebit
                    ? Math.Round(originalLine.Amount * pctDebit, 4)
                    : Math.Round(originalLine.Amount * pctCredit, 4);

                if (i == 0)
                {
                    // Reuse the original line for the first child CC
                    originalLine.CostCenterId = childCcId;
                    // Amount stays the same for now — will be adjusted for remainder below
                    totalDistributed += distAmount;
                }
                else if (distAmount > 0)
                {
                    // Add new distributed lines for remaining child CCs
                    journal.AddLine(originalLine.AccountId, distAmount, originalLine.IsDebit, originalLine.Description);
                    var newLine = journal.Lines[^1];
                    newLine.CostCenterId = childCcId;
                    newLine.AccountCurrency = originalLine.AccountCurrency;
                    newLine.ExchangeRate = originalLine.ExchangeRate;
                    newLine.FinanceBook = originalLine.FinanceBook;

                    totalDistributed += distAmount;
                }
            }

            // Adjust first line amount — absorbs rounding remainder per gotcha #418
            if (distribution.Count > 1)
            {
                var firstPortion = originalLine.IsDebit
                    ? Math.Round(originalLine.Amount * distribution[0].Debit, 4)
                    : Math.Round(originalLine.Amount * distribution[0].Credit, 4);
                var remainder = originalLine.Amount - totalDistributed;
                // First CC line gets its proportional share + any rounding remainder
                originalLine.Amount = firstPortion + remainder;
            }
        }
    }

    private decimal ResolveAmount(AmountSource source, IAccountableDocument document)
    {
        return source switch
        {
            AmountSource.NetTotal => document.NetTotal,
            AmountSource.GrandTotal => document.GrandTotal,
            AmountSource.TaxAmount => document.TaxAmount,
            AmountSource.StockCostTotal => document.StockCostTotal,
            _ => 0m
        };
    }

    private Guid ResolveAccountId(AccountingRule rule, Company company, Guid? warehouseStockAccountOverride = null)
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

            case AccountSource.WarehouseStock:
                // Per ERPNext: full resolution uses WarehouseAccountService with 5-level chain.
                // When warehouseStockAccountOverride is provided (from AppService via WarehouseAccountService),
                // use the resolved per-warehouse account. Otherwise fall back to company default.
                return warehouseStockAccountOverride
                    ?? company.DefaultInventoryAccountId
                    ?? rule.FixedAccountId
                    ?? throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                        .WithData("ruleName", rule.Name + " (no inventory account configured)");

            default:
                return rule.FixedAccountId
                    ?? throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                        .WithData("ruleName", rule.Name);
        }
    }

    /// <summary>
    /// Checks if a cost center has an active allocation and returns the distribution.
    /// Called by AppServices before GL posting to expand lines.
    /// Per ERPNext gotcha #418: validates MAIN cost center budget BEFORE allocation splits.
    /// Returns null if no allocation exists (post directly to original CC).
    /// </summary>
    public async Task<List<(Guid CostCenterId, decimal Debit, decimal Credit)>?> GetCostCenterDistributionAsync(
        Guid costCenterId,
        decimal debit,
        decimal credit,
        DateTime postingDate)
    {
        return await _allocationService.DistributeGlAmountsAsync(costCenterId, debit, credit, postingDate);
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
