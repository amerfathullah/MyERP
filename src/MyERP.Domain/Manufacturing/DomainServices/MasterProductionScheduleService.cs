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

    public MasterProductionScheduleService(IRepository<Item, Guid> itemRepository, IRepository<BillOfMaterials, Guid> bomRepository)
    {
        _itemRepository = itemRepository;
        _bomRepository = bomRepository;
    }

    /// <summary>Resolves the item's default BOM, if any, then delegates to the recursive calculation.</summary>
    public async Task<int> GetCumulativeLeadTimeDaysAsync(Guid itemId, Guid? bomId = null)
    {
        var visited = new HashSet<Guid>();
        return await GetCumulativeLeadTimeRecursiveAsync(itemId, bomId, visited);
    }

    private async Task<int> GetCumulativeLeadTimeRecursiveAsync(Guid itemId, Guid? bomId, HashSet<Guid> visitedBoms)
    {
        var item = await _itemRepository.FindAsync(itemId);
        var leadTime = item?.LeadTimeDays ?? 0;

        var resolvedBomId = bomId ?? item?.DefaultBomId;
        if (resolvedBomId is not { } id || !visitedBoms.Add(id))
            return leadTime;

        var bom = await _bomRepository.FindAsync(id);
        if (bom is null)
            return leadTime;

        foreach (var bomItem in bom.Items)
        {
            leadTime += await GetCumulativeLeadTimeRecursiveAsync(bomItem.ItemId, bomItem.SubBomId, visitedBoms);
        }

        return leadTime;
    }
}
