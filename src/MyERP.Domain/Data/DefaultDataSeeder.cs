using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace MyERP.Data;

/// <summary>
/// Seeds default master data required for the ERP to function.
/// Runs after MalaysiaDataSeederContributor.
/// Creates: Item Groups, Modes of Payment, Price Lists, Salary Components,
/// Fiscal Year, Cost Center, Warehouses.
/// </summary>
public class DefaultDataSeeder : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;
    private readonly IRepository<ModeOfPayment, Guid> _mopRepository;
    private readonly IRepository<PriceList, Guid> _priceListRepository;
    private readonly IRepository<SalaryComponent, Guid> _salaryComponentRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<CostCenter, Guid> _costCenterRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<PaymentTermsTemplate, Guid> _paymentTermsRepository;
    private readonly IRepository<LeaveType, Guid> _leaveTypeRepository;
    private readonly IRepository<HolidayList, Guid> _holidayListRepository;
    private readonly IRepository<AccountingRule, Guid> _accountingRuleRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<Manufacturing.Entities.ManufacturingSettings, Guid> _mfgSettingsRepository;
    private readonly IRepository<Territory, Guid> _territoryRepository;
    private readonly IRepository<CustomerGroup, Guid> _customerGroupRepository;
    private readonly IRepository<SupplierGroup, Guid> _supplierGroupRepository;
    private readonly IRepository<Uom, Guid> _uomRepository;
    private readonly IRepository<UomConversion, Guid> _uomConversionRepository;
    private readonly IRepository<CurrencyExchange, Guid> _currencyExchangeRepository;
    private readonly MalaysianCoaSeeder _coaSeeder;
    private readonly IGuidGenerator _guidGenerator;

    public DefaultDataSeeder(
        IRepository<ItemGroup, Guid> itemGroupRepository,
        IRepository<ModeOfPayment, Guid> mopRepository,
        IRepository<PriceList, Guid> priceListRepository,
        IRepository<SalaryComponent, Guid> salaryComponentRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<CostCenter, Guid> costCenterRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<PaymentTermsTemplate, Guid> paymentTermsRepository,
        IRepository<LeaveType, Guid> leaveTypeRepository,
        IRepository<HolidayList, Guid> holidayListRepository,
        IRepository<AccountingRule, Guid> accountingRuleRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<Manufacturing.Entities.ManufacturingSettings, Guid> mfgSettingsRepository,
        IRepository<Territory, Guid> territoryRepository,
        IRepository<CustomerGroup, Guid> customerGroupRepository,
        IRepository<SupplierGroup, Guid> supplierGroupRepository,
        IRepository<Uom, Guid> uomRepository,
        IRepository<UomConversion, Guid> uomConversionRepository,
        IRepository<CurrencyExchange, Guid> currencyExchangeRepository,
        MalaysianCoaSeeder coaSeeder,
        IGuidGenerator guidGenerator)
    {
        _itemGroupRepository = itemGroupRepository;
        _mopRepository = mopRepository;
        _priceListRepository = priceListRepository;
        _salaryComponentRepository = salaryComponentRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _costCenterRepository = costCenterRepository;
        _warehouseRepository = warehouseRepository;
        _companyRepository = companyRepository;
        _paymentTermsRepository = paymentTermsRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _holidayListRepository = holidayListRepository;
        _accountingRuleRepository = accountingRuleRepository;
        _accountRepository = accountRepository;
        _mfgSettingsRepository = mfgSettingsRepository;
        _territoryRepository = territoryRepository;
        _customerGroupRepository = customerGroupRepository;
        _supplierGroupRepository = supplierGroupRepository;
        _uomRepository = uomRepository;
        _uomConversionRepository = uomConversionRepository;
        _currencyExchangeRepository = currencyExchangeRepository;
        _coaSeeder = coaSeeder;
        _guidGenerator = guidGenerator;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        await SeedItemGroupsAsync();
        await SeedModesOfPaymentAsync();
        await SeedPriceListsAsync();
        await SeedSalaryComponentsAsync();
        await SeedPaymentTermsAsync();
        await SeedLeaveTypesAsync();
        await SeedDefaultCompanyDataAsync();
        await SeedHolidaysAsync();
        await SeedAccountingRulesAsync();
        await SeedTerritoriesAsync();
        await SeedCustomerGroupsAsync();
        await SeedSupplierGroupsAsync();
        await SeedUomDataAsync();
        await SeedPeggedCurrenciesAsync();
    }

    private async Task SeedItemGroupsAsync()
    {
        if (await _itemGroupRepository.GetCountAsync() > 0) return;

        var root = new ItemGroup(_guidGenerator.Create(), "All Item Groups", isGroup: true);
        await _itemGroupRepository.InsertAsync(root, autoSave: true);

        await _itemGroupRepository.InsertAsync(new ItemGroup(_guidGenerator.Create(), "Products", parentId: root.Id), autoSave: true);
        await _itemGroupRepository.InsertAsync(new ItemGroup(_guidGenerator.Create(), "Raw Material", parentId: root.Id), autoSave: true);
        await _itemGroupRepository.InsertAsync(new ItemGroup(_guidGenerator.Create(), "Services", parentId: root.Id), autoSave: true);
        await _itemGroupRepository.InsertAsync(new ItemGroup(_guidGenerator.Create(), "Sub Assemblies", parentId: root.Id), autoSave: true);
        await _itemGroupRepository.InsertAsync(new ItemGroup(_guidGenerator.Create(), "Consumable", parentId: root.Id), autoSave: true);
    }

    private async Task SeedModesOfPaymentAsync()
    {
        if (await _mopRepository.GetCountAsync() > 0) return;

        await _mopRepository.InsertAsync(new ModeOfPayment(_guidGenerator.Create(), "Cash", "Cash"), autoSave: true);
        await _mopRepository.InsertAsync(new ModeOfPayment(_guidGenerator.Create(), "Credit Card", "Bank"), autoSave: true);
        await _mopRepository.InsertAsync(new ModeOfPayment(_guidGenerator.Create(), "Wire Transfer", "Bank"), autoSave: true);
        await _mopRepository.InsertAsync(new ModeOfPayment(_guidGenerator.Create(), "Bank Draft", "Bank"), autoSave: true);
        await _mopRepository.InsertAsync(new ModeOfPayment(_guidGenerator.Create(), "Cheque", "Bank"), autoSave: true);
    }

    private async Task SeedPriceListsAsync()
    {
        if (await _priceListRepository.GetCountAsync() > 0) return;

        await _priceListRepository.InsertAsync(
            new PriceList(_guidGenerator.Create(), "Standard Selling", "MYR", isSelling: true, isBuying: false) { IsDefault = true },
            autoSave: true);
        await _priceListRepository.InsertAsync(
            new PriceList(_guidGenerator.Create(), "Standard Buying", "MYR", isSelling: false, isBuying: true) { IsDefault = true },
            autoSave: true);
    }

    private async Task SeedSalaryComponentsAsync()
    {
        if (await _salaryComponentRepository.GetCountAsync() > 0) return;

        // Earnings
        await _salaryComponentRepository.InsertAsync(new SalaryComponent(
            _guidGenerator.Create(), "Basic Salary", SalaryComponentType.Earning)
            { Abbreviation = "B", IsTaxApplicable = true }, autoSave: true);
        await _salaryComponentRepository.InsertAsync(new SalaryComponent(
            _guidGenerator.Create(), "Housing Allowance", SalaryComponentType.Earning)
            { Abbreviation = "HRA", IsTaxApplicable = true }, autoSave: true);
        await _salaryComponentRepository.InsertAsync(new SalaryComponent(
            _guidGenerator.Create(), "Transport Allowance", SalaryComponentType.Earning)
            { Abbreviation = "TA", IsTaxApplicable = true }, autoSave: true);
        await _salaryComponentRepository.InsertAsync(new SalaryComponent(
            _guidGenerator.Create(), "Overtime", SalaryComponentType.Earning)
            { Abbreviation = "OT", IsTaxApplicable = true, DependsOnPaymentDays = false }, autoSave: true);

        // Statutory Deductions
        await _salaryComponentRepository.InsertAsync(new SalaryComponent(
            _guidGenerator.Create(), "EPF Employee", SalaryComponentType.Deduction)
            { Abbreviation = "EPFE", IsStatutory = true }, autoSave: true);
        await _salaryComponentRepository.InsertAsync(new SalaryComponent(
            _guidGenerator.Create(), "EPF Employer", SalaryComponentType.Deduction)
            { Abbreviation = "EPFR", IsStatutory = true }, autoSave: true);
        await _salaryComponentRepository.InsertAsync(new SalaryComponent(
            _guidGenerator.Create(), "SOCSO Employee", SalaryComponentType.Deduction)
            { Abbreviation = "SOCE", IsStatutory = true }, autoSave: true);
        await _salaryComponentRepository.InsertAsync(new SalaryComponent(
            _guidGenerator.Create(), "SOCSO Employer", SalaryComponentType.Deduction)
            { Abbreviation = "SOCR", IsStatutory = true }, autoSave: true);
        await _salaryComponentRepository.InsertAsync(new SalaryComponent(
            _guidGenerator.Create(), "EIS Employee", SalaryComponentType.Deduction)
            { Abbreviation = "EISE", IsStatutory = true }, autoSave: true);
        await _salaryComponentRepository.InsertAsync(new SalaryComponent(
            _guidGenerator.Create(), "EIS Employer", SalaryComponentType.Deduction)
            { Abbreviation = "EISR", IsStatutory = true }, autoSave: true);
        await _salaryComponentRepository.InsertAsync(new SalaryComponent(
            _guidGenerator.Create(), "PCB/MTD", SalaryComponentType.Deduction)
            { Abbreviation = "PCB", IsStatutory = true }, autoSave: true);
    }

    private async Task SeedPaymentTermsAsync()
    {
        if (await _paymentTermsRepository.GetCountAsync() > 0) return;

        // Due on Receipt
        var dueOnReceipt = new PaymentTermsTemplate(_guidGenerator.Create(), "Due on Receipt");
        dueOnReceipt.AddTerm(new PaymentTerm(_guidGenerator.Create(), dueOnReceipt.Id, 100m, 0, "Due on Receipt"));
        await _paymentTermsRepository.InsertAsync(dueOnReceipt, autoSave: true);

        // Net 30
        var net30 = new PaymentTermsTemplate(_guidGenerator.Create(), "Net 30");
        net30.AddTerm(new PaymentTerm(_guidGenerator.Create(), net30.Id, 100m, 30, "Net 30"));
        await _paymentTermsRepository.InsertAsync(net30, autoSave: true);

        // Net 60
        var net60 = new PaymentTermsTemplate(_guidGenerator.Create(), "Net 60");
        net60.AddTerm(new PaymentTerm(_guidGenerator.Create(), net60.Id, 100m, 60, "Net 60"));
        await _paymentTermsRepository.InsertAsync(net60, autoSave: true);

        // 50% Advance + 50% Net 30
        var split = new PaymentTermsTemplate(_guidGenerator.Create(), "50% Advance, 50% Net 30");
        split.AddTerm(new PaymentTerm(_guidGenerator.Create(), split.Id, 50m, 0, "Advance"));
        split.AddTerm(new PaymentTerm(_guidGenerator.Create(), split.Id, 50m, 30, "Balance"));
        await _paymentTermsRepository.InsertAsync(split, autoSave: true);
    }

    private async Task SeedDefaultCompanyDataAsync()
    {
        var companies = await _companyRepository.GetListAsync();
        foreach (var company in companies)
        {
            // Seed Chart of Accounts
            await _coaSeeder.SeedAsync(company.Id);

            // Assign default accounts from seeded CoA
            await AssignDefaultAccountsAsync(company);

            // Seed Default Fiscal Year (if none exists for this company)
            if (!await _fiscalYearRepository.AnyAsync(f => f.CompanyId == company.Id))
            {
                var currentYear = DateTime.UtcNow.Year;
                var fy = new FiscalYear(
                    _guidGenerator.Create(), company.Id,
                    $"FY {currentYear}-{currentYear + 1}",
                    new DateTime(currentYear, 1, 1),
                    new DateTime(currentYear, 12, 31));
                await _fiscalYearRepository.InsertAsync(fy, autoSave: true);
            }

            // Seed Default Cost Center
            if (!await _costCenterRepository.AnyAsync(c => c.CompanyId == company.Id))
            {
                var root = new CostCenter(_guidGenerator.Create(), company.Id, company.Name, isGroup: true);
                await _costCenterRepository.InsertAsync(root, autoSave: true);

                var main = new CostCenter(_guidGenerator.Create(), company.Id, "Main", parentId: root.Id);
                await _costCenterRepository.InsertAsync(main, autoSave: true);
            }

            // Seed Default Warehouses (hierarchy per ERPNext Company.create_default_warehouses)
            if (!await _warehouseRepository.AnyAsync(w => w.CompanyId == company.Id))
            {
                // Root group warehouse (parent of all others)
                var allWarehouses = new Warehouse(
                    _guidGenerator.Create(), company.Id, "All Warehouses") { IsGroup = true, IsActive = true };
                await _warehouseRepository.InsertAsync(allWarehouses, autoSave: true);

                // Child warehouses under root
                await _warehouseRepository.InsertAsync(new Warehouse(
                    _guidGenerator.Create(), company.Id, "Stores")
                    { ParentWarehouseId = allWarehouses.Id, IsActive = true }, autoSave: true);
                await _warehouseRepository.InsertAsync(new Warehouse(
                    _guidGenerator.Create(), company.Id, "Finished Goods")
                    { ParentWarehouseId = allWarehouses.Id, IsActive = true }, autoSave: true);
                await _warehouseRepository.InsertAsync(new Warehouse(
                    _guidGenerator.Create(), company.Id, "Work In Progress")
                    { ParentWarehouseId = allWarehouses.Id, IsActive = true }, autoSave: true);
                await _warehouseRepository.InsertAsync(new Warehouse(
                    _guidGenerator.Create(), company.Id, "Goods In Transit")
                    { ParentWarehouseId = allWarehouses.Id, IsActive = true }, autoSave: true);
            }

            // Seed Manufacturing Settings (per-company singleton)
            if (!await _mfgSettingsRepository.AnyAsync(s => s.CompanyId == company.Id))
            {
                await _mfgSettingsRepository.InsertAsync(
                    new Manufacturing.Entities.ManufacturingSettings(
                        _guidGenerator.Create(), company.Id), autoSave: true);
            }
        }
    }

    private async Task AssignDefaultAccountsAsync(Company company)
    {
        if (company.DefaultReceivableAccountId.HasValue) return; // Already assigned

        var accounts = (await _accountRepository.GetQueryableAsync())
            .Where(a => a.CompanyId == company.Id)
            .ToList();

        Account? FindByCode(string code) => accounts.FirstOrDefault(a => a.AccountCode == code);

        company.DefaultReceivableAccountId = FindByCode("1130")?.Id;    // Accounts Receivable
        company.DefaultPayableAccountId = FindByCode("2110")?.Id;       // Accounts Payable
        company.DefaultIncomeAccountId = FindByCode("4100")?.Id;        // Sales Revenue
        company.DefaultExpenseAccountId = FindByCode("5100")?.Id;       // Cost of Goods Sold
        company.DefaultBankAccountId = FindByCode("1120")?.Id;          // Bank Accounts
        company.DefaultInventoryAccountId = FindByCode("1140")?.Id;     // Inventory
        company.DepreciationExpenseAccountId = FindByCode("5500")?.Id;  // Depreciation Expense
        company.AccumulatedDepreciationAccountId = FindByCode("1220")?.Id; // Accumulated Depreciation
        company.ExchangeGainLossAccountId = FindByCode("4900")?.Id          // Exchange Gain/Loss
            ?? FindByCode("7100")?.Id;                                        // Fallback: Other Income

        await _companyRepository.UpdateAsync(company, autoSave: true);
    }

    private async Task SeedLeaveTypesAsync()
    {
        if (await _leaveTypeRepository.GetCountAsync() > 0) return;

        // Malaysian standard leave types (Employment Act 1955)
        await _leaveTypeRepository.InsertAsync(new LeaveType(
            _guidGenerator.Create(), "Annual Leave", 12) { IsPaidLeave = true, AllowCarryForward = true, MaxCarryForwardDays = 5 }, autoSave: true);
        await _leaveTypeRepository.InsertAsync(new LeaveType(
            _guidGenerator.Create(), "Sick Leave", 14) { IsPaidLeave = true }, autoSave: true);
        await _leaveTypeRepository.InsertAsync(new LeaveType(
            _guidGenerator.Create(), "Hospitalization Leave", 60) { IsPaidLeave = true }, autoSave: true);
        await _leaveTypeRepository.InsertAsync(new LeaveType(
            _guidGenerator.Create(), "Maternity Leave", 98) { IsPaidLeave = true }, autoSave: true);
        await _leaveTypeRepository.InsertAsync(new LeaveType(
            _guidGenerator.Create(), "Paternity Leave", 7) { IsPaidLeave = true }, autoSave: true);
        await _leaveTypeRepository.InsertAsync(new LeaveType(
            _guidGenerator.Create(), "Unpaid Leave", 30) { IsPaidLeave = false, AllowNegativeBalance = true }, autoSave: true);
        await _leaveTypeRepository.InsertAsync(new LeaveType(
            _guidGenerator.Create(), "Compassionate Leave", 3) { IsPaidLeave = true }, autoSave: true);
    }

    private async Task SeedHolidaysAsync()
    {
        if (await _holidayListRepository.GetCountAsync() > 0) return;

        var companies = await _companyRepository.GetListAsync();
        var currentYear = DateTime.UtcNow.Year;

        foreach (var company in companies)
        {
            var list = new HolidayList(
                _guidGenerator.Create(), company.Id,
                $"Malaysia Public Holidays {currentYear}", currentYear)
            { IsDefault = true, WeeklyOff = "Saturday,Sunday" };

            // Malaysian public holidays (gazetted)
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 1, 1), "New Year's Day"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 2, 1), "Federal Territory Day"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 2, 1), "Thaipusam"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 4, 10), "Hari Raya Aidilfitri"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 4, 11), "Hari Raya Aidilfitri (2nd Day)"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 5, 1), "Labour Day"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 5, 12), "Wesak Day"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 6, 3), "Yang di-Pertuan Agong Birthday"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 6, 17), "Hari Raya Haji"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 7, 7), "Awal Muharram"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 8, 31), "Merdeka Day"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 9, 16), "Malaysia Day"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 9, 15), "Mawlid Nabi"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 10, 31), "Deepavali"));
            list.AddHoliday(new Holiday(_guidGenerator.Create(), list.Id, new DateTime(currentYear, 12, 25), "Christmas Day"));

            await _holidayListRepository.InsertAsync(list, autoSave: true);
        }
    }

    /// <summary>
    /// Seeds default GL posting rules for core document types.
    /// These rules drive the AccountingRuleEngine to create proper journal entries.
    /// Rules follow standard double-entry accounting:
    /// - Sales Invoice: DR Receivable, CR Revenue, CR Tax Payable
    /// - Purchase Invoice: DR Expense/Stock, CR Payable, DR Tax Receivable
    /// - Payment Entry: DR Bank/Cash, CR Receivable (receive) or DR Payable, CR Bank (pay)
    /// - Delivery Note: DR COGS, CR Stock (perpetual inventory)
    /// - Purchase Receipt: DR Stock, CR Stock Received But Not Billed
    /// </summary>
    private async Task SeedAccountingRulesAsync()
    {
        if (await _accountingRuleRepository.GetCountAsync() > 0) return;

        var companies = await _companyRepository.GetListAsync();

        foreach (var company in companies)
        {
            // Sales Invoice: DR Accounts Receivable (GrandTotal)
            await _accountingRuleRepository.InsertAsync(new AccountingRule(
                _guidGenerator.Create(), company.Id, "SI - Debit Receivable",
                "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal)
            { SortOrder = 1, Description = "Debit customer receivable account" }, autoSave: true);

            // Sales Invoice: CR Revenue (NetTotal)
            await _accountingRuleRepository.InsertAsync(new AccountingRule(
                _guidGenerator.Create(), company.Id, "SI - Credit Revenue",
                "SalesInvoice", false, AccountSource.ItemIncome, AmountSource.NetTotal)
            { SortOrder = 2, Description = "Credit revenue/income account" }, autoSave: true);

            // Sales Invoice: CR Tax Payable (TaxAmount)
            await _accountingRuleRepository.InsertAsync(new AccountingRule(
                _guidGenerator.Create(), company.Id, "SI - Credit Tax Payable",
                "SalesInvoice", false, AccountSource.TaxPayable, AmountSource.TaxAmount)
            { SortOrder = 3, Description = "Credit SST/tax payable account" }, autoSave: true);

            // Purchase Invoice: DR Expense/Stock (NetTotal)
            await _accountingRuleRepository.InsertAsync(new AccountingRule(
                _guidGenerator.Create(), company.Id, "PI - Debit Expense",
                "PurchaseInvoice", true, AccountSource.ItemExpense, AmountSource.NetTotal)
            { SortOrder = 1, Description = "Debit expense/stock account" }, autoSave: true);

            // Purchase Invoice: CR Accounts Payable (GrandTotal)
            await _accountingRuleRepository.InsertAsync(new AccountingRule(
                _guidGenerator.Create(), company.Id, "PI - Credit Payable",
                "PurchaseInvoice", false, AccountSource.SupplierPayable, AmountSource.GrandTotal)
            { SortOrder = 2, Description = "Credit supplier payable account" }, autoSave: true);

            // Payment Entry: DR Bank (GrandTotal) — for received payments
            await _accountingRuleRepository.InsertAsync(new AccountingRule(
                _guidGenerator.Create(), company.Id, "PE - Debit Bank",
                "PaymentEntry", true, AccountSource.FixedAccount, AmountSource.GrandTotal)
            { SortOrder = 1, Description = "Debit bank/cash account (payment received)" }, autoSave: true);

            // Payment Entry: CR Receivable (GrandTotal)
            await _accountingRuleRepository.InsertAsync(new AccountingRule(
                _guidGenerator.Create(), company.Id, "PE - Credit Receivable",
                "PaymentEntry", false, AccountSource.CustomerReceivable, AmountSource.GrandTotal)
            { SortOrder = 2, Description = "Credit receivable (payment received)" }, autoSave: true);

            // Delivery Note: DR COGS (NetTotal) — perpetual inventory
            await _accountingRuleRepository.InsertAsync(new AccountingRule(
                _guidGenerator.Create(), company.Id, "DN - Debit COGS",
                "DeliveryNote", true, AccountSource.ItemExpense, AmountSource.NetTotal)
            { SortOrder = 1, Description = "Debit Cost of Goods Sold" }, autoSave: true);

            // Delivery Note: CR Stock In Hand (NetTotal)
            await _accountingRuleRepository.InsertAsync(new AccountingRule(
                _guidGenerator.Create(), company.Id, "DN - Credit Stock",
                "DeliveryNote", false, AccountSource.FixedAccount, AmountSource.NetTotal)
            { SortOrder = 2, Description = "Credit Stock In Hand (perpetual inventory)" }, autoSave: true);

            // Purchase Receipt: DR Stock In Hand (NetTotal)
            await _accountingRuleRepository.InsertAsync(new AccountingRule(
                _guidGenerator.Create(), company.Id, "PR - Debit Stock",
                "PurchaseReceipt", true, AccountSource.FixedAccount, AmountSource.NetTotal)
            { SortOrder = 1, Description = "Debit Stock In Hand" }, autoSave: true);

            // Purchase Receipt: CR Stock Received But Not Billed (NetTotal)
            await _accountingRuleRepository.InsertAsync(new AccountingRule(
                _guidGenerator.Create(), company.Id, "PR - Credit SRBNB",
                "PurchaseReceipt", false, AccountSource.FixedAccount, AmountSource.NetTotal)
            { SortOrder = 2, Description = "Credit Stock Received But Not Billed" }, autoSave: true);
        }
    }

    private async Task SeedTerritoriesAsync()
    {
        if (await _territoryRepository.GetCountAsync() > 0) return;

        var root = new Territory(_guidGenerator.Create(), "All Territories", isGroup: true);
        await _territoryRepository.InsertAsync(root, autoSave: true);

        await _territoryRepository.InsertAsync(
            new Territory(_guidGenerator.Create(), "Malaysia", parentId: root.Id), autoSave: true);
        await _territoryRepository.InsertAsync(
            new Territory(_guidGenerator.Create(), "Rest Of The World", parentId: root.Id), autoSave: true);
    }

    private async Task SeedCustomerGroupsAsync()
    {
        if (await _customerGroupRepository.GetCountAsync() > 0) return;

        var root = new CustomerGroup(_guidGenerator.Create(), "All Customer Groups", isGroup: true);
        await _customerGroupRepository.InsertAsync(root, autoSave: true);

        await _customerGroupRepository.InsertAsync(
            new CustomerGroup(_guidGenerator.Create(), "Individual", parentId: root.Id), autoSave: true);
        await _customerGroupRepository.InsertAsync(
            new CustomerGroup(_guidGenerator.Create(), "Commercial", parentId: root.Id), autoSave: true);
        await _customerGroupRepository.InsertAsync(
            new CustomerGroup(_guidGenerator.Create(), "Government", parentId: root.Id), autoSave: true);
    }

    private async Task SeedSupplierGroupsAsync()
    {
        if (await _supplierGroupRepository.GetCountAsync() > 0) return;

        var root = new SupplierGroup(_guidGenerator.Create(), "All Supplier Groups", isGroup: true);
        await _supplierGroupRepository.InsertAsync(root, autoSave: true);

        await _supplierGroupRepository.InsertAsync(
            new SupplierGroup(_guidGenerator.Create(), "Local", parentId: root.Id), autoSave: true);
        await _supplierGroupRepository.InsertAsync(
            new SupplierGroup(_guidGenerator.Create(), "Overseas", parentId: root.Id), autoSave: true);
    }

    /// <summary>
    /// Seeds standard UOMs + common conversion factors.
    /// Per ERPNext: loaded from uom_data.json + uom_conversion_data.json.
    /// </summary>
    private async Task SeedUomDataAsync()
    {
        if (await _uomRepository.GetCountAsync() > 0) return;

        // Core UOMs (with MustBeWholeNumber where applicable)
        var uoms = new (string Name, bool WholeNumber, string? Category)[]
        {
            ("Unit", true, "Unit"), ("Nos", true, "Unit"), ("Pair", true, "Unit"), ("Set", true, "Unit"),
            ("Dozen", true, "Unit"), ("Box", true, "Unit"), ("Pallet", true, "Unit"), ("Pack", true, "Unit"),
            ("Kg", false, "Mass"), ("Gram", false, "Mass"), ("Tonne", false, "Mass"), ("Pound", false, "Mass"), ("Ounce", false, "Mass"),
            ("Metre", false, "Length"), ("Centimetre", false, "Length"), ("Millimetre", false, "Length"), ("Inch", false, "Length"), ("Foot", false, "Length"),
            ("Litre", false, "Volume"), ("Millilitre", false, "Volume"), ("Gallon", false, "Volume"),
            ("Square Metre", false, "Area"), ("Square Foot", false, "Area"),
            ("Hour", false, "Time"), ("Minute", false, "Time"), ("Day", false, "Time"),
            ("Roll", true, "Unit"), ("Sheet", true, "Unit"), ("Bag", true, "Unit"), ("Bottle", true, "Unit"),
            ("Carton", true, "Unit"), ("Bundle", true, "Unit"), ("Drum", true, "Unit"),
        };

        foreach (var (name, wholeNumber, category) in uoms)
        {
            await _uomRepository.InsertAsync(
                new Uom(_guidGenerator.Create(), name) { MustBeWholeNumber = wholeNumber, Category = category },
                autoSave: true);
        }

        // Standard conversion factors
        var conversions = new (string From, string To, decimal Factor)[]
        {
            ("Kg", "Gram", 1000m), ("Tonne", "Kg", 1000m), ("Pound", "Kg", 0.453592m), ("Ounce", "Gram", 28.3495m),
            ("Metre", "Centimetre", 100m), ("Metre", "Millimetre", 1000m), ("Foot", "Inch", 12m), ("Metre", "Foot", 3.28084m),
            ("Litre", "Millilitre", 1000m), ("Gallon", "Litre", 3.78541m),
            ("Square Metre", "Square Foot", 10.7639m),
            ("Hour", "Minute", 60m), ("Day", "Hour", 8m), // 8-hour working day
            ("Dozen", "Unit", 12m),
        };

        foreach (var (from, to, factor) in conversions)
        {
            await _uomConversionRepository.InsertAsync(
                new UomConversion(_guidGenerator.Create(), from, to, factor),
                autoSave: true);
        }
    }

    /// <summary>
    /// Seeds pegged currency exchange rates (Gulf currencies fixed to USD).
    /// Per ERPNext install.py: 6 Gulf→USD fixed rates created if both currencies exist.
    /// </summary>
    private async Task SeedPeggedCurrenciesAsync()
    {
        if (await _currencyExchangeRepository.GetCountAsync() > 0) return;

        var peggedRates = new (string From, string To, decimal Rate)[]
        {
            ("AED", "USD", 3.6725m),
            ("BHD", "USD", 0.376m),
            ("JOD", "USD", 0.709m),
            ("OMR", "USD", 0.3845m),
            ("QAR", "USD", 3.64m),
            ("SAR", "USD", 3.75m),
        };

        foreach (var (from, to, rate) in peggedRates)
        {
            await _currencyExchangeRepository.InsertAsync(
                new CurrencyExchange(
                    _guidGenerator.Create(), from, to, rate,
                    new DateTime(2000, 1, 1)), // Pegged rates are effectively permanent
                autoSave: true);
        }
    }
}
