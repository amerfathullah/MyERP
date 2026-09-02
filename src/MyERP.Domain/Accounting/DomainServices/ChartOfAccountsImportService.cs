using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Chart of Accounts Import Service — creates hierarchical account structures from templates.
/// ERPNext equivalent: accounts/doctype/chart_of_accounts_importer/chart_of_accounts_importer.py
/// 
/// Used by:
/// 1. DefaultDataSeeder — seeding initial CoA for new companies
/// 2. CoA Importer API — admin bulk-import from CSV/JSON templates
/// 3. Root company account sync — copying CoA from parent to child company
/// 
/// Per DO-NOT: "Import Chart of Accounts if GL entries exist (data corruption)"
/// </summary>
public class ChartOfAccountsImportService : DomainService
{
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<Core.Entities.Company, Guid>? _companyRepository;

    public ChartOfAccountsImportService(
        IRepository<Account, Guid> accountRepository,
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<Core.Entities.Company, Guid>? companyRepository = null)
    {
        _accountRepository = accountRepository;
        _journalRepository = journalRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Imports a full chart of accounts from a structured template.
    /// Creates accounts top-down (parents before children) with proper ID linkage.
    /// 
    /// Per DO-NOT & ERPNext PR #58065:
    /// - Blocked if GL entries exist for this company (data corruption).
    /// - Clears old company default accounts and removes existing accounts if no GL entries exist.
    /// - Auto-assigns standard company default accounts after import.
    /// </summary>
    /// <returns>Number of accounts created.</returns>
    public async Task<int> ImportAsync(Guid companyId, IReadOnlyList<CoaTemplateRow> rows, Guid? tenantId = null)
    {
        // Guard: cannot import if GL entries exist
        var hasGlEntries = await _journalRepository.AnyAsync(je =>
            je.CompanyId == companyId && je.Status == Core.DocumentStatus.Posted);

        if (hasGlEntries)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ChartOfAccountsImportBlocked)
                .WithData("companyId", companyId);
        }

        // Validate no duplicate codes in template
        var codes = rows.Select(r => r.AccountCode).ToList();
        var duplicates = codes.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.DuplicateAccountCode)
                .WithData("codes", string.Join(", ", duplicates));
        }

        // Reset existing company default accounts before replacing (per ERPNext unset_existing_data)
        Core.Entities.Company? company = null;
        if (_companyRepository != null)
        {
            company = await _companyRepository.FindAsync(companyId);
            if (company != null)
            {
                company.DefaultReceivableAccountId = null;
                company.DefaultPayableAccountId = null;
                company.DefaultIncomeAccountId = null;
                company.DefaultExpenseAccountId = null;
                company.DefaultTaxPayableAccountId = null;
                company.DefaultBankAccountId = null;
                company.DefaultInventoryAccountId = null;
                company.DefaultStockAdjustmentAccountId = null;
                company.StockReceivedButNotBilledAccountId = null;
                company.StockDeliveredButNotBilledAccountId = null;
                company.ExchangeGainLossAccountId = null;
                company.ExchangeGainAccountId = null;
                company.ExchangeLossAccountId = null;
                company.BankChargesAccountId = null;
                company.DepreciationExpenseAccountId = null;
                company.AccumulatedDepreciationAccountId = null;
                company.RoundOffAccountId = null;
                await _companyRepository.UpdateAsync(company);
            }
        }

        // Remove existing un-posted accounts for this company before importing new ones
        await _accountRepository.DeleteAsync(a => a.CompanyId == companyId);

        // Build parent code → ID map for hierarchical linking
        var codeToIdMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        // Sort by depth (parents first) — use parent code presence as proxy
        var sortedRows = TopologicalSort(rows);

        var createdAccounts = new List<Account>();
        foreach (var row in sortedRows)
        {
            Guid? parentId = null;
            if (!string.IsNullOrWhiteSpace(row.ParentCode))
            {
                if (!codeToIdMap.TryGetValue(row.ParentCode, out var pid))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                        .WithData("message", $"Parent account '{row.ParentCode}' not found for '{row.AccountCode}'.");
                }
                parentId = pid;
            }

            var accountId = LazyServiceProvider != null ? GuidGenerator.Create() : Guid.NewGuid();
            var account = new Account(
                accountId,
                companyId,
                row.AccountCode,
                row.AccountName,
                row.AccountType,
                tenantId);

            account.ParentAccountId = parentId;
            account.IsGroup = row.IsGroup;
            account.AccountSubType = row.AccountSubType;
            account.BalanceMustBe = row.BalanceMustBe;
            account.Currency = !string.IsNullOrWhiteSpace(row.Currency)
                ? row.Currency
                : (company?.CurrencyCode ?? "MYR");

            await _accountRepository.InsertAsync(account);
            codeToIdMap[row.AccountCode] = account.Id;
            createdAccounts.Add(account);
        }

        // Auto-assign default accounts on company based on created accounts (per ERPNext set_default_accounts)
        if (company != null && _companyRepository != null)
        {
            var leafAccounts = createdAccounts.Where(a => !a.IsGroup).ToList();

            company.DefaultReceivableAccountId = leafAccounts.FirstOrDefault(a => a.AccountSubType == AccountSubType.AccountsReceivable || a.AccountName.Contains("Receivable", StringComparison.OrdinalIgnoreCase))?.Id;
            company.DefaultPayableAccountId = leafAccounts.FirstOrDefault(a => a.AccountSubType == AccountSubType.AccountsPayable || a.AccountName.Contains("Payable", StringComparison.OrdinalIgnoreCase))?.Id;
            company.DefaultIncomeAccountId = leafAccounts.FirstOrDefault(a => a.AccountType == AccountType.Revenue && (a.AccountName.Contains("Sales", StringComparison.OrdinalIgnoreCase) || a.AccountName.Contains("Revenue", StringComparison.OrdinalIgnoreCase)))?.Id;
            company.DefaultExpenseAccountId = leafAccounts.FirstOrDefault(a => a.AccountType == AccountType.Expense && (a.AccountName.Contains("Cost of Goods", StringComparison.OrdinalIgnoreCase) || a.AccountName.Contains("Operating", StringComparison.OrdinalIgnoreCase)))?.Id;
            company.DefaultTaxPayableAccountId = leafAccounts.FirstOrDefault(a => a.AccountSubType == AccountSubType.TaxPayable || a.AccountName.Contains("SST", StringComparison.OrdinalIgnoreCase))?.Id;
            company.DefaultBankAccountId = leafAccounts.FirstOrDefault(a => a.AccountSubType == AccountSubType.BankAccount || a.AccountName.Contains("Bank", StringComparison.OrdinalIgnoreCase))?.Id;
            company.DefaultInventoryAccountId = leafAccounts.FirstOrDefault(a => a.AccountSubType == AccountSubType.Stock || a.AccountName.Contains("Inventory", StringComparison.OrdinalIgnoreCase))?.Id;
            company.DefaultStockAdjustmentAccountId = leafAccounts.FirstOrDefault(a => a.AccountName.Contains("Stock Adjustment", StringComparison.OrdinalIgnoreCase))?.Id;
            company.StockReceivedButNotBilledAccountId = leafAccounts.FirstOrDefault(a => a.AccountName.Contains("Stock Received But Not Billed", StringComparison.OrdinalIgnoreCase))?.Id;
            company.ExchangeGainLossAccountId = leafAccounts.FirstOrDefault(a => a.AccountName.Contains("Exchange Gain", StringComparison.OrdinalIgnoreCase))?.Id;
            company.BankChargesAccountId = leafAccounts.FirstOrDefault(a => a.AccountName.Contains("Bank Charges", StringComparison.OrdinalIgnoreCase) || a.AccountName.Contains("Bank Fee", StringComparison.OrdinalIgnoreCase))?.Id;
            company.DepreciationExpenseAccountId = leafAccounts.FirstOrDefault(a => a.AccountSubType == AccountSubType.DepreciationExpense || a.AccountName.Contains("Depreciation Expense", StringComparison.OrdinalIgnoreCase))?.Id;
            company.AccumulatedDepreciationAccountId = leafAccounts.FirstOrDefault(a => a.AccountSubType == AccountSubType.AccumulatedDepreciation || a.AccountName.Contains("Accumulated Depreciation", StringComparison.OrdinalIgnoreCase))?.Id;
            company.RoundOffAccountId = leafAccounts.FirstOrDefault(a => a.AccountName.Contains("Round Off", StringComparison.OrdinalIgnoreCase))?.Id;

            await _companyRepository.UpdateAsync(company);
        }

        return createdAccounts.Count;
    }

    /// <summary>
    /// Creates the standard Malaysian Chart of Accounts template.
    /// Based on ERPNext's verified/standard_chart_of_accounts.py for Malaysia.
    /// </summary>
    public static List<CoaTemplateRow> GetMalaysianTemplate()
    {
        return new List<CoaTemplateRow>
        {
            // Root accounts
            new("1000", "Assets", AccountType.Asset, isGroup: true),
            new("2000", "Liabilities", AccountType.Liability, isGroup: true),
            new("3000", "Equity", AccountType.Equity, isGroup: true),
            new("4000", "Revenue", AccountType.Revenue, isGroup: true),
            new("5000", "Expenses", AccountType.Expense, isGroup: true),

            // Assets - Current
            new("1100", "Current Assets", AccountType.Asset, isGroup: true, parentCode: "1000"),
            new("1110", "Cash and Cash Equivalents", AccountType.Asset, isGroup: true, parentCode: "1100"),
            new("1111", "Petty Cash", AccountType.Asset, parentCode: "1110", subType: AccountSubType.CashAccount),
            new("1112", "Cash at Bank", AccountType.Asset, parentCode: "1110", subType: AccountSubType.BankAccount),
            new("1120", "Bank Accounts", AccountType.Asset, isGroup: true, parentCode: "1100"),
            new("1121", "Maybank Current Account", AccountType.Asset, parentCode: "1120", subType: AccountSubType.BankAccount),
            new("1130", "Accounts Receivable", AccountType.Asset, parentCode: "1100", subType: AccountSubType.AccountsReceivable),
            new("1140", "Inventory", AccountType.Asset, parentCode: "1100", subType: AccountSubType.Stock),
            new("1150", "Prepaid Expenses", AccountType.Asset, parentCode: "1100"),
            new("1160", "Stock Received But Not Billed", AccountType.Asset, parentCode: "1100"),
            new("1170", "Assets Received But Not Billed", AccountType.Asset, parentCode: "1100"),

            // Assets - Non-Current
            new("1200", "Non-Current Assets", AccountType.Asset, isGroup: true, parentCode: "1000"),
            new("1210", "Property, Plant & Equipment", AccountType.Asset, isGroup: true, parentCode: "1200"),
            new("1211", "Office Equipment", AccountType.Asset, parentCode: "1210", subType: AccountSubType.FixedAsset),
            new("1212", "Computer Equipment", AccountType.Asset, parentCode: "1210", subType: AccountSubType.FixedAsset),
            new("1213", "Motor Vehicles", AccountType.Asset, parentCode: "1210", subType: AccountSubType.FixedAsset),
            new("1220", "Accumulated Depreciation", AccountType.Asset, parentCode: "1200", subType: AccountSubType.AccumulatedDepreciation),
            new("1230", "Capital Work in Progress", AccountType.Asset, parentCode: "1200", subType: AccountSubType.CapitalWorkInProgress),

            // Liabilities - Current
            new("2100", "Current Liabilities", AccountType.Liability, isGroup: true, parentCode: "2000"),
            new("2110", "Accounts Payable", AccountType.Liability, parentCode: "2100", subType: AccountSubType.AccountsPayable),
            new("2120", "SST Payable", AccountType.Liability, parentCode: "2100", subType: AccountSubType.TaxPayable),
            new("2130", "Accrued Expenses", AccountType.Liability, parentCode: "2100"),
            new("2140", "Short-Term Loans", AccountType.Liability, parentCode: "2100"),
            new("2150", "Net Salary Payable", AccountType.Liability, parentCode: "2100"),
            new("2160", "EPF Payable", AccountType.Liability, parentCode: "2100", subType: AccountSubType.TaxPayable),
            new("2170", "SOCSO Payable", AccountType.Liability, parentCode: "2100", subType: AccountSubType.TaxPayable),
            new("2180", "EIS Payable", AccountType.Liability, parentCode: "2100", subType: AccountSubType.TaxPayable),
            new("2190", "PCB/MTD Payable", AccountType.Liability, parentCode: "2100", subType: AccountSubType.TaxPayable),

            // Liabilities - Non-Current
            new("2200", "Non-Current Liabilities", AccountType.Liability, isGroup: true, parentCode: "2000"),
            new("2210", "Long-Term Loans", AccountType.Liability, parentCode: "2200"),

            // Equity
            new("3100", "Share Capital", AccountType.Equity, parentCode: "3000", subType: AccountSubType.ShareCapital),
            new("3200", "Retained Earnings", AccountType.Equity, parentCode: "3000", subType: AccountSubType.RetainedEarnings),
            new("3300", "Current Year Profit/Loss", AccountType.Equity, parentCode: "3000"),

            // Revenue
            new("4100", "Sales Revenue", AccountType.Revenue, parentCode: "4000"),
            new("4200", "Service Revenue", AccountType.Revenue, parentCode: "4000"),
            new("4300", "Other Income", AccountType.Revenue, parentCode: "4000"),
            new("4900", "Exchange Gain/Loss", AccountType.Revenue, parentCode: "4000"),

            // Expenses
            new("5100", "Cost of Goods Sold", AccountType.Expense, parentCode: "5000"),
            new("5200", "Operating Expenses", AccountType.Expense, isGroup: true, parentCode: "5000"),
            new("5210", "Salary & Wages", AccountType.Expense, parentCode: "5200"),
            new("5220", "Employer EPF Contribution", AccountType.Expense, parentCode: "5200"),
            new("5230", "Employer SOCSO Contribution", AccountType.Expense, parentCode: "5200"),
            new("5240", "Employer EIS Contribution", AccountType.Expense, parentCode: "5200"),
            new("5250", "Rent Expense", AccountType.Expense, parentCode: "5200"),
            new("5260", "Utilities", AccountType.Expense, parentCode: "5200"),
            new("5270", "Office Supplies", AccountType.Expense, parentCode: "5200"),
            new("5280", "Professional Fees", AccountType.Expense, parentCode: "5200"),
            new("5290", "Marketing & Advertising", AccountType.Expense, parentCode: "5200"),
            new("5300", "Travel & Transport", AccountType.Expense, parentCode: "5200"),
            new("5400", "Insurance", AccountType.Expense, parentCode: "5200"),
            new("5500", "Depreciation Expense", AccountType.Expense, parentCode: "5000"),
            new("5600", "Stock Adjustment", AccountType.Expense, parentCode: "5000"),
            new("5700", "Write-Off", AccountType.Expense, parentCode: "5000"),
            new("5800", "Round Off", AccountType.Expense, parentCode: "5000"),
        };
    }

    /// <summary>
    /// Topological sort: parents before children based on ParentCode chain.
    /// </summary>
    private static List<CoaTemplateRow> TopologicalSort(IReadOnlyList<CoaTemplateRow> rows)
    {
        var result = new List<CoaTemplateRow>();
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var codeToRow = rows.ToDictionary(r => r.AccountCode, StringComparer.OrdinalIgnoreCase);

        void Visit(CoaTemplateRow row)
        {
            if (processed.Contains(row.AccountCode)) return;

            // Process parent first if it exists
            if (!string.IsNullOrWhiteSpace(row.ParentCode) && codeToRow.TryGetValue(row.ParentCode, out var parent))
            {
                Visit(parent);
            }

            processed.Add(row.AccountCode);
            result.Add(row);
        }

        foreach (var row in rows)
            Visit(row);

        return result;
    }
}

/// <summary>
/// A single row in a Chart of Accounts import template.
/// </summary>
public record CoaTemplateRow(
    string AccountCode,
    string AccountName,
    AccountType AccountType,
    bool IsGroup = false,
    string? ParentCode = null,
    AccountSubType? SubType = null,
    BalanceDirection? BalanceMustBe = null,
    string? Currency = null)
{
    // Convenience: allow setting AccountSubType via constructor alias
    public AccountSubType? AccountSubType => SubType;

    public CoaTemplateRow(string code, string name, AccountType type, bool isGroup = false, string? parentCode = null, AccountSubType? subType = null)
        : this(code, name, type, isGroup, parentCode, subType, null, null) { }
}
