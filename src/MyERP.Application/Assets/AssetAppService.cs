using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Assets.DomainServices;
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
    private readonly IRepository<Location, Guid> _locationRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly AssetLifecycleManager _lifecycleManager;
    private readonly AssetMapper _assetMapper;
    private readonly AssetCategoryMapper _categoryMapper;

    public AssetAppService(
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetCategory, Guid> categoryRepository,
        IRepository<AssetActivity, Guid> activityRepository,
        IRepository<Location, Guid> locationRepository,
        IDocumentNumberGenerator numberGenerator,
        AssetLifecycleManager lifecycleManager,
        AssetMapper assetMapper,
        AssetCategoryMapper categoryMapper)
    {
        _assetRepository = assetRepository;
        _categoryRepository = categoryRepository;
        _activityRepository = activityRepository;
        _locationRepository = locationRepository;
        _numberGenerator = numberGenerator;
        _lifecycleManager = lifecycleManager;
        _assetMapper = assetMapper;
        _categoryMapper = categoryMapper;
    }

    /// <summary>Resolves LocationId to the Location master's name for the denormalized display field.</summary>
    private async Task<string?> ResolveLocationNameAsync(Guid? locationId, string? fallback)
    {
        if (!locationId.HasValue) return fallback;
        var location = await _locationRepository.FindAsync(locationId.Value);
        return location?.LocationName ?? fallback;
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
            Location = await ResolveLocationNameAsync(input.LocationId, input.Location),
            LocationId = input.LocationId,
            CustodianEmployeeId = input.CustodianEmployeeId,
            PurchaseReceiptId = input.PurchaseReceiptId,
            PurchaseInvoiceId = input.PurchaseInvoiceId,
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
        asset.Location = await ResolveLocationNameAsync(input.LocationId, input.Location);
        asset.LocationId = input.LocationId;
        asset.CustodianEmployeeId = input.CustodianEmployeeId;
        asset.AdditionalCost = input.AdditionalCost;
        asset.CalculateDepreciation = input.CalculateDepreciation;
        asset.DepreciationMethod = input.DepreciationMethod;
        asset.UsefulLifeMonths = input.UsefulLifeMonths;
        asset.DepreciationRate = input.DepreciationRate;
        asset.FrequencyMonths = input.FrequencyMonths > 0 ? input.FrequencyMonths : 12;
        asset.AvailableForUseDate = input.AvailableForUseDate;
        asset.OpeningAccumulatedDepreciation = input.OpeningAccumulatedDepreciation;
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

    /// <summary>
    /// Cancels the asset (Draft, Submitted, or PartiallyDepreciated/FullyDepreciated). There
    /// was previously no way to cancel an asset via the API at all — Asset.Cancel() existed
    /// but had zero call sites. PartiallyDepreciated/FullyDepreciated assets have real
    /// GL-posted depreciation Journal Entries — each booked entry's JE is reversed first
    /// (DocumentPostingOrchestrator.ReverseGlForJournalEntryAsync), then the asset's own
    /// schedule/value state is reset (Asset.ReverseAllBookedDepreciation) before Cancel()'s
    /// guard, which by then sees no booked entries left, passes.
    /// </summary>
    [Authorize(MyERPPermissions.Assets.Edit)]
    public async Task<AssetDto> CancelAsync(Guid id)
    {
        var asset = await _assetRepository.GetAsync(id, includeDetails: true);

        var bookedEntries = asset.DepreciationSchedule.Where(e => e.IsBooked && e.JournalEntryId.HasValue).ToList();
        if (bookedEntries.Count > 0)
        {
            var postingOrchestrator = LazyServiceProvider
                .LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
            foreach (var entry in bookedEntries)
            {
                await postingOrchestrator.ReverseGlForJournalEntryAsync(entry.JournalEntryId!.Value);
            }

            asset.ReverseAllBookedDepreciation();
        }

        asset.Cancel();
        await _assetRepository.UpdateAsync(asset);

        var activity = new AssetActivity(
            GuidGenerator.Create(),
            asset.Id,
            AssetActivityType.Cancelled,
            $"Asset cancelled: {asset.AssetNumber}",
            DateTime.UtcNow,
            null,
            "Asset",
            asset.Id.ToString(),
            CurrentTenant.Id);

        await _activityRepository.InsertAsync(activity);

        return _assetMapper.Map(asset);
    }

    [Authorize(MyERPPermissions.Assets.Edit)]
    public async Task<AssetDto> SellAsync(Guid id, DateTime disposalDate, decimal amount, Guid? settlementAccountId)
    {
        var asset = await _assetRepository.GetAsync(id);

        var lifecycleManager = LazyServiceProvider.LazyGetRequiredService<MyERP.Assets.DomainServices.AssetLifecycleManager>();
        var gainLoss = lifecycleManager.CalculateDisposalGainLoss(asset, disposalDate, amount);
        var preDisposalValue = asset.SimulateBookValueAtDate(disposalDate);

        asset.Sell(disposalDate, amount);

        // Remove the asset from the books: CR Fixed Asset, DR Accumulated Depreciation,
        // DR settlement account for proceeds, DR/CR Disposal Account for gain/loss.
        // Without this, a sold asset's cost/accumulated-depreciation stayed on the balance
        // sheet forever and the gain/loss on sale never hit the P&L.
        await lifecycleManager.PostDisposalJournalEntryAsync(asset, amount, preDisposalValue, settlementAccountId);

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
        var gainLoss = lifecycleManager.CalculateDisposalGainLoss(asset, disposalDate, 0);
        var preDisposalValue = asset.SimulateBookValueAtDate(disposalDate);

        asset.Scrap(disposalDate);

        // Remove the asset from the books: CR Fixed Asset, DR Accumulated Depreciation,
        // DR Disposal Account for the full remaining book value (scrap proceeds are always 0).
        await lifecycleManager.PostDisposalJournalEntryAsync(asset, 0, preDisposalValue, settlementAccountId: null);

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

    /// <summary>
    /// Reverses a scrap: reverses the disposal GL entry (a genuine contra entry, not a mutation
    /// of the original — per the accounts-controller "immutable audit trail" rule) and restores
    /// the asset's pre-disposal status. Sale is not reversible this way — a sold asset has real
    /// external proceeds/counterparty, matching ERPNext's restore_asset (scrap-only).
    /// </summary>
    [Authorize(MyERPPermissions.Assets.Edit)]
    public async Task<AssetDto> RestoreAsync(Guid id)
    {
        var asset = await _assetRepository.GetAsync(id);

        var postingOrchestrator = LazyServiceProvider
            .LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.ReverseGlForDocumentAsync("Asset", asset.Id);

        asset.Restore();
        await _assetRepository.UpdateAsync(asset);

        var activity = new AssetActivity(
            GuidGenerator.Create(),
            asset.Id,
            AssetActivityType.Restored,
            $"Asset restored: {asset.AssetNumber}",
            DateTime.UtcNow,
            $"Scrap reversed. Status reset to {asset.Status}.",
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

    [Authorize(MyERPPermissions.Assets.Create)]
    public async Task<AssetDto> SplitAsync(Guid id, int splitQty)
    {
        var splitAsset = await _lifecycleManager.SplitAssetAsync(id, splitQty);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Asset", splitAsset.Id,
            "Created", splitAsset.CompanyId,
            splitAsset.AssetNumber, "Draft", splitAsset.Status.ToString(), CurrentUser.Id,
            $"Asset '{splitAsset.AssetName}' created by splitting from asset ID {id} with quantity {splitQty}", CurrentTenant.Id));

        return _assetMapper.Map(splitAsset);
    }
}
