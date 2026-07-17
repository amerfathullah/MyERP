using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Tax.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Tax;

[Authorize(MyERPPermissions.TaxCategories.Default)]
public class TaxCategoryAppService : ApplicationService, ITaxCategoryAppService
{
    private readonly IRepository<TaxCategory, Guid> _repository;

    public TaxCategoryAppService(IRepository<TaxCategory, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<TaxCategoryDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<TaxCategory, TaxCategoryDto>(entity);
    }

    public async Task<PagedResultDto<TaxCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _repository.GetCountAsync();
        var items = await _repository.GetPagedListAsync(input.SkipCount, input.MaxResultCount, input.Sorting ?? "Code ASC");
        return new PagedResultDto<TaxCategoryDto>(totalCount, items.Select(x => ObjectMapper.Map<TaxCategory, TaxCategoryDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.TaxCategories.Create)]
    public async Task<TaxCategoryDto> CreateAsync(CreateUpdateTaxCategoryDto input)
    {
        var entity = new TaxCategory(GuidGenerator.Create(), input.Code, input.Name, input.TaxType);
        entity.Description = input.Description;
        entity.IsActive = input.IsActive;
        await _repository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<TaxCategory, TaxCategoryDto>(entity);
    }

    [Authorize(MyERPPermissions.TaxCategories.Edit)]
    public async Task<TaxCategoryDto> UpdateAsync(Guid id, CreateUpdateTaxCategoryDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.SetCode(input.Code);
        entity.SetName(input.Name);
        entity.TaxType = input.TaxType;
        entity.Description = input.Description;
        entity.IsActive = input.IsActive;
        await _repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<TaxCategory, TaxCategoryDto>(entity);
    }

    [Authorize(MyERPPermissions.TaxCategories.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}

[Authorize(MyERPPermissions.TaxCategories.Default)]
public class TaxRuleAppService : ApplicationService, ITaxRuleAppService
{
    private readonly IRepository<TaxRule, Guid> _repository;

    public TaxRuleAppService(IRepository<TaxRule, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<TaxRuleDto>> GetListAsync(Guid taxCategoryId, PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        query = query.Where(r => r.TaxCategoryId == taxCategoryId);
        var totalCount = query.Count();
        var items = query.OrderByDescending(r => r.EffectiveFrom)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<TaxRuleDto>(totalCount, items.Select(x => ObjectMapper.Map<TaxRule, TaxRuleDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.TaxCategories.Create)]
    public async Task<TaxRuleDto> CreateAsync(CreateUpdateTaxRuleDto input)
    {
        var entity = new TaxRule(GuidGenerator.Create(), input.TaxCategoryId, input.Rate, input.EffectiveFrom);
        entity.EffectiveTo = input.EffectiveTo;
        entity.ItemGroupFilter = input.ItemGroupFilter;
        entity.RegionFilter = input.RegionFilter;
        entity.Priority = input.Priority;
        entity.Description = input.Description;
        entity.IsActive = input.IsActive;
        await _repository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<TaxRule, TaxRuleDto>(entity);
    }

    [Authorize(MyERPPermissions.TaxCategories.Edit)]
    public async Task<TaxRuleDto> UpdateAsync(Guid id, CreateUpdateTaxRuleDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Rate = input.Rate;
        entity.EffectiveFrom = input.EffectiveFrom;
        entity.EffectiveTo = input.EffectiveTo;
        entity.ItemGroupFilter = input.ItemGroupFilter;
        entity.RegionFilter = input.RegionFilter;
        entity.Priority = input.Priority;
        entity.Description = input.Description;
        entity.IsActive = input.IsActive;
        await _repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<TaxRule, TaxRuleDto>(entity);
    }

    [Authorize(MyERPPermissions.TaxCategories.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
