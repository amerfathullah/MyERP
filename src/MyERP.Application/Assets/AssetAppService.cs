using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Assets.Entities;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.Assets.Default)]
public class AssetAppService : ApplicationService, IAssetAppService
{
    private readonly IRepository<Asset, Guid> _assetRepository;
    private readonly IRepository<AssetCategory, Guid> _categoryRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public AssetAppService(
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetCategory, Guid> categoryRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _assetRepository = assetRepository;
        _categoryRepository = categoryRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<AssetDto> GetAsync(Guid id)
    {
        var asset = await _assetRepository.GetAsync(id, includeDetails: true);
        return ObjectMapper.Map<Asset, AssetDto>(asset);
    }

    public async Task<PagedResultDto<AssetDto>> GetListAsync(GetAssetListDto input)
    {
        var query = await _assetRepository.GetQueryableAsync();

        if (input.Status.HasValue)
            query = query.Where(a => a.Status == input.Status.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(a => a.CompanyId == input.CompanyId.Value);
        if (input.AssetCategoryId.HasValue)
            query = query.Where(a => a.AssetCategoryId == input.AssetCategoryId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.ToLower();
            query = query.Where(a =>
                a.AssetName.ToLower().Contains(filter) ||
                a.AssetNumber.ToLower().Contains(filter));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(a => a.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<AssetDto>(totalCount, items.Select(x => ObjectMapper.Map<Asset, AssetDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.Assets.Create)]
    public async Task<AssetDto> CreateAsync(CreateAssetDto input)
    {
        var number = await _numberGenerator.GenerateAsync("Asset", input.CompanyId);
        var asset = new Asset(
            GuidGenerator.Create(), input.CompanyId, number, input.AssetName,
            input.PurchaseDate, input.PurchaseAmount, CurrentTenant.Id)
        {
            AssetCategoryId = input.AssetCategoryId,
            Location = input.Location,
            AdditionalCost = input.AdditionalCost,
            CalculateDepreciation = input.CalculateDepreciation,
            DepreciationMethod = input.DepreciationMethod,
            UsefulLifeMonths = input.UsefulLifeMonths,
            DepreciationRate = input.DepreciationRate,
            FrequencyMonths = input.FrequencyMonths > 0 ? input.FrequencyMonths : 12,
            AvailableForUseDate = input.AvailableForUseDate,
            Notes = input.Notes,
        };

        if (asset.CalculateDepreciation)
            asset.GenerateDepreciationSchedule();

        await _assetRepository.InsertAsync(asset);
        return ObjectMapper.Map<Asset, AssetDto>(asset);
    }

    [Authorize(MyERPPermissions.Assets.Edit)]
    public async Task<AssetDto> UpdateAsync(Guid id, UpdateAssetDto input)
    {
        var asset = await _assetRepository.GetAsync(id);
        asset.AssetName = input.AssetName;
        asset.AssetCategoryId = input.AssetCategoryId;
        asset.Location = input.Location;
        asset.AdditionalCost = input.AdditionalCost;
        asset.Notes = input.Notes;
        await _assetRepository.UpdateAsync(asset);
        return ObjectMapper.Map<Asset, AssetDto>(asset);
    }

    [Authorize(MyERPPermissions.Assets.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _assetRepository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.Assets.Submit)]
    public async Task<AssetDto> SubmitAsync(Guid id)
    {
        var asset = await _assetRepository.GetAsync(id, includeDetails: true);
        asset.Submit();
        await _assetRepository.UpdateAsync(asset);
        return ObjectMapper.Map<Asset, AssetDto>(asset);
    }

    [Authorize(MyERPPermissions.Assets.Edit)]
    public async Task<AssetDto> SellAsync(Guid id, DateTime disposalDate, decimal amount)
    {
        var asset = await _assetRepository.GetAsync(id);
        asset.Sell(disposalDate, amount);
        await _assetRepository.UpdateAsync(asset);
        return ObjectMapper.Map<Asset, AssetDto>(asset);
    }

    [Authorize(MyERPPermissions.Assets.Edit)]
    public async Task<AssetDto> ScrapAsync(Guid id, DateTime disposalDate)
    {
        var asset = await _assetRepository.GetAsync(id);
        asset.Scrap(disposalDate);
        await _assetRepository.UpdateAsync(asset);
        return ObjectMapper.Map<Asset, AssetDto>(asset);
    }

    public async Task<AssetCategoryDto[]> GetCategoriesAsync()
    {
        var categories = await _categoryRepository.GetListAsync();
        return categories.Select(ObjectMapper.Map<AssetCategory, AssetCategoryDto>).ToArray();
    }

    [Authorize(MyERPPermissions.Assets.Create)]
    public async Task<AssetCategoryDto> CreateCategoryAsync(CreateUpdateAssetCategoryDto input)
    {
        var category = new AssetCategory(GuidGenerator.Create(), input.CategoryName, CurrentTenant.Id)
        {
            IsDepreciable = input.IsDepreciable,
            DefaultDepreciationMethod = input.DefaultDepreciationMethod,
            DefaultUsefulLifeMonths = input.DefaultUsefulLifeMonths,
            DefaultDepreciationRate = input.DefaultDepreciationRate,
        };
        await _categoryRepository.InsertAsync(category);
        return ObjectMapper.Map<AssetCategory, AssetCategoryDto>(category);
    }
}
