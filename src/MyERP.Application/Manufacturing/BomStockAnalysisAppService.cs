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

        // Batch-resolve item code, name, and description (ERPNext PR #47116 / commit b6b4ac5b4a)
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var items = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName, i.Description })
            .ToList();
        var itemMap = items.ToDictionary(i => i.Id);

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

        var groupedBomItems = bom.Items
            .GroupBy(i => i.ItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                TotalQuantity = g.Sum(i => i.Quantity)
            })
            .ToList();

        foreach (var bomItem in groupedBomItems)
        {
            var requiredForBatch = bomItem.TotalQuantity * (requiredQty / (bom.Quantity > 0 ? bom.Quantity : 1));
            var available = stockMap.GetValueOrDefault(bomItem.ItemId, 0);
            var shortage = Math.Max(0, requiredForBatch - available);
            var canMake = requiredForBatch > 0 ? available / requiredForBatch : decimal.MaxValue;
            var itemDetails = itemMap.GetValueOrDefault(bomItem.ItemId);

            materialLines.Add(new BomMaterialAvailabilityDto
            {
                ItemId = bomItem.ItemId,
                ItemCode = itemDetails?.ItemCode ?? bomItem.ItemId.ToString().Substring(0, 8),
                ItemName = itemDetails?.ItemName ?? "Unknown Item",
                Description = itemDetails?.Description,
                RequiredQtyPerUnit = bomItem.TotalQuantity / (bom.Quantity > 0 ? bom.Quantity : 1),
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
