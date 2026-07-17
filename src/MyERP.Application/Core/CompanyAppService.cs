using System;
using System.Linq;
using System.Threading.Tasks;
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

        // Seed Default Warehouses
        var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();
        var hasWh = (await whRepo.GetQueryableAsync()).Any(w => w.CompanyId == companyId);
        if (!hasWh)
        {
            await whRepo.InsertAsync(new Warehouse(GuidGenerator.Create(), companyId, "Stores") { IsActive = true }, autoSave: true);
            await whRepo.InsertAsync(new Warehouse(GuidGenerator.Create(), companyId, "Finished Goods") { IsActive = true }, autoSave: true);
            await whRepo.InsertAsync(new Warehouse(GuidGenerator.Create(), companyId, "Work In Progress") { IsActive = true }, autoSave: true);
        }

        // Seed Manufacturing Settings
        var mfgRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Manufacturing.Entities.ManufacturingSettings, Guid>>();
        var hasMfg = (await mfgRepo.GetQueryableAsync()).Any(s => s.CompanyId == companyId);
        if (!hasMfg)
        {
            await mfgRepo.InsertAsync(new Manufacturing.Entities.ManufacturingSettings(
                GuidGenerator.Create(), companyId), autoSave: true);
        }
    }
}
