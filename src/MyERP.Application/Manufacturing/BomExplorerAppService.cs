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
/// BOM Explorer — multi-level hierarchical Bill of Materials explosion with costing and stock availability.
/// Maps to ERPNext manufacturing/report/bom_explorer/bom_explorer.py.
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class BomExplorerAppService : ApplicationService, IBomExplorerAppService
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public BomExplorerAppService(
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _bomRepository = bomRepository;
        _itemRepository = itemRepository;
        _binRepository = binRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<List<BomLookupDto>> GetBomListAsync(Guid companyId)
    {
        var bomQuery = await _bomRepository.GetQueryableAsync();
        var boms = bomQuery
            .Where(b => b.CompanyId == companyId && b.IsActive)
            .OrderBy(b => b.BomNumber)
            .ToList();

        if (!boms.Any())
        {
            return new List<BomLookupDto>();
        }

        var itemIds = boms.Select(b => b.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var items = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionary(i => i.Id, i => i);

        return boms.Select(b =>
        {
            var item = items.GetValueOrDefault(b.ItemId);
            return new BomLookupDto
            {
                Id = b.Id,
                BomNumber = b.BomNumber,
                ItemId = b.ItemId,
                ItemCode = item?.ItemCode ?? string.Empty,
                ItemName = item?.ItemName ?? string.Empty,
                Quantity = b.Quantity,
                Uom = b.Uom,
                IsDefault = b.IsDefault
            };
        }).ToList();
    }

    public async Task<BomExplorerDto> GetExplorerAsync(BomExplorerRequestDto input)
    {
        var rootBomQuery = await _bomRepository.WithDetailsAsync(b => b.Items);
        var allCompanyBoms = rootBomQuery
            .Where(b => b.CompanyId == input.CompanyId)
            .ToList();

        var bomsById = allCompanyBoms.ToDictionary(b => b.Id, b => b);

        if (!bomsById.TryGetValue(input.BomId, out var rootBom))
        {
            return new BomExplorerDto
            {
                BomId = input.BomId,
                BomNumber = string.Empty,
                ItemId = Guid.Empty,
                ItemCode = string.Empty,
                ItemName = string.Empty
            };
        }

        if (!rootBom.IsActive)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.BomInactive)
                .WithData("bomNumber", rootBom.BomNumber);
        }

        var multiplier = input.Quantity > 0 ? input.Quantity : 1m;
        var baseQty = rootBom.Quantity > 0 ? rootBom.Quantity : 1m;
        var initialFactor = multiplier / baseQty;

        // Collect all items across all company BOMs
        var allItemIds = allCompanyBoms
            .Select(b => b.ItemId)
            .Concat(allCompanyBoms.SelectMany(b => b.Items.Select(i => i.ItemId)))
            .Distinct()
            .ToList();

        var itemQuery = await _itemRepository.GetQueryableAsync();
        var items = itemQuery
            .Where(i => allItemIds.Contains(i.Id))
            .ToDictionary(i => i.Id, i => i);

        // Fetch stock
        var warehouseQuery = await _warehouseRepository.GetQueryableAsync();
        var companyWarehouses = warehouseQuery.Where(w => w.CompanyId == input.CompanyId).ToList();
        var warehouseIds = input.WarehouseId.HasValue
            ? new List<Guid> { input.WarehouseId.Value }
            : companyWarehouses.Select(w => w.Id).ToList();

        var binQuery = await _binRepository.GetQueryableAsync();
        var bins = binQuery
            .Where(b => warehouseIds.Contains(b.WarehouseId))
            .ToList();

        var binStockMap = bins
            .GroupBy(b => b.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.ActualQty));

        var rows = new List<BomExplorerRowDto>();
        var totalCost = 0m;
        var totalSubBoms = 0;

        void Explode(Guid currentBomId, decimal parentFactor, int level, HashSet<Guid> path)
        {
            if (path.Contains(currentBomId))
            {
                return; // Cycle-safe
            }

            path.Add(currentBomId);

            if (!bomsById.TryGetValue(currentBomId, out var currentBom))
            {
                return;
            }

            foreach (var bomItem in currentBom.Items)
            {
                var scaledQty = parentFactor * bomItem.Quantity;
                var scaledStockQty = parentFactor * bomItem.StockQty;
                var lineAmount = Math.Round(scaledQty * bomItem.Rate, 2);
                totalCost += lineAmount;

                var item = items.GetValueOrDefault(bomItem.ItemId);
                var itemCode = item?.ItemCode ?? bomItem.ItemName;
                var itemName = item?.ItemName ?? bomItem.ItemName;
                var desc = item?.Description ?? bomItem.ItemName;

                var availableStock = binStockMap.GetValueOrDefault(bomItem.ItemId);
                var hasSufficientStock = availableStock >= scaledStockQty;

                BillOfMaterials? subBom = null;
                if (bomItem.SubBomId.HasValue && bomsById.TryGetValue(bomItem.SubBomId.Value, out var foundSubBom))
                {
                    subBom = foundSubBom;
                    totalSubBoms++;
                }

                rows.Add(new BomExplorerRowDto
                {
                    ItemId = bomItem.ItemId,
                    ItemCode = itemCode,
                    ItemName = itemName,
                    BomLevel = level,
                    Indent = level,
                    SubBomId = bomItem.SubBomId,
                    SubBomNumber = subBom?.BomNumber,
                    Qty = Math.Round(scaledQty, 4),
                    Uom = bomItem.Uom,
                    StockQty = Math.Round(scaledStockQty, 4),
                    StockUom = bomItem.StockUom,
                    Rate = bomItem.Rate,
                    Amount = lineAmount,
                    IsPhantom = bomItem.IsPhantom,
                    DoNotExplode = bomItem.DoNotExplode,
                    Description = desc,
                    AvailableQty = Math.Round(availableStock, 2),
                    HasSufficientStock = hasSufficientStock
                });

                if (bomItem.SubBomId.HasValue && !bomItem.DoNotExplode && subBom != null)
                {
                    var childBomQty = subBom.Quantity > 0 ? subBom.Quantity : 1m;
                    var childFactor = scaledStockQty / childBomQty;
                    Explode(bomItem.SubBomId.Value, childFactor, level + 1, new HashSet<Guid>(path));
                }
            }
        }

        Explode(rootBom.Id, initialFactor, 0, new HashSet<Guid>());

        var rootItem = items.GetValueOrDefault(rootBom.ItemId);

        return new BomExplorerDto
        {
            BomId = rootBom.Id,
            BomNumber = rootBom.BomNumber,
            ItemId = rootBom.ItemId,
            ItemCode = rootItem?.ItemCode ?? string.Empty,
            ItemName = rootItem?.ItemName ?? string.Empty,
            BaseQuantity = rootBom.Quantity,
            ExplodedQuantity = multiplier,
            Uom = rootBom.Uom,
            Rows = rows,
            TotalComponents = rows.Count,
            TotalSubBoms = totalSubBoms,
            TotalCost = totalCost
        };
    }
}
