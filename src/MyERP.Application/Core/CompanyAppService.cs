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
using Volo.Abp;
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
    public bool EnablePerpetualInventory { get; set; }
    public decimal OverDeliveryAllowance { get; set; }
    public decimal OverBillingAllowance { get; set; }
    public bool AllowUomWithConversionRateDefinedInItem { get; set; }
    public Guid? DefaultReceivableAccountId { get; set; }
    public Guid? DefaultPayableAccountId { get; set; }
    public Guid? DefaultIncomeAccountId { get; set; }
    public Guid? DefaultExpenseAccountId { get; set; }
    public Guid? DefaultTaxPayableAccountId { get; set; }
    public Guid? DefaultBankAccountId { get; set; }
    public Guid? DefaultInventoryAccountId { get; set; }
    public Guid? DepreciationExpenseAccountId { get; set; }
    public Guid? AccumulatedDepreciationAccountId { get; set; }
    public Guid? ExchangeGainLossAccountId { get; set; }
    public Guid? BankChargesAccountId { get; set; }
    public Guid? DefaultCostCenterId { get; set; }
    public Guid? RoundOffAccountId { get; set; }
    public Guid? RoundOffForOpeningAccountId { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
    public Guid? SampleRetentionWarehouseId { get; set; }
    public Guid? DefaultInTransitWarehouseId { get; set; }
    public Guid? DefaultWarehouseForSalesReturnId { get; set; }
    public Guid? DefaultWipWarehouseId { get; set; }
    public Guid? DefaultFgWarehouseId { get; set; }
    public Guid? DefaultScrapWarehouseId { get; set; }

    // Advance Payment Settings (gotcha #510)
    public bool BookAdvancePaymentsInSeparatePartyAccount { get; set; }
    public Guid? DefaultAdvanceReceivedAccountId { get; set; }
    public Guid? DefaultAdvancePaidAccountId { get; set; }
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
        await ValidateAdvanceAccountsCurrencyAsync(result.Id, input.CurrencyCode, input.DefaultAdvanceReceivedAccountId, input.DefaultAdvancePaidAccountId);
        // Auto-setup the new company with required master data (FY, CoA, warehouses, etc.)
        await SetupNewCompanyAsync(result.Id);
        return result;
    }

    public override async Task<CompanyDto> UpdateAsync(Guid id, CreateUpdateCompanyDto input)
    {
        await ValidateWarehousesAsync(id, input);
        await ValidateAdvanceAccountsCurrencyAsync(id, input.CurrencyCode, input.DefaultAdvanceReceivedAccountId, input.DefaultAdvancePaidAccountId);
        return await base.UpdateAsync(id, input);
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
        entity.AllowUomWithConversionRateDefinedInItem = input.AllowUomWithConversionRateDefinedInItem;

        // Warehouse Defaults (per PR #57571)
        entity.DefaultWarehouseId = input.DefaultWarehouseId;
        entity.SampleRetentionWarehouseId = input.SampleRetentionWarehouseId;
        entity.DefaultInTransitWarehouseId = input.DefaultInTransitWarehouseId;
        entity.DefaultWarehouseForSalesReturnId = input.DefaultWarehouseForSalesReturnId;
        entity.DefaultWipWarehouseId = input.DefaultWipWarehouseId;
        entity.DefaultFgWarehouseId = input.DefaultFgWarehouseId;
        entity.DefaultScrapWarehouseId = input.DefaultScrapWarehouseId;

        // Account Defaults
        entity.DefaultReceivableAccountId = input.DefaultReceivableAccountId;
        entity.DefaultPayableAccountId = input.DefaultPayableAccountId;
        entity.DefaultIncomeAccountId = input.DefaultIncomeAccountId;
        entity.DefaultExpenseAccountId = input.DefaultExpenseAccountId;
        entity.DefaultTaxPayableAccountId = input.DefaultTaxPayableAccountId;
        entity.DefaultBankAccountId = input.DefaultBankAccountId;
        entity.DefaultInventoryAccountId = input.DefaultInventoryAccountId;
        entity.StockReceivedButNotBilledAccountId = input.StockReceivedButNotBilledAccountId;
        entity.StockDeliveredButNotBilledAccountId = input.StockDeliveredButNotBilledAccountId;
        entity.DefaultCostCenterId = input.DefaultCostCenterId;
        entity.RoundOffAccountId = input.RoundOffAccountId;
        entity.RoundOffForOpeningAccountId = input.RoundOffForOpeningAccountId;

        // Advance Payment Defaults (gotcha #510)
        entity.BookAdvancePaymentsInSeparatePartyAccount = input.BookAdvancePaymentsInSeparatePartyAccount;
        entity.DefaultAdvanceReceivedAccountId = input.DefaultAdvanceReceivedAccountId;
        entity.DefaultAdvancePaidAccountId = input.DefaultAdvancePaidAccountId;
    }

    [Authorize(MyERPPermissions.Companies.Edit)]
    public async Task UpdateSettingsAsync(Guid id, UpdateCompanySettingsDto input)
    {
        var company = await Repository.GetAsync(id);

        var currency = !string.IsNullOrWhiteSpace(input.DefaultCurrency) ? input.DefaultCurrency : company.CurrencyCode;
        await ValidateAdvanceAccountsCurrencyAsync(id, currency, input.DefaultAdvanceReceivedAccountId, input.DefaultAdvancePaidAccountId);

        if (!string.IsNullOrWhiteSpace(input.DefaultCurrency))
            company.CurrencyCode = input.DefaultCurrency;
        if (input.FiscalYearStartMonth.HasValue)
            company.FiscalYearStartMonth = input.FiscalYearStartMonth.Value;

        company.StockFrozenUpto = string.IsNullOrWhiteSpace(input.StockFrozenUpto)
            ? null : DateTime.Parse(input.StockFrozenUpto);
        company.AccountsFrozenTillDate = string.IsNullOrWhiteSpace(input.AccountsFrozenTillDate)
            ? null : DateTime.Parse(input.AccountsFrozenTillDate);

        company.OverDeliveryReceiptAllowance = input.OverDeliveryAllowance;
        company.OverBillingAllowance = input.OverBillingAllowance;
        company.EnablePerpetualInventory = input.EnablePerpetualInventory;
        company.AllowUomWithConversionRateDefinedInItem = input.AllowUomWithConversionRateDefinedInItem;

        company.DefaultValuationMethod = string.IsNullOrWhiteSpace(input.DefaultValuationMethod)
            ? null
            : Enum.Parse<MyERP.Inventory.ValuationMethod>(input.DefaultValuationMethod, ignoreCase: true);

        company.DefaultReceivableAccountId = input.DefaultReceivableAccountId;
        company.DefaultPayableAccountId = input.DefaultPayableAccountId;
        company.DefaultIncomeAccountId = input.DefaultIncomeAccountId;
        company.DefaultExpenseAccountId = input.DefaultExpenseAccountId;
        company.DefaultTaxPayableAccountId = input.DefaultTaxPayableAccountId;
        company.DefaultBankAccountId = input.DefaultBankAccountId;
        company.DefaultInventoryAccountId = input.DefaultInventoryAccountId;
        company.DepreciationExpenseAccountId = input.DepreciationExpenseAccountId;
        company.AccumulatedDepreciationAccountId = input.AccumulatedDepreciationAccountId;
        company.ExchangeGainLossAccountId = input.ExchangeGainLossAccountId;
        company.BankChargesAccountId = input.BankChargesAccountId;
        company.DefaultCostCenterId = input.DefaultCostCenterId;
        company.RoundOffAccountId = input.RoundOffAccountId;
        company.RoundOffForOpeningAccountId = input.RoundOffForOpeningAccountId;

        company.BookAdvancePaymentsInSeparatePartyAccount = input.BookAdvancePaymentsInSeparatePartyAccount;
        company.DefaultAdvanceReceivedAccountId = input.DefaultAdvanceReceivedAccountId;
        company.DefaultAdvancePaidAccountId = input.DefaultAdvancePaidAccountId;

        company.DefaultWarehouseId = input.DefaultWarehouseId;
        company.SampleRetentionWarehouseId = input.SampleRetentionWarehouseId;
        company.DefaultInTransitWarehouseId = input.DefaultInTransitWarehouseId;
        company.DefaultWarehouseForSalesReturnId = input.DefaultWarehouseForSalesReturnId;
        company.DefaultWipWarehouseId = input.DefaultWipWarehouseId;
        company.DefaultFgWarehouseId = input.DefaultFgWarehouseId;
        company.DefaultScrapWarehouseId = input.DefaultScrapWarehouseId;

        await Repository.UpdateAsync(company);

        // Invalidate cached settings so next posting reads fresh frozen dates + accounts
        var cache = LazyServiceProvider.LazyGetRequiredService<MyERP.Core.DomainServices.CompanySettingsCache>();
        await cache.InvalidateAsync(id);
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
        var existingCostCenters = (await ccRepo.GetQueryableAsync()).Where(c => c.CompanyId == companyId).ToList();
        CostCenter? mainCostCenter;
        if (existingCostCenters.Count == 0)
        {
            var root = new CostCenter(GuidGenerator.Create(), companyId, company.Name, isGroup: true);
            await ccRepo.InsertAsync(root, autoSave: true);
            mainCostCenter = new CostCenter(GuidGenerator.Create(), companyId, "Main", parentId: root.Id);
            await ccRepo.InsertAsync(mainCostCenter, autoSave: true);
        }
        else
        {
            mainCostCenter = existingCostCenters.FirstOrDefault(c => !c.IsGroup) ?? existingCostCenters.First();
        }

        // Every P&L GL line requires a cost center (AccountingDimensionService.ValidatePlAccountsHaveCostCenterAsync)
        // and falls back to this when the source document doesn't specify one — matches ERPNext's
        // Company.cost_center, which every new company points at its own root cost center by default.
        if (!company.DefaultCostCenterId.HasValue && mainCostCenter != null)
        {
            company.DefaultCostCenterId = mainCostCenter.Id;
            await Repository.UpdateAsync(company, autoSave: true);
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
            if (lookup.TryGetValue("2115", out var srbnb)) company.StockReceivedButNotBilledAccountId = srbnb;
            if (lookup.TryGetValue("2120", out var taxPayable)) company.DefaultTaxPayableAccountId = taxPayable;
            if (lookup.TryGetValue("4100", out var income)) company.DefaultIncomeAccountId = income;
            if (lookup.TryGetValue("5100", out var expense)) company.DefaultExpenseAccountId = expense;
            if (lookup.TryGetValue("1120", out var bank)) company.DefaultBankAccountId = bank;
            if (lookup.TryGetValue("1140", out var inventory)) company.DefaultInventoryAccountId = inventory;
            if (lookup.TryGetValue("5500", out var depr)) company.DepreciationExpenseAccountId = depr;
            if (lookup.TryGetValue("1220", out var accDepr)) company.AccumulatedDepreciationAccountId = accDepr;
            if (lookup.TryGetValue("4900", out var exchangeGl)) company.ExchangeGainLossAccountId = exchangeGl;
            if (lookup.TryGetValue("3900", out var stockAdj)) company.DefaultStockAdjustmentAccountId = stockAdj;
            if (lookup.TryGetValue("1145", out var wip)) company.DefaultWipAccountId = wip;
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
                new AccountingRule(GuidGenerator.Create(), companyId, "SI CR Tax", "SalesInvoice", false, Accounting.AccountSource.TaxPayable, Accounting.AmountSource.TaxAmount) { SortOrder = 3, FixedAccountId = company.DefaultTaxPayableAccountId ?? company.DefaultPayableAccountId },
                new AccountingRule(GuidGenerator.Create(), companyId, "PI DR Expense", "PurchaseInvoice", true, Accounting.AccountSource.ItemExpense, Accounting.AmountSource.NetTotal) { SortOrder = 1 },
                new AccountingRule(GuidGenerator.Create(), companyId, "PI CR Payable", "PurchaseInvoice", false, Accounting.AccountSource.SupplierPayable, Accounting.AmountSource.GrandTotal) { SortOrder = 2 },
                new AccountingRule(GuidGenerator.Create(), companyId, "PE DR Bank", "PaymentEntry", true, Accounting.AccountSource.FixedAccount, Accounting.AmountSource.GrandTotal) { SortOrder = 1, FixedAccountId = company.DefaultBankAccountId },
                new AccountingRule(GuidGenerator.Create(), companyId, "PE CR Receivable", "PaymentEntry", false, Accounting.AccountSource.CustomerReceivable, Accounting.AmountSource.GrandTotal) { SortOrder = 2 },
                new AccountingRule(GuidGenerator.Create(), companyId, "DN DR COGS", "DeliveryNote", true, Accounting.AccountSource.ItemExpense, Accounting.AmountSource.NetTotal) { SortOrder = 1 },
                new AccountingRule(GuidGenerator.Create(), companyId, "DN CR Stock", "DeliveryNote", false, Accounting.AccountSource.FixedAccount, Accounting.AmountSource.NetTotal) { SortOrder = 2, FixedAccountId = company.DefaultInventoryAccountId },
                new AccountingRule(GuidGenerator.Create(), companyId, "PR DR Stock", "PurchaseReceipt", true, Accounting.AccountSource.FixedAccount, Accounting.AmountSource.NetTotal) { SortOrder = 1, FixedAccountId = company.DefaultInventoryAccountId },
                new AccountingRule(GuidGenerator.Create(), companyId, "PR CR SRBNB", "PurchaseReceipt", false, Accounting.AccountSource.FixedAccount, Accounting.AmountSource.NetTotal) { SortOrder = 2, FixedAccountId = company.StockReceivedButNotBilledAccountId ?? company.DefaultPayableAccountId },
            };
            foreach (var rule in rules) await ruleRepo.InsertAsync(rule, autoSave: true);
        }

        // Seed default Document Series — every document type that calls
        // IDocumentNumberGenerator.GenerateAsync(documentType, ...) needs one configured per
        // company, or its Create endpoint fails with DocumentSeriesNotConfigured. Without this,
        // a freshly-provisioned company can create nothing until an admin manually adds a series
        // for each type under Settings > Document Series (discovered repeatedly during live
        // testing — see migration session notes 2026-08-28 rounds 60/62).
        var seriesRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DocumentSeries, Guid>>();
        var existingSeriesTypes = (await seriesRepo.GetQueryableAsync())
            .Where(s => s.CompanyId == companyId)
            .Select(s => s.DocumentType)
            .ToHashSet();

        // (documentType, friendlyName, prefix) — documentType must exactly match the string
        // literal each *AppService passes to GenerateAsync. Some document types have two
        // distinct series by design (a user-facing one and an auxiliary one used only for
        // system-generated documents of the same underlying kind, e.g. reversal/LCV/
        // StockReconciliation journal entries vs. user-created ones) — both need their own row.
        var seriesDefs = new (string DocumentType, string Name, string Prefix)[]
        {
            ("SalesOrder", "Sales Order Numbering", "SO-"),
            ("SalesInvoice", "Sales Invoice Numbering", "SI-"),
            ("Quotation", "Quotation Numbering", "QTN-"),
            ("DeliveryNote", "Delivery Note Numbering", "DN-"),
            ("PurchaseOrder", "Purchase Order Numbering", "PO-"),
            ("PurchaseInvoice", "Purchase Invoice Numbering", "PI-"),
            ("PurchaseReceipt", "Purchase Receipt Numbering", "PR-"),
            ("RFQ", "Request for Quotation Numbering", "RFQ-"),
            ("SQ", "Supplier Quotation Numbering", "SQ-"),
            ("MR", "Material Request Numbering", "MR-"),
            ("StockEntry", "Stock Entry Numbering", "SE-"),
            ("SE", "System Stock Entry Numbering", "SEX-"),
            ("QI", "Quality Inspection Numbering", "QI-"),
            ("JournalEntry", "Journal Entry Numbering", "JV-"),
            ("JE", "System Journal Entry Numbering", "JEX-"),
            ("PaymentEntry", "Payment Entry Numbering", "PE-"),
            ("PaymentOrder", "Payment Order Numbering", "PMO-"),
            ("LCV", "Landed Cost Voucher Numbering", "LCV-"),
            ("Asset", "Asset Numbering", "AST-"),
            ("BOM", "Bill of Materials Numbering", "BOM-"),
            ("WO", "Work Order Numbering", "WO-"),
            ("PP", "Production Plan Numbering", "PP-"),
            ("MPS", "Master Production Schedule Numbering", "MPS-"),
            ("SF", "Sales Forecast Numbering", "SF-"),
            ("SCO", "Subcontracting Order Numbering", "SCO-"),
            ("SCR", "Subcontracting Receipt Numbering", "SCR-"),
            ("SCR-RET", "Subcontracting Receipt Return Numbering", "SCRR-"),
            ("Employee", "Employee Numbering", "EMP-"),
            ("LOAN", "Employee Loan Numbering", "LN-"),
            ("Payroll", "Payroll Numbering", "PAY-"),
            ("Project", "Project Numbering", "PROJ-"),
            ("Contract", "Contract Numbering", "CON-"),
            ("SHIP", "Shipment Numbering", "SHIP-"),
            ("IN", "Installation Note Numbering", "IN-"),
            ("POS", "POS Invoice Numbering", "POS-"),
            ("PRO", "Proforma Invoice Numbering", "PRO-"),
        };

        foreach (var (documentType, name, prefix) in seriesDefs)
        {
            if (existingSeriesTypes.Contains(documentType)) continue;
            await seriesRepo.InsertAsync(
                new DocumentSeries(GuidGenerator.Create(), companyId, name, documentType, prefix, CurrentTenant.Id),
                autoSave: true);
        }
    }

    /// <summary>
    /// One-off repair for companies provisioned before DefaultTaxPayableAccountId/
    /// StockReceivedButNotBilledAccountId existed: their "PR CR SRBNB" and "SI CR Tax"
    /// AccountingRule rows were seeded pointing at DefaultPayableAccountId (see commit
    /// fixing the double-credit-to-Payable bug). Idempotent — creates the missing "2115"
    /// account and repoints only the two rules that are still misconfigured; safe to call
    /// repeatedly or on an already-correct company (no-ops).
    /// </summary>
    [Authorize(MyERPPermissions.Companies.Edit)]
    public async Task RepairSrbnbAndTaxPayableAccountsAsync(Guid companyId)
    {
        var company = await Repository.GetAsync(companyId);
        var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();
        var accounts = (await accountRepo.GetQueryableAsync()).Where(a => a.CompanyId == companyId).ToList();

        if (!company.StockReceivedButNotBilledAccountId.HasValue)
        {
            var srbnbAccount = accounts.FirstOrDefault(a => a.AccountCode == "2115");
            if (srbnbAccount == null)
            {
                var payableAccount = accounts.FirstOrDefault(a => a.Id == company.DefaultPayableAccountId);
                if (payableAccount != null)
                {
                    srbnbAccount = new Account(GuidGenerator.Create(), companyId, "2115",
                        "Stock Received But Not Billed", Accounting.AccountType.Liability, CurrentTenant.Id)
                    {
                        AccountSubType = Accounting.AccountSubType.CurrentLiability,
                        ParentAccountId = payableAccount.ParentAccountId,
                        IsGroup = false,
                    };
                    await accountRepo.InsertAsync(srbnbAccount, autoSave: true);
                }
            }
            if (srbnbAccount != null) company.StockReceivedButNotBilledAccountId = srbnbAccount.Id;
        }

        if (!company.DefaultTaxPayableAccountId.HasValue)
        {
            var taxAccount = accounts.FirstOrDefault(a => a.AccountCode == "2120");
            if (taxAccount != null) company.DefaultTaxPayableAccountId = taxAccount.Id;
        }

        await Repository.UpdateAsync(company, autoSave: true);

        var ruleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountingRule, Guid>>();
        var rules = (await ruleRepo.GetQueryableAsync()).Where(r => r.CompanyId == companyId).ToList();

        var srbnbRule = rules.FirstOrDefault(r => r.Name == "PR CR SRBNB" && r.DocumentType == "PurchaseReceipt");
        if (srbnbRule != null && company.StockReceivedButNotBilledAccountId.HasValue
            && srbnbRule.FixedAccountId == company.DefaultPayableAccountId)
        {
            srbnbRule.FixedAccountId = company.StockReceivedButNotBilledAccountId;
            await ruleRepo.UpdateAsync(srbnbRule, autoSave: true);
        }

        var taxRule = rules.FirstOrDefault(r => r.Name == "SI CR Tax" && r.DocumentType == "SalesInvoice");
        if (taxRule != null && company.DefaultTaxPayableAccountId.HasValue
            && taxRule.FixedAccountId == company.DefaultPayableAccountId)
        {
            taxRule.FixedAccountId = company.DefaultTaxPayableAccountId;
            await ruleRepo.UpdateAsync(taxRule, autoSave: true);
        }
    }

    private async Task ValidateWarehousesAsync(Guid companyId, CreateUpdateCompanyDto input)
    {
        var warehouseIds = new[]
        {
            input.DefaultWarehouseId,
            input.SampleRetentionWarehouseId,
            input.DefaultInTransitWarehouseId,
            input.DefaultWarehouseForSalesReturnId,
            input.DefaultWipWarehouseId,
            input.DefaultFgWarehouseId,
            input.DefaultScrapWarehouseId
        }.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        if (!warehouseIds.Any())
            return;

        var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
        var warehouses = await whRepo.GetListAsync(w => warehouseIds.Contains(w.Id));

        foreach (var wh in warehouses)
        {
            if (wh.CompanyId != companyId)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Warehouse '{wh.Name}' does not belong to company.");

            if (wh.IsGroup)
                throw new BusinessException(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock)
                    .WithData("detail", $"Warehouse '{wh.Name}' is a group warehouse. Default warehouse must be a leaf warehouse.");
        }
    }

    private async Task ValidateAdvanceAccountsCurrencyAsync(Guid companyId, string companyCurrency, Guid? advanceReceivedAccountId, Guid? advancePaidAccountId)
    {
        var accountIds = new[] { advanceReceivedAccountId, advancePaidAccountId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        if (!accountIds.Any()) return;

        var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();
        var accounts = await accountRepo.GetListAsync(a => accountIds.Contains(a.Id));

        foreach (var account in accounts)
        {
            if (account.CompanyId != companyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Account '{account.AccountName}' does not belong to company.");
            }

            if (!string.IsNullOrWhiteSpace(account.Currency) &&
                !string.Equals(account.Currency, companyCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Advance account '{account.AccountName}' currency ({account.Currency}) must match company default currency ({companyCurrency}).");
            }
        }
    }
}
