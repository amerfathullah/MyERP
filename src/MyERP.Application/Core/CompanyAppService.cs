using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
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
}
