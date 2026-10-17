using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.StockEntries.Default)]
public class ItemPriceStockAppService : ApplicationService, IItemPriceStockAppService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<ItemPrice, Guid> _itemPriceRepository;
    private readonly IRepository<PriceList, Guid> _priceListRepository;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;

    public ItemPriceStockAppService(
        IRepository<Item, Guid> itemRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<ItemPrice, Guid> itemPriceRepository,
        IRepository<PriceList, Guid> priceListRepository,
        IRepository<ItemGroup, Guid> itemGroupRepository)
    {
        _itemRepository = itemRepository;
        _binRepository = binRepository;
        _warehouseRepository = warehouseRepository;
        _itemPriceRepository = itemPriceRepository;
        _priceListRepository = priceListRepository;
        _itemGroupRepository = itemGroupRepository;
    }

    public async Task<ItemPriceStockReportDto> GetReportAsync(ItemPriceStockFilterDto input)
    {
        var now = DateTime.UtcNow.Date;

        // 1. Fetch items
        var itemQuery = await _itemRepository.GetQueryableAsync();
        itemQuery = itemQuery.Where(i => i.IsActive && i.MaintainStock);

        if (input.CompanyId.HasValue && input.CompanyId != Guid.Empty)
        {
            itemQuery = itemQuery.Where(i => i.CompanyId == input.CompanyId.Value);
        }

        if (input.ItemId.HasValue && input.ItemId != Guid.Empty)
        {
            itemQuery = itemQuery.Where(i => i.Id == input.ItemId.Value);
        }

        if (input.ItemGroupId.HasValue && input.ItemGroupId != Guid.Empty)
        {
            itemQuery = itemQuery.Where(i => i.ItemGroupId == input.ItemGroupId.Value);
        }

        var items = itemQuery.ToList();
        if (items.Count == 0)
        {
            return new ItemPriceStockReportDto();
        }

        var itemMap = items.ToDictionary(i => i.Id);
        var itemIds = itemMap.Keys.ToHashSet();

        // 2. Fetch warehouses
        var whQuery = await _warehouseRepository.GetQueryableAsync();
        if (input.CompanyId.HasValue && input.CompanyId != Guid.Empty)
        {
            whQuery = whQuery.Where(w => w.CompanyId == input.CompanyId.Value);
        }
        var warehouses = whQuery.Where(w => !w.IsGroup && w.IsActive).ToList();
        var whMap = warehouses.ToDictionary(w => w.Id, w => w.Name);
        var whIds = warehouses.Select(w => w.Id).ToHashSet();

        if (input.WarehouseId.HasValue && input.WarehouseId != Guid.Empty)
        {
            whIds.RemoveWhere(id => id != input.WarehouseId.Value);
        }

        // 3. Fetch bins
        var binQuery = await _binRepository.GetQueryableAsync();
        binQuery = binQuery.Where(b => itemIds.Contains(b.ItemId) && whIds.Contains(b.WarehouseId));
        var bins = binQuery.ToList();

        // 4. Fetch price lists & item prices
        var priceListQuery = await _priceListRepository.GetQueryableAsync();
        var priceLists = priceListQuery.Where(p => p.IsActive).ToDictionary(p => p.Id);

        var itemPriceQuery = await _itemPriceRepository.GetQueryableAsync();
        var prices = itemPriceQuery
            .Where(p => itemIds.Contains(p.ItemId))
            .Where(p => (!p.ValidFrom.HasValue || p.ValidFrom.Value <= now)
                     && (!p.ValidUpto.HasValue || p.ValidUpto.Value >= now))
            .ToList();

        // Resolve latest selling and buying price per item
        var itemSellingPrices = new Dictionary<Guid, (string PriceListName, decimal Rate)>();
        var itemBuyingPrices = new Dictionary<Guid, (string PriceListName, decimal Rate)>();

        foreach (var p in prices)
        {
            if (!priceLists.TryGetValue(p.PriceListId, out var pl)) continue;

            if (input.SellingPriceListId.HasValue)
            {
                if (pl.Id == input.SellingPriceListId.Value && pl.IsSelling)
                {
                    itemSellingPrices[p.ItemId] = (pl.Name, p.PriceListRate);
                }
            }
            else if (pl.IsSelling && !itemSellingPrices.ContainsKey(p.ItemId))
            {
                itemSellingPrices[p.ItemId] = (pl.Name, p.PriceListRate);
            }

            if (input.BuyingPriceListId.HasValue)
            {
                if (pl.Id == input.BuyingPriceListId.Value && pl.IsBuying)
                {
                    itemBuyingPrices[p.ItemId] = (pl.Name, p.PriceListRate);
                }
            }
            else if (pl.IsBuying && !itemBuyingPrices.ContainsKey(p.ItemId))
            {
                itemBuyingPrices[p.ItemId] = (pl.Name, p.PriceListRate);
            }
        }

        // 5. Build rows
        var rows = new List<ItemPriceStockRowDto>();

        var binsByItemWh = bins.ToDictionary(b => (b.ItemId, b.WarehouseId));

        foreach (var item in items)
        {
            var sellingPrice = itemSellingPrices.TryGetValue(item.Id, out var sp) ? sp : (PriceListName: "Standard Selling", Rate: item.StandardSellingPrice ?? 0m);
            var buyingPrice = itemBuyingPrices.TryGetValue(item.Id, out var bp) ? bp : (PriceListName: "Standard Buying", Rate: item.StandardBuyingPrice ?? 0m);

            if (input.WarehouseId.HasValue && input.WarehouseId != Guid.Empty)
            {
                var whId = input.WarehouseId.Value;
                var whName = whMap.GetValueOrDefault(whId, "—");
                var bin = binsByItemWh.GetValueOrDefault((item.Id, whId));
                var qty = bin?.ActualQty ?? 0m;
                var valRate = bin?.ValuationRate ?? item.StandardBuyingPrice ?? 0m;
                var stockVal = bin?.StockValue ?? (qty * valRate);

                if (input.InStockOnly && qty <= 0) continue;

                rows.Add(new ItemPriceStockRowDto
                {
                    ItemId = item.Id,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    Brand = item.Brand,
                    ItemGroup = item.ItemGroup,
                    StockUom = item.Uom,
                    WarehouseId = whId,
                    WarehouseName = whName,
                    StockAvailable = qty,
                    BuyingPriceListName = buyingPrice.PriceListName,
                    BuyingRate = buyingPrice.Rate,
                    SellingPriceListName = sellingPrice.PriceListName,
                    SellingRate = sellingPrice.Rate,
                    ValuationRate = valRate,
                    StockValue = stockVal
                });
            }
            else
            {
                var itemBins = bins.Where(b => b.ItemId == item.Id).ToList();
                if (itemBins.Count == 0)
                {
                    if (input.InStockOnly) continue;

                    rows.Add(new ItemPriceStockRowDto
                    {
                        ItemId = item.Id,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName,
                        Brand = item.Brand,
                        ItemGroup = item.ItemGroup,
                        StockUom = item.Uom,
                        WarehouseId = null,
                        WarehouseName = "—",
                        StockAvailable = 0m,
                        BuyingPriceListName = buyingPrice.PriceListName,
                        BuyingRate = buyingPrice.Rate,
                        SellingPriceListName = sellingPrice.PriceListName,
                        SellingRate = sellingPrice.Rate,
                        ValuationRate = item.StandardBuyingPrice ?? 0m,
                        StockValue = 0m
                    });
                }
                else
                {
                    foreach (var bin in itemBins)
                    {
                        if (input.InStockOnly && bin.ActualQty <= 0) continue;

                        var whName = whMap.GetValueOrDefault(bin.WarehouseId, "—");
                        var valRate = bin.ValuationRate != 0 ? bin.ValuationRate : (item.StandardBuyingPrice ?? 0m);

                        rows.Add(new ItemPriceStockRowDto
                        {
                            ItemId = item.Id,
                            ItemCode = item.ItemCode,
                            ItemName = item.ItemName,
                            Brand = item.Brand,
                            ItemGroup = item.ItemGroup,
                            StockUom = item.Uom,
                            WarehouseId = bin.WarehouseId,
                            WarehouseName = whName,
                            StockAvailable = bin.ActualQty,
                            BuyingPriceListName = buyingPrice.PriceListName,
                            BuyingRate = buyingPrice.Rate,
                            SellingPriceListName = sellingPrice.PriceListName,
                            SellingRate = sellingPrice.Rate,
                            ValuationRate = valRate,
                            StockValue = bin.StockValue
                        });
                    }
                }
            }
        }

        // 6. Search filtering
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim().ToLowerInvariant();
            rows = rows.Where(r =>
                r.ItemCode.ToLowerInvariant().Contains(term) ||
                r.ItemName.ToLowerInvariant().Contains(term) ||
                (!string.IsNullOrEmpty(r.Brand) && r.Brand.ToLowerInvariant().Contains(term)) ||
                (!string.IsNullOrEmpty(r.WarehouseName) && r.WarehouseName.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        rows = rows.OrderBy(r => r.ItemCode).ThenBy(r => r.WarehouseName).ToList();

        return new ItemPriceStockReportDto
        {
            Rows = rows,
            TotalItems = rows.Select(r => r.ItemId).Distinct().Count(),
            TotalStockAvailable = rows.Sum(r => r.StockAvailable),
            TotalStockValue = rows.Sum(r => r.StockValue),
            TotalSellingValue = rows.Sum(r => r.StockAvailable * r.SellingRate)
        };
    }
}
