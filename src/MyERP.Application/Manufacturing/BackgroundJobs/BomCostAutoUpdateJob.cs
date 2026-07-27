using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Manufacturing.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing.BackgroundJobs;

/// <summary>
/// Background job that auto-updates BOM costs when raw material prices change.
/// Per ERPNext: Manufacturing Settings.update_bom_costs_automatically → daily scheduler.
/// 
/// Algorithm:
/// 1. Find all active BOMs for the company
/// 2. Bottom-up level-wise processing (leaf BOMs first, then parents)
/// 3. For each BOM: recalculate material + operating cost from child items/sub-BOMs
/// 4. If cost changed → update and propagate to parent BOMs
/// 
/// Per gotcha #2638: concurrency=1, 1-day stale detection.
/// </summary>
public class BomCostAutoUpdateJob : AsyncBackgroundJob<BomCostAutoUpdateJobArgs>, ITransientDependency
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly ILogger<BomCostAutoUpdateJob> _logger;

    public BomCostAutoUpdateJob(
        IRepository<BillOfMaterials, Guid> bomRepository,
        ILogger<BomCostAutoUpdateJob> logger)
    {
        _bomRepository = bomRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(BomCostAutoUpdateJobArgs args)
    {
        _logger.LogInformation("BomCostAutoUpdateJob: Processing company {CompanyId}", args.CompanyId);

        var allBoms = await _bomRepository.GetListAsync(b => b.CompanyId == args.CompanyId && b.IsActive);

        if (!allBoms.Any())
        {
            _logger.LogInformation("BomCostAutoUpdateJob: No active BOMs for company {CompanyId}", args.CompanyId);
            return;
        }

        // Build dependency map: BomId → set of parent BomIds that reference it
        var parentMap = new Dictionary<Guid, HashSet<Guid>>();
        var bomDict = allBoms.ToDictionary(b => b.Id);

        foreach (var bom in allBoms)
        {
            foreach (var item in bom.Items)
            {
                if (item.SubBomId.HasValue && bomDict.ContainsKey(item.SubBomId.Value))
                {
                    if (!parentMap.ContainsKey(item.SubBomId.Value))
                        parentMap[item.SubBomId.Value] = new HashSet<Guid>();
                    parentMap[item.SubBomId.Value].Add(bom.Id);
                }
            }
        }

        // Level-wise processing: leaf BOMs first (no sub-BOMs), then progressively upward
        var processed = new HashSet<Guid>();
        var updatedCount = 0;
        var queue = new Queue<BillOfMaterials>();

        // Start with leaf BOMs (BOMs that have no sub-BOM references in their items)
        foreach (var bom in allBoms)
        {
            var hasSubBom = bom.Items.Any(i => i.SubBomId.HasValue && bomDict.ContainsKey(i.SubBomId.Value));
            if (!hasSubBom)
            {
                queue.Enqueue(bom);
            }
        }

        while (queue.Count > 0)
        {
            var bom = queue.Dequeue();
            if (processed.Contains(bom.Id)) continue;

            try
            {
                var previousCost = bom.TotalCost;

                // Update sub-BOM item rates from child BOM costs
                foreach (var item in bom.Items.Where(i => i.SubBomId.HasValue))
                {
                    if (item.SubBomId.HasValue && bomDict.TryGetValue(item.SubBomId.Value, out var childBom))
                    {
                        // Sub-assembly rate = child BOM total cost / child BOM quantity
                        var newRate = childBom.Quantity > 0 ? childBom.TotalCost / childBom.Quantity : 0;
                        if (newRate != item.Rate)
                        {
                            item.Rate = newRate;
                            item.Amount = item.Quantity * newRate;
                        }
                    }
                }

                bom.RecalculateCost();

                if (Math.Abs(bom.TotalCost - previousCost) > 0.01m)
                {
                    await _bomRepository.UpdateAsync(bom, autoSave: true);
                    updatedCount++;
                }

                processed.Add(bom.Id);

                // Enqueue parent BOMs whose dependencies are now all processed
                if (parentMap.TryGetValue(bom.Id, out var parents))
                {
                    foreach (var parentId in parents)
                    {
                        if (processed.Contains(parentId)) continue;
                        if (!bomDict.TryGetValue(parentId, out var parentBom)) continue;

                        // Check if all sub-BOMs of this parent have been processed
                        var allChildrenProcessed = parentBom.Items
                            .Where(i => i.SubBomId.HasValue && bomDict.ContainsKey(i.SubBomId.Value))
                            .All(i => processed.Contains(i.SubBomId!.Value));

                        if (allChildrenProcessed)
                        {
                            queue.Enqueue(parentBom);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BomCostAutoUpdateJob: Failed to update BOM {BomId}", bom.Id);
                processed.Add(bom.Id); // Mark as processed to avoid infinite loop
            }
        }

        _logger.LogInformation(
            "BomCostAutoUpdateJob: Company {CompanyId} — {Updated}/{Total} BOMs updated",
            args.CompanyId, updatedCount, allBoms.Count);
    }
}

public class BomCostAutoUpdateJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}
