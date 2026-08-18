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
public class BrandAppService : ApplicationService, IBrandAppService
{
    private readonly IRepository<Brand, Guid> _repository;

    public BrandAppService(IRepository<Brand, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<BrandDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(b => b.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<BrandDto>(totalCount, items.Select(ObjectMapper.Map<Brand, BrandDto>).ToList());
    }

    public async Task<BrandDto> GetAsync(Guid id)
        => ObjectMapper.Map<Brand, BrandDto>(await _repository.GetAsync(id));

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<BrandDto> CreateAsync(CreateUpdateBrandDto input)
    {
        var brand = new Brand(GuidGenerator.Create(), input.Name, CurrentTenant.Id)
        {
            Description = input.Description,
            DefaultWarehouseId = input.DefaultWarehouseId,
            DefaultIncomeAccountId = input.DefaultIncomeAccountId,
            DefaultExpenseAccountId = input.DefaultExpenseAccountId,
            IsActive = input.IsActive,
        };
        await _repository.InsertAsync(brand);
        return ObjectMapper.Map<Brand, BrandDto>(brand);
    }

    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<BrandDto> UpdateAsync(Guid id, CreateUpdateBrandDto input)
    {
        var brand = await _repository.GetAsync(id);
        brand.Rename(input.Name);
        brand.Description = input.Description;
        brand.DefaultWarehouseId = input.DefaultWarehouseId;
        brand.DefaultIncomeAccountId = input.DefaultIncomeAccountId;
        brand.DefaultExpenseAccountId = input.DefaultExpenseAccountId;
        brand.IsActive = input.IsActive;
        await _repository.UpdateAsync(brand);
        return ObjectMapper.Map<Brand, BrandDto>(brand);
    }

    [Authorize(MyERPPermissions.Items.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
