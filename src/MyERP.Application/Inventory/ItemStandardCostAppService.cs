using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Application service for Item Standard Cost management.
/// Per DO-NOT: "Allow Item Standard Cost with future effective_date (must be ≤ today)"
/// Per DO-NOT: "Allow Item Standard Cost effective_date before last SLE posting_date"
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class ItemStandardCostAppService : ApplicationService, IItemStandardCostAppService
{
    private readonly IRepository<ItemStandardCost, Guid> _repository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public ItemStandardCostAppService(
        IRepository<ItemStandardCost, Guid> repository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _repository = repository;
        _sleRepository = sleRepository;
        _itemRepository = itemRepository;
    }

    public async Task<PagedResultDto<ItemStandardCostDto>> GetListAsync(GetItemStandardCostListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (input.ItemId.HasValue)
            query = query.Where(x => x.ItemId == input.ItemId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
            query = query.Where(x => x.Status == DocumentStatus.Submitted);

        var count = query.Count();
        var items = query.OrderByDescending(x => x.EffectiveDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ItemStandardCostDto>(count,
            items.Select(ObjectMapper.Map<ItemStandardCost, ItemStandardCostDto>).ToList());
    }

    public async Task<ItemStandardCostDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<ItemStandardCost, ItemStandardCostDto>(entity);
    }

    /// <summary>Get the current effective standard cost for an item.</summary>
    public async Task<ItemStandardCostDto?> GetCurrentAsync(Guid itemId, Guid companyId)
    {
        var query = await _repository.GetQueryableAsync();
        var current = query
            .Where(x => x.ItemId == itemId && x.CompanyId == companyId
                        && x.Status == DocumentStatus.Submitted
                        && x.EffectiveDate <= DateTime.UtcNow.Date)
            .OrderByDescending(x => x.EffectiveDate)
            .FirstOrDefault();
        return current != null ? ObjectMapper.Map<ItemStandardCost, ItemStandardCostDto>(current) : null;
    }

    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<ItemStandardCostDto> CreateAsync(CreateItemStandardCostDto input)
    {
        // Validate against last SLE
        var sleQuery = await _sleRepository.GetQueryableAsync();
        var lastSleDate = sleQuery
            .Where(s => s.ItemId == input.ItemId)
            .OrderByDescending(s => s.PostingDate)
            .Select(s => (DateTime?)s.PostingDate)
            .FirstOrDefault();

        // Get previous rate for PPV tracking
        var existingQuery = await _repository.GetQueryableAsync();
        var previousRate = existingQuery
            .Where(x => x.ItemId == input.ItemId && x.CompanyId == input.CompanyId
                        && x.Status == DocumentStatus.Submitted)
            .OrderByDescending(x => x.EffectiveDate)
            .Select(x => (decimal?)x.StandardRate)
            .FirstOrDefault();

        var entity = new ItemStandardCost(GuidGenerator.Create(), input.CompanyId,
            input.ItemId, input.StandardRate, input.EffectiveDate, CurrentTenant.Id);

        entity.ValidateAgainstLastSle(lastSleDate);
        entity.PreviousRate = previousRate;

        await _repository.InsertAsync(entity);
        return ObjectMapper.Map<ItemStandardCost, ItemStandardCostDto>(entity);
    }

    [Authorize(MyERPPermissions.StockEntries.Submit)]
    public async Task<ItemStandardCostDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Submit();
        await _repository.UpdateAsync(entity, autoSave: true);

        // Entity is now the latest submitted, currently-effective record for this item/company
        // (or will be superseded by a later-dated one on the next sync) — always push it through,
        // since the user just explicitly asked this rate to take effect.
        await SyncItemStandardBuyingPriceAsync(entity.ItemId, entity.CompanyId, fallbackToNull: false);

        return ObjectMapper.Map<ItemStandardCost, ItemStandardCostDto>(entity);
    }

    [Authorize(MyERPPermissions.StockEntries.Cancel)]
    public async Task<ItemStandardCostDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);

        // Check for stock activity on or after effective date
        var sleQuery = await _sleRepository.GetQueryableAsync();
        var hasActivity = sleQuery.Any(s => s.ItemId == entity.ItemId
                                            && s.PostingDate >= entity.EffectiveDate);

        entity.Cancel(hasActivity);
        await _repository.UpdateAsync(entity, autoSave: true);

        // Fall back to whatever submitted record is now current. If none remains, leave
        // Item.StandardBuyingPrice untouched rather than nulling it — some items still carry a
        // value set directly on the Item form (ItemAppService.UpdateAsync) that never went
        // through this workflow, and zeroing that out on cancel would be a bigger regression
        // than a stale rate.
        await SyncItemStandardBuyingPriceAsync(entity.ItemId, entity.CompanyId, fallbackToNull: false);

        return ObjectMapper.Map<ItemStandardCost, ItemStandardCostDto>(entity);
    }

    /// <summary>
    /// Keeps Item.StandardBuyingPrice — the field StockValuationService.CalculateStandardCost
    /// actually reads for Standard Cost valuation — in sync with the latest submitted,
    /// currently-effective Item Standard Cost record. The Submit/Cancel workflow otherwise had
    /// no effect on valuation at all: it validated and tracked PreviousRate for PPV, but never
    /// wrote back to the field the valuation engine reads.
    /// </summary>
    private async Task SyncItemStandardBuyingPriceAsync(Guid itemId, Guid companyId, bool fallbackToNull)
    {
        var query = await _repository.GetQueryableAsync();
        var current = query
            .Where(x => x.ItemId == itemId && x.CompanyId == companyId
                        && x.Status == DocumentStatus.Submitted
                        && x.EffectiveDate <= DateTime.UtcNow.Date)
            .OrderByDescending(x => x.EffectiveDate)
            .Select(x => (decimal?)x.StandardRate)
            .FirstOrDefault();

        if (current == null && !fallbackToNull)
            return;

        var item = await _itemRepository.FindAsync(itemId);
        if (item != null && item.StandardBuyingPrice != current)
        {
            item.StandardBuyingPrice = current;
            await _itemRepository.UpdateAsync(item, autoSave: true);
        }
    }
}
