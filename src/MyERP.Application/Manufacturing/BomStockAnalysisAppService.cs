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

[Authorize(MyERPPermissions.Manufacturing.Default)]
public class BomStockAnalysisAppService : ApplicationService, IBomStockAnalysisAppService
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public BomStockAnalysisAppService(
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _bomRepository = bomRepository;
        _binRepository = binRepository;
        _itemRepository = itemRepository;
    }

    public async Task<BomStockAnalysisDto> GetAnalysisAsync(Guid bomId, decimal requiredQty = 1)
    {
        var bom = await _bomRepository.GetAsync(bomId);
        if (bom.Items == null || !bom.Items.Any())
            return new BomStockAnalysisDto { BomId = bomId, BomNumber = bom.BomNumber, CanManufactureQty = 0 };

        // Resolve BOM item name
        var fgItem = await _itemRepository.FindAsync(bom.ItemId);

        var itemIds = bom.Items.Select(i => i.ItemId).Distinct().ToList();

        // Batch-resolve item names
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var items = itemQuery.Where(i => itemIds.Contains(i.Id)).Select(i => new { i.Id, i.ItemCode, i.ItemName }).ToList();
        var itemNameMap = items.ToDictionary(i => i.Id, i => $"{i.ItemCode} - {i.ItemName}");

        // Batch-resolve available stock per item (sum across all warehouses for the company)
        var binQuery = await _binRepository.GetQueryableAsync();
        var binData = binQuery
            .Where(b => itemIds.Contains(b.ItemId))
            .GroupBy(b => b.ItemId)
            .Select(g => new { ItemId = g.Key, AvailableQty = g.Sum(b => b.ActualQty - b.ReservedQty) })
            .ToList();
        var stockMap = binData.ToDictionary(b => b.ItemId, b => b.AvailableQty);

        var materialLines = new List<BomMaterialAvailabilityDto>();
        decimal minManufacturable = decimal.MaxValue;

        foreach (var bomItem in bom.Items)
        {
            var requiredForBatch = bomItem.Quantity * (requiredQty / (bom.Quantity > 0 ? bom.Quantity : 1));
            var available = stockMap.GetValueOrDefault(bomItem.ItemId, 0);
            var shortage = Math.Max(0, requiredForBatch - available);
            var canMake = requiredForBatch > 0 ? available / requiredForBatch : decimal.MaxValue;

            materialLines.Add(new BomMaterialAvailabilityDto
            {
                ItemId = bomItem.ItemId,
                ItemName = itemNameMap.GetValueOrDefault(bomItem.ItemId, bomItem.ItemId.ToString().Substring(0, 8)),
                RequiredQtyPerUnit = bomItem.Quantity / (bom.Quantity > 0 ? bom.Quantity : 1),
                RequiredQtyForBatch = Math.Round(requiredForBatch, 4),
                AvailableQty = Math.Round(available, 4),
                Shortage = Math.Round(shortage, 4),
                IsSufficient = shortage <= 0,
            });

            if (canMake < minManufacturable) minManufacturable = canMake;
        }

        var canManufacture = minManufacturable == decimal.MaxValue ? 0 : Math.Floor(minManufacturable * (bom.Quantity > 0 ? bom.Quantity : 1));

        return new BomStockAnalysisDto
        {
            BomId = bomId,
            BomNumber = bom.BomNumber,
            ItemName = fgItem?.ItemName ?? "",
            BomQuantity = bom.Quantity,
            RequestedQty = requiredQty,
            CanManufactureQty = canManufacture,
            AllMaterialsSufficient = materialLines.All(m => m.IsSufficient),
            Materials = materialLines
        };
    }
}
