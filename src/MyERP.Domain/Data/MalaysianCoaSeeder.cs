using System;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace MyERP.Data;

/// <summary>
/// Seeds a standard Malaysian Chart of Accounts for a given company.
/// Based on MFRS/MPERS requirements.
/// </summary>
public class MalaysianCoaSeeder : ITransientDependency
{
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IGuidGenerator _guidGenerator;

    public MalaysianCoaSeeder(
        IRepository<Account, Guid> accountRepository,
        IGuidGenerator guidGenerator)
    {
        _accountRepository = accountRepository;
        _guidGenerator = guidGenerator;
    }

    public async Task SeedAsync(Guid companyId, Guid? tenantId = null)
    {
        if (await _accountRepository.AnyAsync(a => a.CompanyId == companyId))
            return;

        // Assets
        var assets = await CreateGroup(companyId, "1000", "Assets", AccountType.Asset, null, tenantId);
        var currentAssets = await CreateGroup(companyId, "1100", "Current Assets", AccountType.Asset, assets.Id, tenantId);
        await CreateAccount(companyId, "1110", "Cash on Hand", AccountType.Asset, AccountSubType.CashAccount, currentAssets.Id, tenantId);
        await CreateAccount(companyId, "1120", "Bank Accounts", AccountType.Asset, AccountSubType.BankAccount, currentAssets.Id, tenantId);
        await CreateAccount(companyId, "1130", "Accounts Receivable", AccountType.Asset, AccountSubType.AccountsReceivable, currentAssets.Id, tenantId);
        await CreateAccount(companyId, "1140", "Inventory", AccountType.Asset, AccountSubType.CurrentAsset, currentAssets.Id, tenantId);
        await CreateAccount(companyId, "1150", "Prepaid Expenses", AccountType.Asset, AccountSubType.CurrentAsset, currentAssets.Id, tenantId);

        var fixedAssets = await CreateGroup(companyId, "1200", "Fixed Assets", AccountType.Asset, assets.Id, tenantId);
        await CreateAccount(companyId, "1210", "Property, Plant & Equipment", AccountType.Asset, AccountSubType.FixedAsset, fixedAssets.Id, tenantId);
        await CreateAccount(companyId, "1220", "Accumulated Depreciation", AccountType.Asset, AccountSubType.FixedAsset, fixedAssets.Id, tenantId);

        // Liabilities
        var liabilities = await CreateGroup(companyId, "2000", "Liabilities", AccountType.Liability, null, tenantId);
        var currentLiab = await CreateGroup(companyId, "2100", "Current Liabilities", AccountType.Liability, liabilities.Id, tenantId);
        await CreateAccount(companyId, "2110", "Accounts Payable", AccountType.Liability, AccountSubType.AccountsPayable, currentLiab.Id, tenantId);
        await CreateAccount(companyId, "2120", "SST Payable", AccountType.Liability, AccountSubType.TaxPayable, currentLiab.Id, tenantId);
        await CreateAccount(companyId, "2130", "EPF Payable", AccountType.Liability, AccountSubType.CurrentLiability, currentLiab.Id, tenantId);
        await CreateAccount(companyId, "2140", "SOCSO Payable", AccountType.Liability, AccountSubType.CurrentLiability, currentLiab.Id, tenantId);
        await CreateAccount(companyId, "2150", "PCB/MTD Payable", AccountType.Liability, AccountSubType.CurrentLiability, currentLiab.Id, tenantId);
        await CreateAccount(companyId, "2160", "Accrued Expenses", AccountType.Liability, AccountSubType.CurrentLiability, currentLiab.Id, tenantId);

        var longTermLiab = await CreateGroup(companyId, "2200", "Long-Term Liabilities", AccountType.Liability, liabilities.Id, tenantId);
        await CreateAccount(companyId, "2210", "Bank Loans", AccountType.Liability, AccountSubType.LongTermLiability, longTermLiab.Id, tenantId);

        // Equity
        var equity = await CreateGroup(companyId, "3000", "Equity", AccountType.Equity, null, tenantId);
        await CreateAccount(companyId, "3100", "Share Capital", AccountType.Equity, AccountSubType.ShareCapital, equity.Id, tenantId);
        await CreateAccount(companyId, "3200", "Retained Earnings", AccountType.Equity, AccountSubType.RetainedEarnings, equity.Id, tenantId);

        // Revenue
        var revenue = await CreateGroup(companyId, "4000", "Revenue", AccountType.Revenue, null, tenantId);
        await CreateAccount(companyId, "4100", "Sales Revenue", AccountType.Revenue, AccountSubType.OperatingRevenue, revenue.Id, tenantId);
        await CreateAccount(companyId, "4200", "Service Revenue", AccountType.Revenue, AccountSubType.OperatingRevenue, revenue.Id, tenantId);
        await CreateAccount(companyId, "4900", "Other Income", AccountType.Revenue, AccountSubType.OtherIncome, revenue.Id, tenantId);

        // Expenses
        var expenses = await CreateGroup(companyId, "5000", "Expenses", AccountType.Expense, null, tenantId);
        await CreateAccount(companyId, "5100", "Cost of Goods Sold", AccountType.Expense, AccountSubType.CostOfGoodsSold, expenses.Id, tenantId);
        await CreateAccount(companyId, "5200", "Salaries & Wages", AccountType.Expense, AccountSubType.OperatingExpense, expenses.Id, tenantId);
        await CreateAccount(companyId, "5210", "EPF Expense (Employer)", AccountType.Expense, AccountSubType.OperatingExpense, expenses.Id, tenantId);
        await CreateAccount(companyId, "5220", "SOCSO Expense (Employer)", AccountType.Expense, AccountSubType.OperatingExpense, expenses.Id, tenantId);
        await CreateAccount(companyId, "5300", "Rent Expense", AccountType.Expense, AccountSubType.OperatingExpense, expenses.Id, tenantId);
        await CreateAccount(companyId, "5400", "Utilities Expense", AccountType.Expense, AccountSubType.OperatingExpense, expenses.Id, tenantId);
        await CreateAccount(companyId, "5500", "Depreciation Expense", AccountType.Expense, AccountSubType.DepreciationExpense, expenses.Id, tenantId);
        await CreateAccount(companyId, "5900", "Other Expenses", AccountType.Expense, AccountSubType.OperatingExpense, expenses.Id, tenantId);
    }

    private async Task<Account> CreateGroup(Guid companyId, string code, string name, AccountType type, Guid? parentId, Guid? tenantId)
    {
        var account = new Account(_guidGenerator.Create(), companyId, code, name, type, tenantId)
        {
            IsGroup = true,
            ParentAccountId = parentId
        };
        return await _accountRepository.InsertAsync(account, autoSave: true);
    }

    private async Task<Account> CreateAccount(Guid companyId, string code, string name, AccountType type, AccountSubType subType, Guid? parentId, Guid? tenantId)
    {
        var account = new Account(_guidGenerator.Create(), companyId, code, name, type, tenantId)
        {
            AccountSubType = subType,
            ParentAccountId = parentId,
            IsGroup = false
        };
        return await _accountRepository.InsertAsync(account, autoSave: true);
    }
}
