using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.Items.Default)]
public class ItemPriceAppService : ApplicationService
{
    private readonly IRepository<ItemPrice, Guid> _itemPriceRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<PriceList, Guid> _priceListRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;

    public ItemPriceAppService(
        IRepository<ItemPrice, Guid> itemPriceRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<PriceList, Guid> priceListRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Supplier, Guid> supplierRepo)
    {
        _itemPriceRepo = itemPriceRepo;
        _itemRepo = itemRepo;
        _priceListRepo = priceListRepo;
        _customerRepo = customerRepo;
        _supplierRepo = supplierRepo;
    }

    public async Task<PagedResultDto<ItemPriceDto>> GetListAsync(GetItemPriceListDto input)
    {
        var queryable = await _itemPriceRepo.GetQueryableAsync();

        if (input.ItemId.HasValue)
            queryable = queryable.Where(p => p.ItemId == input.ItemId.Value);

        if (input.PriceListId.HasValue)
            queryable = queryable.Where(p => p.PriceListId == input.PriceListId.Value);

        if (input.CustomerId.HasValue)
            queryable = queryable.Where(p => p.CustomerId == input.CustomerId.Value);

        if (input.SupplierId.HasValue)
            queryable = queryable.Where(p => p.SupplierId == input.SupplierId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim();
            var items = await _itemRepo.GetQueryableAsync();
            var matchingItemIds = items
                .Where(i => i.ItemCode.Contains(filter) || i.ItemName.Contains(filter))
                .Select(i => i.Id)
                .ToList();
            if (matchingItemIds.Any())
                queryable = queryable.Where(p => matchingItemIds.Contains(p.ItemId));
            else
                queryable = queryable.Where(p => false);
        }

        var totalCount = queryable.Count();
        var prices = queryable
            .OrderByDescending(p => p.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        // Batch-resolve names
        var itemIds = prices.Select(p => p.ItemId).Distinct().ToList();
        var plIds = prices.Select(p => p.PriceListId).Distinct().ToList();
        var customerIds = prices.Where(p => p.CustomerId.HasValue).Select(p => p.CustomerId!.Value).Distinct().ToList();
        var supplierIds = prices.Where(p => p.SupplierId.HasValue).Select(p => p.SupplierId!.Value).Distinct().ToList();

        var itemQ = await _itemRepo.GetQueryableAsync();
        var itemMap = itemQ.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName }).ToList()
            .ToDictionary(i => i.Id);

        var plQ = await _priceListRepo.GetQueryableAsync();
        var plMap = plQ.Where(pl => plIds.Contains(pl.Id))
            .Select(pl => new { pl.Id, pl.Name }).ToList()
            .ToDictionary(pl => pl.Id);

        Dictionary<Guid, string> customerMap = new();
        if (customerIds.Any())
        {
            var cQ = await _customerRepo.GetQueryableAsync();
            customerMap = cQ.Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name }).ToList()
                .ToDictionary(c => c.Id, c => c.Name);
        }

        Dictionary<Guid, string> supplierMap = new();
        if (supplierIds.Any())
        {
            var sQ = await _supplierRepo.GetQueryableAsync();
            supplierMap = sQ.Where(s => supplierIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Name }).ToList()
                .ToDictionary(s => s.Id, s => s.Name);
        }

        var dtos = prices.Select(p =>
        {
            var dto = new ItemPriceDto
            {
                Id = p.Id,
                ItemId = p.ItemId,
                PriceListId = p.PriceListId,
                PriceListRate = p.PriceListRate,
                Uom = p.Uom,
                CurrencyCode = p.CurrencyCode,
                MinQty = p.MinQty,
                ValidFrom = p.ValidFrom,
                ValidUpto = p.ValidUpto,
                CustomerId = p.CustomerId,
                SupplierId = p.SupplierId,
                BatchNo = p.BatchNo,
                IsAutoInserted = p.IsAutoInserted,
            };
            if (itemMap.TryGetValue(p.ItemId, out var item))
            {
                dto.ItemCode = item.ItemCode;
                dto.ItemName = item.ItemName;
            }
            if (plMap.TryGetValue(p.PriceListId, out var pl))
                dto.PriceListName = pl.Name;
            if (p.CustomerId.HasValue && customerMap.TryGetValue(p.CustomerId.Value, out var cn))
                dto.CustomerName = cn;
            if (p.SupplierId.HasValue && supplierMap.TryGetValue(p.SupplierId.Value, out var sn))
                dto.SupplierName = sn;
            return dto;
        }).ToList();

        return new PagedResultDto<ItemPriceDto>(totalCount, dtos);
    }

    public async Task<ItemPriceDto> GetAsync(Guid id)
    {
        var p = await _itemPriceRepo.GetAsync(id);
        var dto = new ItemPriceDto
        {
            Id = p.Id,
            ItemId = p.ItemId,
            PriceListId = p.PriceListId,
            PriceListRate = p.PriceListRate,
            Uom = p.Uom,
            CurrencyCode = p.CurrencyCode,
            MinQty = p.MinQty,
            ValidFrom = p.ValidFrom,
            ValidUpto = p.ValidUpto,
            CustomerId = p.CustomerId,
            SupplierId = p.SupplierId,
            BatchNo = p.BatchNo,
            IsAutoInserted = p.IsAutoInserted,
        };
        var item = await _itemRepo.FindAsync(p.ItemId);
        if (item != null) { dto.ItemCode = item.ItemCode; dto.ItemName = item.ItemName; }
        var pl = await _priceListRepo.FindAsync(p.PriceListId);
        if (pl != null) dto.PriceListName = pl.Name;
        return dto;
    }

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<ItemPriceDto> CreateAsync(CreateUpdateItemPriceDto input)
    {
        var price = new ItemPrice(
            GuidGenerator.Create(),
            input.ItemId,
            input.PriceListId,
            input.PriceListRate,
            input.Uom,
            input.CurrencyCode,
            CurrentTenant.Id)
        {
            MinQty = input.MinQty,
            ValidFrom = input.ValidFrom,
            ValidUpto = input.ValidUpto,
            CustomerId = input.CustomerId,
            SupplierId = input.SupplierId,
            BatchNo = input.BatchNo,
        };

        await _itemPriceRepo.InsertAsync(price);
        return await GetAsync(price.Id);
    }

    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<ItemPriceDto> UpdateAsync(Guid id, CreateUpdateItemPriceDto input)
    {
        var price = await _itemPriceRepo.GetAsync(id);
        price.PriceListRate = input.PriceListRate;
        price.MinQty = input.MinQty;
        price.ValidFrom = input.ValidFrom;
        price.ValidUpto = input.ValidUpto;
        price.CustomerId = input.CustomerId;
        price.SupplierId = input.SupplierId;
        price.BatchNo = input.BatchNo;
        await _itemPriceRepo.UpdateAsync(price);
        return await GetAsync(price.Id);
    }

    [Authorize(MyERPPermissions.Items.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _itemPriceRepo.DeleteAsync(id);
    }

    /// <summary>
    /// Bulk percentage update on all prices in a price list.
    /// Per ERPNext: enables "increase all prices by X%" workflows.
    /// </summary>
    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<BulkPriceUpdateResultDto> BulkUpdateAsync(BulkPriceUpdateDto input)
    {
        var queryable = await _itemPriceRepo.GetQueryableAsync();
        var prices = queryable.Where(p => p.PriceListId == input.PriceListId).ToList();

        if (input.ItemGroupId.HasValue)
        {
            var itemQ = await _itemRepo.GetQueryableAsync();
            var itemIdsInGroup = itemQ.Where(i => i.ItemGroupId == input.ItemGroupId.Value)
                .Select(i => i.Id).ToList();
            prices = prices.Where(p => itemIdsInGroup.Contains(p.ItemId)).ToList();
        }

        var multiplier = 1 + (input.PercentageChange / 100m);
        foreach (var p in prices)
        {
            p.PriceListRate = Math.Round(p.PriceListRate * multiplier, 4);
        }

        await _itemPriceRepo.UpdateManyAsync(prices);

        return new BulkPriceUpdateResultDto
        {
            UpdatedCount = prices.Count,
            PercentageApplied = input.PercentageChange,
        };
    }
}
