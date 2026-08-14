using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Assets.Entities;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.Assets.Default)]
public class AssetAppService : ApplicationService, IAssetAppService
{
    private readonly IRepository<Asset, Guid> _assetRepository;
    private readonly IRepository<AssetCategory, Guid> _categoryRepository;
    private readonly IRepository<AssetActivity, Guid> _activityRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly AssetMapper _assetMapper;
    private readonly AssetCategoryMapper _categoryMapper;

    public AssetAppService(
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetCategory, Guid> categoryRepository,
        IRepository<AssetActivity, Guid> activityRepository,
        IDocumentNumberGenerator numberGenerator,
        AssetMapper assetMapper,
        AssetCategoryMapper categoryMapper)
    {
        _assetRepository = assetRepository;
        _categoryRepository = categoryRepository;
        _activityRepository = activityRepository;
        _numberGenerator = numberGenerator;
        _assetMapper = assetMapper;
        _categoryMapper = categoryMapper;
    }

    public async Task<AssetDto> GetAsync(Guid id)
    {
        var asset = await _assetRepository.GetAsync(id, includeDetails: true);
        return _assetMapper.Map(asset);
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
        if (input.FromDate.HasValue)
            query = query.Where(a => a.PurchaseDate >= input.FromDate.Value);
        if (input.ToDate.HasValue)
            query = query.Where(a => a.PurchaseDate <= input.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(a =>
                a.AssetName.Contains(filter) ||
                a.AssetNumber.Contains(filter));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        query = SortingHelper.ApplySorting(query, input.Sorting,
            q => q.OrderByDescending(a => a.CreationTime),
            ("assetNumber", a => a.AssetNumber),
            ("assetName", a => a.AssetName),
            ("purchaseDate", a => a.PurchaseDate),
            ("purchaseAmount", a => a.PurchaseAmount));

        var items = await AsyncExecuter.ToListAsync(
            query.Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<AssetDto>(totalCount, items.Select(_assetMapper.Map).ToList());
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
            ItemId = input.ItemId,
            Location = input.Location,
            CustodianEmployeeId = input.CustodianEmployeeId,
            AdditionalCost = input.AdditionalCost,
            CalculateDepreciation = input.CalculateDepreciation,
            DepreciationMethod = input.DepreciationMethod,
            UsefulLifeMonths = input.UsefulLifeMonths,
            DepreciationRate = input.DepreciationRate,
            FrequencyMonths = input.FrequencyMonths > 0 ? input.FrequencyMonths : 12,
            AvailableForUseDate = input.AvailableForUseDate,
            OpeningAccumulatedDepreciation = input.OpeningAccumulatedDepreciation,
            Notes = input.Notes,
        };

        if (asset.CalculateDepreciation)
            asset.GenerateDepreciationSchedule();

        await _assetRepository.InsertAsync(asset);

        var activity = new AssetActivity(
            GuidGenerator.Create(),
            asset.Id,
            AssetActivityType.Created,
            $"Asset created: {asset.AssetNumber} ({asset.AssetName})",
            DateTime.UtcNow,
            $"Purchase amount: {asset.PurchaseAmount:N2}",
            "Asset",
            asset.Id.ToString(),
            CurrentTenant.Id);

        await _activityRepository.InsertAsync(activity);

        return _assetMapper.Map(asset);
    }

    [Authorize(MyERPPermissions.Assets.Edit)]
    public async Task<AssetDto> UpdateAsync(Guid id, UpdateAssetDto input)
    {
        var asset = await _assetRepository.GetAsync(id);
        asset.AssetName = input.AssetName;
        asset.AssetCategoryId = input.AssetCategoryId;
        asset.ItemId = input.ItemId;
        asset.Location = input.Location;
        asset.CustodianEmployeeId = input.CustodianEmployeeId;
        asset.AdditionalCost = input.AdditionalCost;
        asset.CalculateDepreciation = input.CalculateDepreciation;
        asset.DepreciationMethod = input.DepreciationMethod;
        asset.UsefulLifeMonths = input.UsefulLifeMonths;
        asset.DepreciationRate = input.DepreciationRate;
        asset.FrequencyMonths = input.FrequencyMonths > 0 ? input.FrequencyMonths : 12;
        asset.AvailableForUseDate = input.AvailableForUseDate;
        asset.Notes = input.Notes;

        if (asset.CalculateDepreciation)
            asset.GenerateDepreciationSchedule();

        await _assetRepository.UpdateAsync(asset);
        return _assetMapper.Map(asset);
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
        return _assetMapper.Map(asset);
    }

    [Authorize(MyERPPermissions.Assets.Edit)]
    public async Task<AssetDto> SellAsync(Guid id, DateTime disposalDate, decimal amount)
    {
        var asset = await _assetRepository.GetAsync(id);

        var lifecycleManager = LazyServiceProvider.LazyGetRequiredService<MyERP.Assets.DomainServices.AssetLifecycleManager>();
        var gainLoss = lifecycleManager.CalculateDisposalGainLoss(asset);

        asset.Sell(disposalDate, amount);
        await _assetRepository.UpdateAsync(asset);

        var activity = new AssetActivity(
            GuidGenerator.Create(),
            asset.Id,
            AssetActivityType.Sold,
            $"Asset sold: {asset.AssetNumber}",
            disposalDate,
            $"Disposed for {amount:N2}, Gain/Loss: {gainLoss:N2}",
            "Asset",
            asset.Id.ToString(),
            CurrentTenant.Id);

        await _activityRepository.InsertAsync(activity);

        return _assetMapper.Map(asset);
    }

    [Authorize(MyERPPermissions.Assets.Edit)]
    public async Task<AssetDto> ScrapAsync(Guid id, DateTime disposalDate)
    {
        var asset = await _assetRepository.GetAsync(id);

        var lifecycleManager = LazyServiceProvider.LazyGetRequiredService<MyERP.Assets.DomainServices.AssetLifecycleManager>();
        var gainLoss = lifecycleManager.CalculateDisposalGainLoss(asset);

        asset.Scrap(disposalDate);
        await _assetRepository.UpdateAsync(asset);

        var activity = new AssetActivity(
            GuidGenerator.Create(),
            asset.Id,
            AssetActivityType.Scrapped,
            $"Asset scrapped: {asset.AssetNumber}",
            disposalDate,
            $"Scrapped. Book value loss: {Math.Abs(gainLoss):N2}",
            "Asset",
            asset.Id.ToString(),
            CurrentTenant.Id);

        await _activityRepository.InsertAsync(activity);

        return _assetMapper.Map(asset);
    }

    public async Task<AssetCategoryDto[]> GetCategoriesAsync()
    {
        var query = await _categoryRepository.WithDetailsAsync(c => c.Accounts);
        var categories = await AsyncExecuter.ToListAsync(query);
        return categories.Select(_categoryMapper.Map).ToArray();
    }

    [Authorize(MyERPPermissions.Assets.Create)]
    public async Task<AssetCategoryDto> CreateCategoryAsync(CreateUpdateAssetCategoryDto input)
    {
        var category = new AssetCategory(GuidGenerator.Create(), input.CategoryName, CurrentTenant.Id)
        {
            IsDepreciable = input.IsDepreciable,
            EnableCwipAccounting = input.EnableCwipAccounting,
            NonDepreciableCategory = input.NonDepreciableCategory,
            DefaultDepreciationMethod = input.DefaultDepreciationMethod,
            DefaultUsefulLifeMonths = input.DefaultUsefulLifeMonths,
            DefaultDepreciationRate = input.DefaultDepreciationRate,
            DefaultFrequencyMonths = input.DefaultFrequencyMonths,
            AssetAccountId = input.AssetAccountId,
            DepreciationAccountId = input.DepreciationAccountId,
            AccumulatedDepreciationAccountId = input.AccumulatedDepreciationAccountId,
        };

        if (input.Accounts != null)
        {
            foreach (var acc in input.Accounts)
            {
                category.AddAccount(
                    GuidGenerator.Create(),
                    acc.CompanyId,
                    acc.FixedAssetAccountId,
                    acc.AccumulatedDepreciationAccountId,
                    acc.DepreciationExpenseAccountId,
                    acc.CapitalWorkInProgressAccountId);
            }
        }

        await _categoryRepository.InsertAsync(category);
        return _categoryMapper.Map(category);
    }
}
