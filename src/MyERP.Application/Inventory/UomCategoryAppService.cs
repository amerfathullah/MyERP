using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.Items.Default)]
public class UomCategoryAppService : ApplicationService, IUomCategoryAppService
{
    private readonly IRepository<UomCategory, Guid> _repository;

    public UomCategoryAppService(IRepository<UomCategory, Guid> repository)
    {
        _repository = repository;
    }

    private static UomCategoryDto ToDto(UomCategory entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        CreationTime = entity.CreationTime,
    };

    public async Task<UomCategoryDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ToDto(entity);
    }

    public async Task<PagedResultDto<UomCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var list = query.OrderBy(x => x.Name).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<UomCategoryDto>(totalCount, list.Select(ToDto).ToList());
    }

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<UomCategoryDto> CreateAsync(CreateUpdateUomCategoryDto input)
    {
        var entity = new UomCategory(GuidGenerator.Create(), input.Name, CurrentTenant.Id);
        await _repository.InsertAsync(entity);
        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<UomCategoryDto> UpdateAsync(Guid id, CreateUpdateUomCategoryDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.SetName(input.Name);
        await _repository.UpdateAsync(entity);
        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.Items.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
