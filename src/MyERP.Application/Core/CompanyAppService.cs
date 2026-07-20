using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

public class UpdateCompanySettingsDto
{
    public string? DefaultCurrency { get; set; }
    public int? FiscalYearStartMonth { get; set; }
    public string? StockFrozenUpto { get; set; }
    public string? AccountsFrozenTillDate { get; set; }
    public string? DefaultValuationMethod { get; set; }
    public decimal OverDeliveryAllowance { get; set; }
    public decimal OverBillingAllowance { get; set; }
    public Guid? DefaultReceivableAccountId { get; set; }
    public Guid? DefaultPayableAccountId { get; set; }
    public Guid? DefaultIncomeAccountId { get; set; }
    public Guid? DefaultExpenseAccountId { get; set; }
    public Guid? DefaultBankAccountId { get; set; }
    public Guid? DefaultInventoryAccountId { get; set; }
    public Guid? DepreciationExpenseAccountId { get; set; }
    public Guid? AccumulatedDepreciationAccountId { get; set; }
    public Guid? ExchangeGainLossAccountId { get; set; }
}

public class CompanyAppService :
    CrudAppService<
        Company,
        CompanyDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateCompanyDto>,
    ICompanyAppService
{
    public CompanyAppService(IRepository<Company, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Companies.Default;
        GetListPolicyName = MyERPPermissions.Companies.Default;
        CreatePolicyName = MyERPPermissions.Companies.Create;
        UpdatePolicyName = MyERPPermissions.Companies.Edit;
        DeletePolicyName = MyERPPermissions.Companies.Delete;
    }

    public override async Task<CompanyDto> CreateAsync(CreateUpdateCompanyDto input)
    {
        var result = await base.CreateAsync(input);
        // Auto-setup the new company with required master data (FY, CoA, warehouses, etc.)
        await SetupNewCompanyAsync(result.Id);
        return result;
    }

    protected override Company MapToEntity(CreateUpdateCompanyDto input)
    {
        var entity = new Company(
            GuidGenerator.Create(),
            input.Name,
            CurrentTenant.Id);
        MapUpdateFields(input, entity);
        return entity;
    }

    protected override void MapToEntity(CreateUpdateCompanyDto input, Company entity)
    {
        entity.SetName(input.Name);
        MapUpdateFields(input, entity);
    }

    private static void MapUpdateFields(CreateUpdateCompanyDto input, Company entity)
    {
        entity.ShortName = input.ShortName;
        entity.TaxId = input.TaxId;
        entity.RegistrationNumber = input.RegistrationNumber;
        entity.SstRegistrationNumber = input.SstRegistrationNumber;
        entity.MsicCode = input.MsicCode;
        entity.Phone = input.Phone;
        entity.Email = input.Email;
        entity.Website = input.Website;
        entity.Address = input.Address;
        entity.City = input.City;
        entity.State = input.State;
        entity.PostalCode = input.PostalCode;
        entity.Country = input.Country;
        entity.CurrencyCode = input.CurrencyCode;
        entity.FiscalYearStartMonth = input.FiscalYearStartMonth;
        entity.IsActive = input.IsActive;
    }

    [Authorize(MyERPPermissions.Companies.Edit)]
    public async Task UpdateSettingsAsync(Guid id, UpdateCompanySettingsDto input)
    {
        var company = await Repository.GetAsync(id);

        if (!string.IsNullOrWhiteSpace(input.DefaultCurrency))
            company.CurrencyCode = input.DefaultCurrency;
        if (input.FiscalYearStartMonth.HasValue)
            company.FiscalYearStartMonth = input.FiscalYearStartMonth.Value;

        company.StockFrozenUpto = string.IsNullOrWhiteSpace(input.StockFrozenUpto)
            ? null : DateTime.Parse(input.StockFrozenUpto);
        company.AccountsFrozenTillDate = string.IsNullOrWhiteSpace(input.AccountsFrozenTillDate)
            ? null : DateTime.Parse(input.AccountsFrozenTillDate);

        company.DefaultReceivableAccountId = input.DefaultReceivableAccountId;
        company.DefaultPayableAccountId = input.DefaultPayableAccountId;
        company.DefaultIncomeAccountId = input.DefaultIncomeAccountId;
        company.DefaultExpenseAccountId = input.DefaultExpenseAccountId;
        company.DefaultBankAccountId = input.DefaultBankAccountId;
        company.DefaultInventoryAccountId = input.DefaultInventoryAccountId;
        company.DepreciationExpenseAccountId = input.DepreciationExpenseAccountId;
        company.AccumulatedDepreciationAccountId = input.AccumulatedDepreciationAccountId;
        company.ExchangeGainLossAccountId = input.ExchangeGainLossAccountId;

        await Repository.UpdateAsync(company);
    }

    /// <summary>
    /// Sets up default data for a newly created company:
    /// - Default Fiscal Year (current calendar year)
    /// - Default Cost Centers (root + "Main")
    /// - Default Warehouses (Stores, Finished Goods, Work In Progress)
    /// - Manufacturing Settings singleton
    /// 
    /// Call this after creating a new company via the API.
    /// Per ERPNext: company creation auto-generates default accounts, warehouses, and cost centers.
    /// The Chart of Accounts is seeded separately via the CoA importer or default seeder.
    /// </summary>
    [Authorize(MyERPPermissions.Companies.Create)]
    public async Task SetupNewCompanyAsync(Guid companyId)
    {
        var company = await Repository.GetAsync(companyId);

        // Seed Fiscal Year
        var fyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<FiscalYear, Guid>>();
        var hasFy = (await fyRepo.GetQueryableAsync()).Any(f => f.CompanyId == companyId);
        if (!hasFy)
        {
            var year = DateTime.UtcNow.Year;
            var startMonth = company.FiscalYearStartMonth > 0 ? company.FiscalYearStartMonth : 1;
            var fyStart = new DateTime(year, startMonth, 1);
            var fyEnd = fyStart.AddYears(1).AddDays(-1);

            await fyRepo.InsertAsync(new FiscalYear(
                GuidGenerator.Create(), companyId,
                $"FY {fyStart:yyyy}-{fyEnd:yyyy}",
                fyStart, fyEnd), autoSave: true);
        }

        // Seed Cost Centers
        var ccRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CostCenter, Guid>>();
        var hasCc = (await ccRepo.GetQueryableAsync()).Any(c => c.CompanyId == companyId);
        if (!hasCc)
        {
            var root = new CostCenter(GuidGenerator.Create(), companyId, company.Name, isGroup: true);
            await ccRepo.InsertAsync(root, autoSave: true);
            await ccRepo.InsertAsync(new CostCenter(GuidGenerator.Create(), companyId, "Main", parentId: root.Id), autoSave: true);
        }

        // Seed Default Warehouses (hierarchy per ERPNext Company.create_default_warehouses)
        var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();
        var hasWh = (await whRepo.GetQueryableAsync()).Any(w => w.CompanyId == companyId);
        if (!hasWh)
        {
            var allWarehouses = new Warehouse(GuidGenerator.Create(), companyId, "All Warehouses")
                { IsGroup = true, IsActive = true };
            await whRepo.InsertAsync(allWarehouses, autoSave: true);

            await whRepo.InsertAsync(new Warehouse(GuidGenerator.Create(), companyId, "Stores")
                { ParentWarehouseId = allWarehouses.Id, IsActive = true }, autoSave: true);
            await whRepo.InsertAsync(new Warehouse(GuidGenerator.Create(), companyId, "Finished Goods")
                { ParentWarehouseId = allWarehouses.Id, IsActive = true }, autoSave: true);
            await whRepo.InsertAsync(new Warehouse(GuidGenerator.Create(), companyId, "Work In Progress")
                { ParentWarehouseId = allWarehouses.Id, IsActive = true }, autoSave: true);
            await whRepo.InsertAsync(new Warehouse(GuidGenerator.Create(), companyId, "Goods In Transit")
                { ParentWarehouseId = allWarehouses.Id, IsActive = true }, autoSave: true);
        }

        // Seed Manufacturing Settings
        var mfgRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Manufacturing.Entities.ManufacturingSettings, Guid>>();
        var hasMfg = (await mfgRepo.GetQueryableAsync()).Any(s => s.CompanyId == companyId);
        if (!hasMfg)
        {
            await mfgRepo.InsertAsync(new Manufacturing.Entities.ManufacturingSettings(
                GuidGenerator.Create(), companyId), autoSave: true);
        }

        // Seed Chart of Accounts + assign default accounts
        var coaSeeder = LazyServiceProvider.LazyGetRequiredService<Data.MalaysianCoaSeeder>();
        var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();
        var hasAccounts = (await accountRepo.GetQueryableAsync()).Any(a => a.CompanyId == companyId);
        if (!hasAccounts)
        {
            await coaSeeder.SeedAsync(companyId);
            // Assign default accounts from seeded CoA
            var accounts = (await accountRepo.GetQueryableAsync())
                .Where(a => a.CompanyId == companyId).ToList();
            var lookup = accounts.ToDictionary(a => a.AccountCode ?? "", a => a.Id);
            if (lookup.TryGetValue("1130", out var receivable)) company.DefaultReceivableAccountId = receivable;
            if (lookup.TryGetValue("2110", out var payable)) company.DefaultPayableAccountId = payable;
            if (lookup.TryGetValue("4100", out var income)) company.DefaultIncomeAccountId = income;
            if (lookup.TryGetValue("5100", out var expense)) company.DefaultExpenseAccountId = expense;
            if (lookup.TryGetValue("1120", out var bank)) company.DefaultBankAccountId = bank;
            if (lookup.TryGetValue("1140", out var inventory)) company.DefaultInventoryAccountId = inventory;
            if (lookup.TryGetValue("5500", out var depr)) company.DepreciationExpenseAccountId = depr;
            if (lookup.TryGetValue("1220", out var accDepr)) company.AccumulatedDepreciationAccountId = accDepr;
            if (lookup.TryGetValue("4900", out var exchangeGl)) company.ExchangeGainLossAccountId = exchangeGl;
            await Repository.UpdateAsync(company, autoSave: true);
        }

        // Seed default GL posting rules (11 rules per company)
        var ruleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountingRule, Guid>>();
        var hasRules = (await ruleRepo.GetQueryableAsync()).Any(r => r.CompanyId == companyId);
        if (!hasRules)
        {
            var rules = new[]
            {
                new AccountingRule(GuidGenerator.Create(), companyId, "SI DR Receivable", "SalesInvoice", true, Accounting.AccountSource.CustomerReceivable, Accounting.AmountSource.GrandTotal) { SortOrder = 1 },
                new AccountingRule(GuidGenerator.Create(), companyId, "SI CR Revenue", "SalesInvoice", false, Accounting.AccountSource.ItemIncome, Accounting.AmountSource.NetTotal) { SortOrder = 2 },
                new AccountingRule(GuidGenerator.Create(), companyId, "SI CR Tax", "SalesInvoice", false, Accounting.AccountSource.TaxPayable, Accounting.AmountSource.TaxAmount) { SortOrder = 3, FixedAccountId = company.DefaultPayableAccountId },
                new AccountingRule(GuidGenerator.Create(), companyId, "PI DR Expense", "PurchaseInvoice", true, Accounting.AccountSource.ItemExpense, Accounting.AmountSource.NetTotal) { SortOrder = 1 },
                new AccountingRule(GuidGenerator.Create(), companyId, "PI CR Payable", "PurchaseInvoice", false, Accounting.AccountSource.SupplierPayable, Accounting.AmountSource.GrandTotal) { SortOrder = 2 },
                new AccountingRule(GuidGenerator.Create(), companyId, "PE DR Bank", "PaymentEntry", true, Accounting.AccountSource.FixedAccount, Accounting.AmountSource.GrandTotal) { SortOrder = 1, FixedAccountId = company.DefaultBankAccountId },
                new AccountingRule(GuidGenerator.Create(), companyId, "PE CR Receivable", "PaymentEntry", false, Accounting.AccountSource.CustomerReceivable, Accounting.AmountSource.GrandTotal) { SortOrder = 2 },
                new AccountingRule(GuidGenerator.Create(), companyId, "DN DR COGS", "DeliveryNote", true, Accounting.AccountSource.ItemExpense, Accounting.AmountSource.NetTotal) { SortOrder = 1 },
                new AccountingRule(GuidGenerator.Create(), companyId, "DN CR Stock", "DeliveryNote", false, Accounting.AccountSource.FixedAccount, Accounting.AmountSource.NetTotal) { SortOrder = 2, FixedAccountId = company.DefaultInventoryAccountId },
                new AccountingRule(GuidGenerator.Create(), companyId, "PR DR Stock", "PurchaseReceipt", true, Accounting.AccountSource.FixedAccount, Accounting.AmountSource.NetTotal) { SortOrder = 1, FixedAccountId = company.DefaultInventoryAccountId },
                new AccountingRule(GuidGenerator.Create(), companyId, "PR CR SRBNB", "PurchaseReceipt", false, Accounting.AccountSource.FixedAccount, Accounting.AmountSource.NetTotal) { SortOrder = 2, FixedAccountId = company.DefaultPayableAccountId },
            };
            foreach (var rule in rules) await ruleRepo.InsertAsync(rule, autoSave: true);
        }
    }
}
