using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Dtos;
using MyERP.Inventory.DomainServices;
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
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly StockValuationService _valuationService;
    private readonly IStockReconciliationAppService _stockReconciliationAppService;

    public ItemStandardCostAppService(
        IRepository<ItemStandardCost, Guid> repository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Company, Guid> companyRepository,
        StockValuationService valuationService,
        IStockReconciliationAppService stockReconciliationAppService)
    {
        _repository = repository;
        _sleRepository = sleRepository;
        _itemRepository = itemRepository;
        _companyRepository = companyRepository;
        _valuationService = valuationService;
        _stockReconciliationAppService = stockReconciliationAppService;
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
        var appliedRate = await SyncItemStandardBuyingPriceAsync(entity.ItemId, entity.CompanyId, fallbackToNull: false);

        // Per the entity's own doc comment ("Creates auto-revaluation Stock Reconciliation on
        // submit for all warehouses with stock") — only meaningful when this record actually WON
        // (a later-dated record could have superseded it already) and the rate genuinely changed;
        // a first-ever Standard Cost record for an item with no prior rate has nothing to revalue
        // FROM (existing stock's book value, if any, came from FIFO/moving-average history, not a
        // stale standard cost — reposting a full valuation-method switch is a separate, bigger
        // concern deliberately not attempted here).
        if (appliedRate == entity.StandardRate && entity.PreviousRate.HasValue && entity.PreviousRate != entity.StandardRate)
        {
            entity.RevaluationStockReconciliationId = await CreateRevaluationReconciliationAsync(entity);
            await _repository.UpdateAsync(entity, autoSave: true);
        }

        return ObjectMapper.Map<ItemStandardCost, ItemStandardCostDto>(entity);
    }

    /// <summary>
    /// Re-bases every warehouse currently holding stock of this item to the new standard rate via
    /// a real Stock Reconciliation (same quantity, new valuation rate — a pure revaluation, no
    /// physical count change). StockValuationService.CalculateStandardCost values a Standard Cost
    /// item purely off Item.StandardBuyingPrice regardless of the SLE's own IncomingRate, so once
    /// that field is synced (above), submitting a zero-quantity-change reconciliation at the new
    /// rate is all that's needed to correctly re-value the existing balance — StockReconciliation-
    /// AppService already posts the resulting GL difference per warehouse.
    /// </summary>
    private async Task<Guid?> CreateRevaluationReconciliationAsync(ItemStandardCost entity)
    {
        var sleQuery = await _sleRepository.GetQueryableAsync();
        var warehouseIds = sleQuery
            .Where(s => s.ItemId == entity.ItemId)
            .Select(s => s.WarehouseId)
            .Distinct()
            .ToList();

        var rows = new List<CreateStockReconciliationItemDto>();
        foreach (var warehouseId in warehouseIds)
        {
            var balance = await _valuationService.GetCurrentBalanceAsync(entity.ItemId, warehouseId);
            if (balance.Quantity == 0) continue;

            rows.Add(new CreateStockReconciliationItemDto
            {
                ItemId = entity.ItemId,
                WarehouseId = warehouseId,
                CurrentQuantity = balance.Quantity,
                CurrentValuationRate = balance.ValuationRate,
                NewQuantity = balance.Quantity,
                NewValuationRate = entity.StandardRate,
            });
        }

        if (rows.Count == 0) return null;

        var company = await _companyRepository.GetAsync(entity.CompanyId);

        var created = await _stockReconciliationAppService.CreateAsync(new CreateStockReconciliationDto
        {
            CompanyId = entity.CompanyId,
            PostingDate = entity.EffectiveDate,
            Purpose = "Item Standard Cost revaluation",
            Notes = $"Auto-created by Item Standard Cost effective {entity.EffectiveDate:yyyy-MM-dd} (rate {entity.PreviousRate:N4} -> {entity.StandardRate:N4}).",
            ExpenseAccountId = company.DefaultStockAdjustmentAccountId,
            Items = rows.ToArray(),
        });

        // StockReconciliationAppService.CreateAsync inserts with autoSave:false — fine for its own
        // normal (separate-request) Create-then-Submit flow, since the ambient UnitOfWork commits
        // at the end of that call. Here both calls share the SAME ambient UnitOfWork (this method
        // runs inside ItemStandardCostAppService.SubmitAsync), so without an explicit flush the
        // reconciliation SubmitAsync below queries the DB directly and finds zero rows. Same
        // pattern as JobCardAppService.FlushPendingChangesAsync.
        var uowManager = LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>();
        if (uowManager.Current != null)
        {
            await uowManager.Current.SaveChangesAsync();
        }

        await _stockReconciliationAppService.SubmitAsync(created.Id);
        return created.Id;
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
    private async Task<decimal?> SyncItemStandardBuyingPriceAsync(Guid itemId, Guid companyId, bool fallbackToNull)
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
            return null;

        var item = await _itemRepository.FindAsync(itemId);
        if (item != null && item.StandardBuyingPrice != current)
        {
            item.StandardBuyingPrice = current;
            await _itemRepository.UpdateAsync(item, autoSave: true);
        }

        return current;
    }
}
