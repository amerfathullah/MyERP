using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Item Variant Details report application service.
/// Displays variants of a selected template item with dynamic attributes, stock levels,
/// in-production quantities, open sales orders, and average price list rates.
/// Maps to ERPNext stock/report/item_variant_details/item_variant_details.py.
/// </summary>
[Authorize(MyERPPermissions.Items.Default)]
public class ItemVariantDetailsAppService : ApplicationService, IItemVariantDetailsAppService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<ItemAttribute, Guid> _attributeRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<ItemPrice, Guid> _itemPriceRepository;
    private readonly IRepository<PriceList, Guid> _priceListRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;

    public ItemVariantDetailsAppService(
        IRepository<Item, Guid> itemRepository,
        IRepository<ItemAttribute, Guid> attributeRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<ItemPrice, Guid> itemPriceRepository,
        IRepository<PriceList, Guid> priceListRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository)
    {
        _itemRepository = itemRepository;
        _attributeRepository = attributeRepository;
        _binRepository = binRepository;
        _itemPriceRepository = itemPriceRepository;
        _priceListRepository = priceListRepository;
        _salesOrderRepository = salesOrderRepository;
    }

    public async Task<ItemVariantDetailsReportDto> GetReportAsync(ItemVariantDetailsFilterDto input)
    {
        if (!input.TemplateItemId.HasValue || input.TemplateItemId.Value == Guid.Empty)
        {
            return new ItemVariantDetailsReportDto();
        }

        var template = await _itemRepository.FindAsync(input.TemplateItemId.Value);
        if (template == null)
        {
            return new ItemVariantDetailsReportDto();
        }

        // Fetch all active variants of this template
        var itemQuery = await _itemRepository.WithDetailsAsync(i => i.VariantAttributes);
        var variants = itemQuery
            .Where(i => i.VariantOfId == template.Id && i.IsActive)
            .OrderBy(i => i.ItemCode)
            .ToList();

        if (variants.Count == 0)
        {
            return new ItemVariantDetailsReportDto
            {
                TemplateItemId = template.Id,
                TemplateItemCode = template.ItemCode,
                TemplateItemName = template.ItemName,
                AttributeNames = new List<string>(),
                Rows = new List<ItemVariantDetailsRowDto>()
            };
        }

        var variantIds = variants.Select(v => v.Id).ToHashSet();

        // Resolve attribute names
        var attrIds = variants
            .SelectMany(v => v.VariantAttributes)
            .Select(va => va.ItemAttributeId)
            .Distinct()
            .ToList();

        var attrQuery = await _attributeRepository.GetQueryableAsync();
        var attributes = attrQuery
            .Where(a => attrIds.Contains(a.Id))
            .ToList();
        var attrNameMap = attributes.ToDictionary(a => a.Id, a => a.AttributeName);
        var orderedAttrNames = attributes.Select(a => a.AttributeName).OrderBy(n => n).ToList();

        // Stock details from Bin
        var binQuery = await _binRepository.GetQueryableAsync();
        binQuery = binQuery.Where(b => variantIds.Contains(b.ItemId));
        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
        {
            binQuery = binQuery.Where(b => b.WarehouseId == input.WarehouseId.Value);
        }
        var bins = binQuery.ToList();

        var stockMap = bins
            .GroupBy(b => b.ItemId)
            .ToDictionary(
                g => g.Key,
                g => (
                    ActualQty: g.Sum(b => b.ActualQty),
                    PlannedQty: g.Sum(b => b.PlannedQty)
                )
            );

        // Price lists and rates
        var now = DateTime.UtcNow.Date;
        var priceListQuery = await _priceListRepository.GetQueryableAsync();
        var activePriceLists = priceListQuery.Where(p => p.IsActive).ToList();
        var sellingPlIds = activePriceLists.Where(p => p.IsSelling).Select(p => p.Id).ToHashSet();
        var buyingPlIds = activePriceLists.Where(p => p.IsBuying).Select(p => p.Id).ToHashSet();

        var priceQuery = await _itemPriceRepository.GetQueryableAsync();
        var prices = priceQuery
            .Where(p => variantIds.Contains(p.ItemId)
                     && p.CustomerId == null
                     && p.SupplierId == null
                     && (!p.ValidFrom.HasValue || p.ValidFrom.Value <= now)
                     && (!p.ValidUpto.HasValue || p.ValidUpto.Value >= now))
            .ToList();

        var sellingPriceMap = prices
            .Where(p => sellingPlIds.Contains(p.PriceListId))
            .GroupBy(p => p.ItemId)
            .ToDictionary(g => g.Key, g => g.Average(p => p.PriceListRate));

        var buyingPriceMap = prices
            .Where(p => buyingPlIds.Contains(p.PriceListId))
            .GroupBy(p => p.ItemId)
            .ToDictionary(g => g.Key, g => g.Average(p => p.PriceListRate));

        // Open sales orders (submitted orders not closed or completed)
        var soQuery = await _salesOrderRepository.WithDetailsAsync(s => s.Items);
        var openSos = soQuery
            .Where(s => s.Status != DocumentStatus.Draft
                     && s.Status != DocumentStatus.Cancelled
                     && s.Status != DocumentStatus.Closed
                     && s.Status != DocumentStatus.Completed)
            .ToList();

        var openOrderCountMap = new Dictionary<Guid, int>();
        foreach (var vid in variantIds)
        {
            var count = openSos.Count(so => so.Items.Any(item =>
                item.ItemId == vid && !item.IsClosed && item.DeliveredQty < item.Quantity));
            openOrderCountMap[vid] = count;
        }

        var rows = new List<ItemVariantDetailsRowDto>();
        foreach (var v in variants)
        {
            var row = new ItemVariantDetailsRowDto
            {
                VariantId = v.Id,
                VariantCode = v.ItemCode,
                VariantName = v.ItemName,
                Currency = "MYR"
            };

            foreach (var va in v.VariantAttributes)
            {
                if (attrNameMap.TryGetValue(va.ItemAttributeId, out var name))
                {
                    row.Attributes[name] = va.AttributeValue;
                }
            }

            if (stockMap.TryGetValue(v.Id, out var stock))
            {
                row.CurrentStock = stock.ActualQty;
                row.InProduction = stock.PlannedQty;
            }

            if (sellingPriceMap.TryGetValue(v.Id, out var avgSelling))
            {
                row.AvgSellingPrice = Math.Round(avgSelling, 2);
            }
            else if (v.StandardSellingPrice.HasValue)
            {
                row.AvgSellingPrice = v.StandardSellingPrice.Value;
            }

            if (buyingPriceMap.TryGetValue(v.Id, out var avgBuying))
            {
                row.AvgBuyingPrice = Math.Round(avgBuying, 2);
            }
            else if (v.StandardBuyingPrice.HasValue)
            {
                row.AvgBuyingPrice = v.StandardBuyingPrice.Value;
            }

            if (openOrderCountMap.TryGetValue(v.Id, out var openCount))
            {
                row.OpenOrders = openCount;
            }

            rows.Add(row);
        }

        return new ItemVariantDetailsReportDto
        {
            TemplateItemId = template.Id,
            TemplateItemCode = template.ItemCode,
            TemplateItemName = template.ItemName,
            AttributeNames = orderedAttrNames,
            Rows = rows
        };
    }
}
