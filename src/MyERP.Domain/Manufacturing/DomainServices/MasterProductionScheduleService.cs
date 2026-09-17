using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Manufacturing.DomainServices;

/// <summary>
/// Computes cumulative lead time for Master Production Schedule demand aggregation.
/// Per ERPNext MasterProductionSchedule.get_cumulative_lead_time(): sums the item's own
/// lead time with the lead times of every raw material down its BOM tree (recursively,
/// through sub-assemblies), so the order/production release date can be backed off from
/// the required delivery date.
/// </summary>
public class MasterProductionScheduleService : DomainService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<ItemLeadTime, Guid>? _itemLeadTimeRepository;

    public MasterProductionScheduleService(
        IRepository<Item, Guid> itemRepository,
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<ItemLeadTime, Guid>? itemLeadTimeRepository = null)
    {
        _itemRepository = itemRepository;
        _bomRepository = bomRepository;
        _itemLeadTimeRepository = itemLeadTimeRepository;
    }

    /// <summary>
    /// Resolves the item's default BOM, if any, then delegates to the recursive calculation.
    /// Supports required quantity scaling for manufacturing duration per ERPNext PR #59007 / commit 2ad4a8c4a4.
    /// </summary>
    public async Task<int> GetCumulativeLeadTimeDaysAsync(Guid itemId, Guid? bomId = null, decimal qty = 1)
    {
        var visited = new HashSet<Guid>();
        return await GetCumulativeLeadTimeRecursiveAsync(itemId, bomId, qty, visited);
    }

    private async Task<int> GetCumulativeLeadTimeRecursiveAsync(Guid itemId, Guid? bomId, decimal qty, HashSet<Guid> visitedBoms)
    {
        var item = await _itemRepository.FindAsync(itemId);
        var resolvedBomId = bomId ?? item?.DefaultBomId;
        var isManufacture = resolvedBomId.HasValue;

        var leadTime = await ResolveItemLeadTimeAsync(itemId, isManufacture, qty, item?.LeadTimeDays ?? 0);

        if (resolvedBomId is not { } id || !visitedBoms.Add(id))
            return leadTime;

        var bom = await _bomRepository.FindAsync(id);
        if (bom is null)
            return leadTime;

        var bomBaseQty = bom.Quantity > 0 ? bom.Quantity : 1m;
        foreach (var bomItem in bom.Items)
        {
            var componentQty = (bomItem.Quantity / bomBaseQty) * (qty > 0 ? qty : 1m);
            leadTime += await GetCumulativeLeadTimeRecursiveAsync(bomItem.ItemId, bomItem.SubBomId, componentQty, visitedBoms);
        }

        return leadTime;
    }

    private async Task<int> ResolveItemLeadTimeAsync(Guid itemId, bool isManufacture, decimal qty, int fallbackDays)
    {
        var leadTimeRepo = _itemLeadTimeRepository ?? LazyServiceProvider.LazyGetService<IRepository<ItemLeadTime, Guid>>();
        if (leadTimeRepo != null)
        {
            var leadTimeQ = await leadTimeRepo.GetQueryableAsync();
            var itemLeadTime = leadTimeQ.FirstOrDefault(lt => lt.ItemId == itemId);
            if (itemLeadTime != null)
            {
                return itemLeadTime.CalculateLeadTimeDays(qty, isManufacture);
            }
        }

        return fallbackDays;
    }
}
