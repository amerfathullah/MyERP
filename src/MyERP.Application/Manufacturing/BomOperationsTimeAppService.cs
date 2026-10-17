using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// BOM Operations Time report — analyzes operation runtimes, workstation assignments,
/// and sub-assembly BOM usage counts across active Bills of Materials.
/// ERPNext equivalent: manufacturing/report/bom_operations_time/bom_operations_time.py
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class BomOperationsTimeAppService : ApplicationService, IBomOperationsTimeAppService
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<BomItem, Guid> _bomItemRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Operation, Guid> _operationRepository;
    private readonly IRepository<Workstation, Guid> _workstationRepository;

    public BomOperationsTimeAppService(
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<BomItem, Guid> bomItemRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Operation, Guid> operationRepository,
        IRepository<Workstation, Guid> workstationRepository)
    {
        _bomRepository = bomRepository;
        _bomItemRepository = bomItemRepository;
        _itemRepository = itemRepository;
        _operationRepository = operationRepository;
        _workstationRepository = workstationRepository;
    }

    public async Task<BomOperationsTimeReportDto> GetReportAsync(BomOperationsTimeRequestDto input)
    {
        var bomQueryable = await _bomRepository.WithDetailsAsync(b => b.Operations);
        var query = bomQueryable
            .Where(b => b.CompanyId == input.CompanyId && b.IsActive && b.WithOperations);

        if (input.BomId.HasValue)
        {
            query = query.Where(b => b.Id == input.BomId.Value);
        }

        if (input.ItemId.HasValue)
        {
            query = query.Where(b => b.ItemId == input.ItemId.Value);
        }

        var boms = query.ToList();

        // Filter operations by Workstation if specified
        var filteredBoms = new List<(BillOfMaterials Bom, List<BomOperation> Ops)>();
        foreach (var bom in boms)
        {
            var ops = bom.Operations.AsEnumerable();
            if (input.WorkstationId.HasValue)
            {
                ops = ops.Where(o => o.WorkstationId == input.WorkstationId.Value);
            }

            var opList = ops.OrderBy(o => o.SequenceId).ToList();
            if (opList.Count > 0)
            {
                filteredBoms.Add((bom, opList));
            }
        }

        if (filteredBoms.Count == 0)
        {
            return new BomOperationsTimeReportDto();
        }

        var bomIds = filteredBoms.Select(x => x.Bom.Id).Distinct().ToList();
        var itemIds = filteredBoms.Select(x => x.Bom.ItemId).Distinct().ToList();
        var opIds = filteredBoms.SelectMany(x => x.Ops.Select(o => o.OperationId)).Distinct().ToList();
        var wsIds = filteredBoms.SelectMany(x => x.Ops.Where(o => o.WorkstationId.HasValue).Select(o => o.WorkstationId!.Value)).Distinct().ToList();

        // 1. Fetch Item master details
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var items = itemQuery.Where(i => itemIds.Contains(i.Id)).ToList();
        var itemMap = items.ToDictionary(i => i.Id, i => i);

        // 2. Fetch Operation master details
        var opQuery = await _operationRepository.GetQueryableAsync();
        var operations = opQuery.Where(o => opIds.Contains(o.Id)).ToList();
        var opMap = operations.ToDictionary(o => o.Id, o => o.Name);

        // 3. Fetch Workstation details
        var wsQuery = await _workstationRepository.GetQueryableAsync();
        var workstations = wsQuery.Where(w => wsIds.Contains(w.Id)).ToList();
        var wsMap = workstations.ToDictionary(w => w.Id, w => w.Name);

        // 4. Calculate Sub-assembly BOM count (how many times this BOM is used as a sub-assembly in other BOM items)
        var bomItemQuery = await _bomItemRepository.GetQueryableAsync();
        var subBomUsage = bomItemQuery
            .Where(bi => bi.SubBomId.HasValue && bomIds.Contains(bi.SubBomId.Value))
            .GroupBy(bi => bi.SubBomId!.Value)
            .Select(g => new { SubBomId = g.Key, Count = g.Count() })
            .ToList();
        var subBomCountMap = subBomUsage.ToDictionary(x => x.SubBomId, x => x.Count);

        // 5. Construct rows
        var rows = new List<BomOperationsTimeRowDto>();
        foreach (var (bom, ops) in filteredBoms.OrderBy(x => x.Bom.BomNumber))
        {
            itemMap.TryGetValue(bom.ItemId, out var item);
            var itemCode = item?.ItemCode ?? string.Empty;
            var itemName = item?.ItemName ?? string.Empty;
            var uom = bom.Uom ?? item?.Uom ?? "Unit";
            var subAssemblyCount = subBomCountMap.GetValueOrDefault(bom.Id, 0);

            foreach (var op in ops)
            {
                opMap.TryGetValue(op.OperationId, out var opName);
                string? wsName = null;
                if (op.WorkstationId.HasValue)
                {
                    wsMap.TryGetValue(op.WorkstationId.Value, out wsName);
                }

                rows.Add(new BomOperationsTimeRowDto
                {
                    BomId = bom.Id,
                    BomNumber = bom.BomNumber,
                    ItemId = bom.ItemId,
                    ItemCode = itemCode,
                    ItemName = itemName,
                    Uom = uom,
                    OperationId = op.OperationId,
                    OperationName = opName ?? string.Empty,
                    WorkstationId = op.WorkstationId,
                    WorkstationName = wsName,
                    TimeInMins = op.TimeInMins,
                    UsedAsSubassemblyItems = subAssemblyCount
                });
            }
        }

        return new BomOperationsTimeReportDto
        {
            Rows = rows,
            TotalBoms = rows.Select(r => r.BomId).Distinct().Count(),
            TotalOperations = rows.Count,
            TotalTimeInMinutes = rows.Sum(r => r.TimeInMins)
        };
    }
}
