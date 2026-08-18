using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Tax.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Tax;

[Authorize(MyERPPermissions.TaxCategories.Default)]
public class TaxWithholdingCategoryAppService : ApplicationService, ITaxWithholdingCategoryAppService
{
    private readonly IRepository<TaxWithholdingCategory, Guid> _repository;

    public TaxWithholdingCategoryAppService(IRepository<TaxWithholdingCategory, Guid> repository)
        => _repository = repository;

    public async Task<PagedResultDto<TaxWithholdingCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderBy(t => t.CategoryName)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<TaxWithholdingCategoryDto>(totalCount,
            items.Select(x => ObjectMapper.Map<TaxWithholdingCategory, TaxWithholdingCategoryDto>(x)).ToList());
    }

    public async Task<TaxWithholdingCategoryDto> GetAsync(Guid id)
    {
        var t = (await _repository.WithDetailsAsync()).First(x => x.Id == id);
        return ObjectMapper.Map<TaxWithholdingCategory, TaxWithholdingCategoryDto>(t);
    }

    [Authorize(MyERPPermissions.TaxCategories.Create)]
    public async Task<TaxWithholdingCategoryDto> CreateAsync(CreateTaxWithholdingCategoryDto input)
    {
        var t = new TaxWithholdingCategory(GuidGenerator.Create(), input.CategoryName, CurrentTenant.Id)
        {
            TaxDeductionBasis = input.TaxDeductionBasis,
            RoundOffTaxAmount = input.RoundOffTaxAmount,
            TaxOnExcessAmount = input.TaxOnExcessAmount,
            DisableCumulativeThreshold = input.DisableCumulativeThreshold,
            DisableTransactionThreshold = input.DisableTransactionThreshold,
        };
        ApplyRatesAndAccounts(t, input);
        await _repository.InsertAsync(t);
        return ObjectMapper.Map<TaxWithholdingCategory, TaxWithholdingCategoryDto>(t);
    }

    [Authorize(MyERPPermissions.TaxCategories.Edit)]
    public async Task<TaxWithholdingCategoryDto> UpdateAsync(Guid id, UpdateTaxWithholdingCategoryDto input)
    {
        var t = (await _repository.WithDetailsAsync()).First(x => x.Id == id);
        t.Rename(input.CategoryName);
        t.TaxDeductionBasis = input.TaxDeductionBasis;
        t.RoundOffTaxAmount = input.RoundOffTaxAmount;
        t.TaxOnExcessAmount = input.TaxOnExcessAmount;
        t.DisableCumulativeThreshold = input.DisableCumulativeThreshold;
        t.DisableTransactionThreshold = input.DisableTransactionThreshold;
        t.ClearRates();
        t.ClearAccounts();
        ApplyRatesAndAccounts(t, input);
        await _repository.UpdateAsync(t);
        return ObjectMapper.Map<TaxWithholdingCategory, TaxWithholdingCategoryDto>(t);
    }

    [Authorize(MyERPPermissions.TaxCategories.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    private static void ApplyRatesAndAccounts(TaxWithholdingCategory t, CreateTaxWithholdingCategoryDto input)
    {
        foreach (var r in input.Rates)
            t.AddRate(r.FromDate, r.ToDate, r.Rate, r.SingleThreshold, r.CumulativeThreshold, r.Group);
        foreach (var a in input.Accounts)
            t.AddAccount(a.CompanyId, a.AccountId);
    }
}
