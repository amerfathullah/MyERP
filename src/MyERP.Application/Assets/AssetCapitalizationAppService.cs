using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.DomainServices;
using MyERP.Assets.DomainServices;
using MyERP.Assets.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.AssetCapitalizations.Default)]
public class AssetCapitalizationAppService : ApplicationService, IAssetCapitalizationAppService
{
    private readonly IRepository<AssetCapitalization, Guid> _repository;
    private readonly IRepository<Asset, Guid> _assetRepository;
    private readonly IRepository<AssetActivity, Guid> _activityRepository;
    private readonly AssetCapitalizationMapper _mapper;
    private readonly AssetCapitalizationPostingService _postingService;
    private readonly DocumentPostingOrchestrator _postingOrchestrator;

    public AssetCapitalizationAppService(
        IRepository<AssetCapitalization, Guid> repository,
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetActivity, Guid> activityRepository,
        AssetCapitalizationMapper mapper,
        AssetCapitalizationPostingService postingService,
        DocumentPostingOrchestrator postingOrchestrator)
    {
        _repository = repository;
        _assetRepository = assetRepository;
        _activityRepository = activityRepository;
        _mapper = mapper;
        _postingService = postingService;
        _postingOrchestrator = postingOrchestrator;
    }

    public async Task<PagedResultDto<AssetCapitalizationDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var items = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.PostingDate)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        return new PagedResultDto<AssetCapitalizationDto>(totalCount, items.Select(_mapper.Map).ToList());
    }

    public async Task<AssetCapitalizationDto> GetAsync(Guid id)
    {
        var cap = (await _repository.WithDetailsAsync(c => c.StockItems, c => c.ServiceItems, c => c.ConsumedAssets)).First(c => c.Id == id);
        return _mapper.Map(cap);
    }

    [Authorize(MyERPPermissions.AssetCapitalizations.Create)]
    public async Task<AssetCapitalizationDto> CreateAsync(CreateUpdateAssetCapitalizationDto input)
    {
        var targetAsset = await _assetRepository.GetAsync(input.TargetAssetId);
        if (targetAsset.CompanyId != input.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AssetCompanyMismatch)
                .WithData("assetName", targetAsset.AssetName);
        }
        if (targetAsset.Status is AssetStatus.Sold or AssetStatus.Scrapped)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AssetCannotBeMoved)
                .WithData("assetName", targetAsset.AssetName)
                .WithData("status", targetAsset.Status.ToString());
        }

        if (input.StockItems != null && input.StockItems.Any())
        {
            var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
            await itemValidation.ValidateItemsForTransactionAsync(input.StockItems.Select(i => i.ItemId).ToArray());
        }

        if (input.ConsumedAssets != null)
        {
            foreach (var ca in input.ConsumedAssets)
            {
                if (ca.AssetId == input.TargetAssetId)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ConsumedAssetCannotBeTargetAsset);
                }
                var consumedAsset = await _assetRepository.GetAsync(ca.AssetId);
                if (consumedAsset.CompanyId != input.CompanyId)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.AssetCompanyMismatch)
                        .WithData("assetName", consumedAsset.AssetName);
                }
                if (consumedAsset.Status is AssetStatus.Sold or AssetStatus.Scrapped)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.AssetCannotBeMoved)
                        .WithData("assetName", consumedAsset.AssetName)
                        .WithData("status", consumedAsset.Status.ToString());
                }
            }
        }

        var capNumber = $"AS-CAP-{DateTime.UtcNow:yyyyMMdd}-{GuidGenerator.Create().ToString()[..6].ToUpper()}";
        var cap = new AssetCapitalization(
            GuidGenerator.Create(),
            input.CompanyId,
            capNumber,
            input.PostingDate,
            input.TargetAssetId,
            CurrentTenant.Id)
        {
            TargetAssetName = input.TargetAssetName,
        };

        if (input.StockItems != null)
        {
            foreach (var item in input.StockItems)
            {
                cap.AddStockItem(item.ItemId, item.ItemName, item.Qty, item.Rate, item.WarehouseId);
            }
        }

        if (input.ServiceItems != null)
        {
            foreach (var item in input.ServiceItems)
            {
                cap.AddServiceItem(item.ItemId, item.ItemName, item.Amount, item.ExpenseAccountId);
            }
        }

        if (input.ConsumedAssets != null)
        {
            foreach (var item in input.ConsumedAssets)
            {
                cap.AddConsumedAsset(item.AssetId, item.AssetName, item.CurrentValue);
            }
        }

        await _repository.InsertAsync(cap);
        return _mapper.Map(cap);
    }

    [Authorize(MyERPPermissions.AssetCapitalizations.Edit)]
    public async Task<AssetCapitalizationDto> UpdateAsync(Guid id, CreateUpdateAssetCapitalizationDto input)
    {
        var cap = await _repository.GetAsync(id);
        if (cap.Status != AssetCapitalizationStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        cap.PostingDate = input.PostingDate;
        cap.TargetAssetId = input.TargetAssetId;
        cap.TargetAssetName = input.TargetAssetName;

        await _repository.UpdateAsync(cap);
        return _mapper.Map(cap);
    }

    [Authorize(MyERPPermissions.AssetCapitalizations.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var cap = await _repository.GetAsync(id);
        if (cap.Status != AssetCapitalizationStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        await _repository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.AssetCapitalizations.Edit)]
    public async Task<AssetCapitalizationDto> SubmitAsync(Guid id)
    {
        // Plain GetAsync never loaded StockItems/ServiceItems/ConsumedAssets (no AutoInclude
        // configured on those navigations) — SubmitAsync's own item-count guard right below always
        // saw them as empty and threw DocumentMustHaveItems for every capitalization, regardless of
        // its real content. Submitting one had never actually worked.
        var cap = (await _repository.WithDetailsAsync(c => c.StockItems, c => c.ServiceItems, c => c.ConsumedAssets)).First(c => c.Id == id);
        if (!cap.StockItems.Any() && !cap.ServiceItems.Any() && !cap.ConsumedAssets.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        cap.Submit();

        var targetAsset = await _assetRepository.FindAsync(cap.TargetAssetId);
        if (targetAsset != null)
        {
            // Consumes stock items for real (StockLedgerEntry per item) and posts GL, returning the
            // total recomputed at true stock-consumption cost — applied to the target asset's book
            // value below instead of the row-level estimate so GL and asset value stay consistent.
            var (journalEntryId, postedTotal) = await _postingService.PostAsync(cap, targetAsset);
            if (journalEntryId.HasValue)
            {
                cap.JournalEntryId = journalEntryId;
                cap.TotalCapitalizedAmount = postedTotal;
            }

            targetAsset.ApplyRepairCapitalization(cap.TotalCapitalizedAmount, 0);
            await _assetRepository.UpdateAsync(targetAsset);

            var activity = new AssetActivity(
                GuidGenerator.Create(),
                targetAsset.Id,
                AssetActivityType.Capitalized,
                $"Asset Capitalization #{cap.CapitalizationNumber} submitted",
                cap.PostingDate,
                $"Capitalized amount: {cap.TotalCapitalizedAmount:N2}",
                "AssetCapitalization",
                cap.Id.ToString(),
                CurrentTenant.Id);

            await _activityRepository.InsertAsync(activity);
        }

        // Mark consumed assets as capitalized (ERPNext commit 2391c859b2 / a121c30b56)
        foreach (var consumedRow in cap.ConsumedAssets)
        {
            var consumedAsset = await _assetRepository.FindAsync(consumedRow.AssetId);
            if (consumedAsset != null)
            {
                consumedAsset.MarkAsCapitalized(cap.PostingDate);
                await _assetRepository.UpdateAsync(consumedAsset);

                var activity = new AssetActivity(
                    GuidGenerator.Create(),
                    consumedAsset.Id,
                    AssetActivityType.Capitalized,
                    $"Asset consumed in Capitalization #{cap.CapitalizationNumber}",
                    cap.PostingDate,
                    $"Consumed into target asset {targetAsset?.AssetName ?? cap.TargetAssetName}",
                    "AssetCapitalization",
                    cap.Id.ToString(),
                    CurrentTenant.Id);
                await _activityRepository.InsertAsync(activity);
            }
        }

        await _repository.UpdateAsync(cap);
        return _mapper.Map(cap);
    }

    [Authorize(MyERPPermissions.AssetCapitalizations.Edit)]
    public async Task<AssetCapitalizationDto> CancelAsync(Guid id)
    {
        var cap = (await _repository.WithDetailsAsync(c => c.StockItems, c => c.ServiceItems, c => c.ConsumedAssets)).First(c => c.Id == id);
        cap.Cancel();

        if (cap.JournalEntryId.HasValue)
        {
            await _postingOrchestrator.ReverseGlForJournalEntryAsync(cap.JournalEntryId.Value);
            await _postingService.ReverseStockAsync(cap, CurrentTenant.Id);
        }

        var targetAsset = await _assetRepository.FindAsync(cap.TargetAssetId);
        if (targetAsset != null)
        {
            targetAsset.ApplyRepairCapitalization(-1 * cap.TotalCapitalizedAmount, 0);
            await _assetRepository.UpdateAsync(targetAsset);

            var activity = new AssetActivity(
                GuidGenerator.Create(),
                targetAsset.Id,
                AssetActivityType.Capitalized,
                $"Asset Capitalization #{cap.CapitalizationNumber} cancelled",
                DateTime.UtcNow,
                $"Reverted capitalization of {cap.TotalCapitalizedAmount:N2}",
                "AssetCapitalization",
                cap.Id.ToString(),
                CurrentTenant.Id);

            await _activityRepository.InsertAsync(activity);
        }

        // Restore consumed assets (ERPNext commit 2391c859b2 / a121c30b56)
        foreach (var consumedRow in cap.ConsumedAssets)
        {
            var consumedAsset = await _assetRepository.FindAsync(consumedRow.AssetId);
            if (consumedAsset != null)
            {
                consumedAsset.RestoreFromCapitalization();
                await _assetRepository.UpdateAsync(consumedAsset);

                var activity = new AssetActivity(
                    GuidGenerator.Create(),
                    consumedAsset.Id,
                    AssetActivityType.Capitalized,
                    $"Asset consumption reverted (Capitalization #{cap.CapitalizationNumber} cancelled)",
                    DateTime.UtcNow,
                    $"Restored from target asset {targetAsset?.AssetName ?? cap.TargetAssetName}",
                    "AssetCapitalization",
                    cap.Id.ToString(),
                    CurrentTenant.Id);
                await _activityRepository.InsertAsync(activity);
            }
        }

        await _repository.UpdateAsync(cap);
        return _mapper.Map(cap);
    }
}
