using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Assets.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

public class AssetCategoryDetailDto : EntityDto<Guid>
{
    public string CategoryName { get; set; } = null!;
    public bool IsDepreciable { get; set; }
    public DepreciationMethod DefaultDepreciationMethod { get; set; }
    public int DefaultUsefulLifeMonths { get; set; }
    public decimal? DefaultDepreciationRate { get; set; }
    public Guid? AssetAccountId { get; set; }
    public Guid? DepreciationAccountId { get; set; }
    public Guid? AccumulatedDepreciationAccountId { get; set; }
}

public class CreateUpdateAssetCategoryDetailDto
{
    public string CategoryName { get; set; } = null!;
    public bool IsDepreciable { get; set; } = true;
    public DepreciationMethod DefaultDepreciationMethod { get; set; }
    public int DefaultUsefulLifeMonths { get; set; } = 60;
    public decimal? DefaultDepreciationRate { get; set; }
    public Guid? AssetAccountId { get; set; }
    public Guid? DepreciationAccountId { get; set; }
    public Guid? AccumulatedDepreciationAccountId { get; set; }
}

[Authorize(MyERPPermissions.Assets.Default)]
public class AssetCategoryAppService : ApplicationService
{
    private readonly IRepository<AssetCategory, Guid> _repository;

    public AssetCategoryAppService(IRepository<AssetCategory, Guid> repository)
        => _repository = repository;

    public async Task<PagedResultDto<AssetCategoryDetailDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(c => c.CategoryName)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<AssetCategoryDetailDto>(totalCount,
            items.Select(ObjectMapper.Map<AssetCategory, AssetCategoryDetailDto>).ToList());
    }

    public async Task<AssetCategoryDetailDto> GetAsync(Guid id)
        => ObjectMapper.Map<AssetCategory, AssetCategoryDetailDto>(await _repository.GetAsync(id));

    [Authorize(MyERPPermissions.Assets.Create)]
    public async Task<AssetCategoryDetailDto> CreateAsync(CreateUpdateAssetCategoryDetailDto input)
    {
        var category = new AssetCategory(GuidGenerator.Create(), input.CategoryName, CurrentTenant.Id)
        {
            IsDepreciable = input.IsDepreciable,
            DefaultDepreciationMethod = input.DefaultDepreciationMethod,
            DefaultUsefulLifeMonths = input.DefaultUsefulLifeMonths,
            DefaultDepreciationRate = input.DefaultDepreciationRate,
            AssetAccountId = input.AssetAccountId,
            DepreciationAccountId = input.DepreciationAccountId,
            AccumulatedDepreciationAccountId = input.AccumulatedDepreciationAccountId,
        };
        await _repository.InsertAsync(category);
        return ObjectMapper.Map<AssetCategory, AssetCategoryDetailDto>(category);
    }

    [Authorize(MyERPPermissions.Assets.Edit)]
    public async Task<AssetCategoryDetailDto> UpdateAsync(Guid id, CreateUpdateAssetCategoryDetailDto input)
    {
        var category = await _repository.GetAsync(id);
        category.CategoryName = input.CategoryName;
        category.IsDepreciable = input.IsDepreciable;
        category.DefaultDepreciationMethod = input.DefaultDepreciationMethod;
        category.DefaultUsefulLifeMonths = input.DefaultUsefulLifeMonths;
        category.DefaultDepreciationRate = input.DefaultDepreciationRate;
        category.AssetAccountId = input.AssetAccountId;
        category.DepreciationAccountId = input.DepreciationAccountId;
        category.AccumulatedDepreciationAccountId = input.AccumulatedDepreciationAccountId;
        await _repository.UpdateAsync(category);
        return ObjectMapper.Map<AssetCategory, AssetCategoryDetailDto>(category);
    }

    [Authorize(MyERPPermissions.Assets.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        // Block deletion if active assets use this category
        var assetRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Asset, Guid>>();
        var query = await assetRepo.GetQueryableAsync();
        var hasActive = query.Any(a => a.AssetCategoryId == id
            && a.Status != AssetStatus.Cancelled);
        if (hasActive)
        {
            throw new Volo.Abp.BusinessException("MyERP:15002")
                .WithData("reason", "Active assets are linked to this category.");
        }
        await _repository.DeleteAsync(id);
    }
}
