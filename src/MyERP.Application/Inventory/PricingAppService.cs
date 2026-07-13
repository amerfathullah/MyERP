using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Manages Price Lists and Item Prices.
/// Provides item rate resolution for transaction line items.
/// </summary>
[Authorize(MyERPPermissions.Items.Default)]
public class PricingAppService : ApplicationService
{
    private readonly IRepository<PriceList, Guid> _priceListRepository;
    private readonly IRepository<ItemPrice, Guid> _itemPriceRepository;

    public PricingAppService(
        IRepository<PriceList, Guid> priceListRepository,
        IRepository<ItemPrice, Guid> itemPriceRepository)
    {
        _priceListRepository = priceListRepository;
        _itemPriceRepository = itemPriceRepository;
    }

    // === Price List CRUD ===

    public async Task<List<PriceListDto>> GetPriceListsAsync()
    {
        var lists = await _priceListRepository.GetListAsync();
        return lists.Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(MapPriceListToDto)
            .ToList();
    }

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<PriceListDto> CreatePriceListAsync(CreateUpdatePriceListDto input)
    {
        var priceList = new PriceList(
            GuidGenerator.Create(), input.Name, input.CurrencyCode,
            input.IsSelling, input.IsBuying, CurrentTenant.Id)
        {
            IsDefault = input.IsDefault,
            CompanyId = input.CompanyId,
        };
        await _priceListRepository.InsertAsync(priceList);
        return MapPriceListToDto(priceList);
    }

    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<PriceListDto> UpdatePriceListAsync(Guid id, CreateUpdatePriceListDto input)
    {
        var priceList = await _priceListRepository.GetAsync(id);
        priceList.SetName(input.Name);
        priceList.CurrencyCode = input.CurrencyCode;
        priceList.IsSelling = input.IsSelling;
        priceList.IsBuying = input.IsBuying;
        priceList.IsDefault = input.IsDefault;
        priceList.CompanyId = input.CompanyId;
        await _priceListRepository.UpdateAsync(priceList);
        return MapPriceListToDto(priceList);
    }

    // === Item Price CRUD ===

    public async Task<PagedResultDto<ItemPriceDto>> GetItemPricesAsync(GetItemPriceListDto input)
    {
        var query = await _itemPriceRepository.GetQueryableAsync();

        if (input.ItemId.HasValue)
            query = query.Where(p => p.ItemId == input.ItemId.Value);
        if (input.PriceListId.HasValue)
            query = query.Where(p => p.PriceListId == input.PriceListId.Value);
        if (input.CustomerId.HasValue)
            query = query.Where(p => p.CustomerId == input.CustomerId.Value);
        if (input.SupplierId.HasValue)
            query = query.Where(p => p.SupplierId == input.SupplierId.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(p => p.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<ItemPriceDto>(
            totalCount, items.Select(MapItemPriceToDto).ToList());
    }

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<ItemPriceDto> CreateItemPriceAsync(CreateUpdateItemPriceDto input)
    {
        var price = new ItemPrice(
            GuidGenerator.Create(), input.ItemId, input.PriceListId,
            input.PriceListRate, input.Uom, input.CurrencyCode, CurrentTenant.Id)
        {
            MinQty = input.MinQty,
            ValidFrom = input.ValidFrom,
            ValidUpto = input.ValidUpto,
            CustomerId = input.CustomerId,
            SupplierId = input.SupplierId,
            BatchNo = input.BatchNo,
        };
        await _itemPriceRepository.InsertAsync(price);
        return MapItemPriceToDto(price);
    }

    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<ItemPriceDto> UpdateItemPriceAsync(Guid id, CreateUpdateItemPriceDto input)
    {
        var price = await _itemPriceRepository.GetAsync(id);
        price.PriceListRate = input.PriceListRate;
        price.Uom = input.Uom;
        price.MinQty = input.MinQty;
        price.ValidFrom = input.ValidFrom;
        price.ValidUpto = input.ValidUpto;
        price.CustomerId = input.CustomerId;
        price.SupplierId = input.SupplierId;
        price.BatchNo = input.BatchNo;
        await _itemPriceRepository.UpdateAsync(price);
        return MapItemPriceToDto(price);
    }

    [Authorize(MyERPPermissions.Items.Delete)]
    public async Task DeleteItemPriceAsync(Guid id)
    {
        await _itemPriceRepository.DeleteAsync(id);
    }

    // === Price Resolution ===

    /// <summary>
    /// Resolves the best item price for the given context.
    /// Priority: batch-specific > party-specific > min-qty > generic (per ERPNext lookup order).
    /// </summary>
    public async Task<ItemRateResultDto> GetItemRateAsync(GetItemRateRequestDto input)
    {
        var query = await _itemPriceRepository.GetQueryableAsync();
        var date = input.TransactionDate ?? DateTime.UtcNow;

        // Base filter: item + price list
        var candidates = query
            .Where(p => p.ItemId == input.ItemId && p.PriceListId == input.PriceListId)
            .ToList()
            .Where(p => p.IsValidOnDate(date))
            .Where(p => p.MinQty <= input.Qty)
            .ToList();

        // Priority 1: batch-specific
        if (!string.IsNullOrEmpty(input.BatchNo))
        {
            var batchPrice = candidates.FirstOrDefault(p => p.BatchNo == input.BatchNo);
            if (batchPrice != null)
                return new ItemRateResultDto { Rate = batchPrice.PriceListRate, ItemPriceId = batchPrice.Id, Source = "Batch" };
        }

        // Priority 2: party-specific (customer or supplier)
        if (input.CustomerId.HasValue)
        {
            var customerPrice = candidates
                .Where(p => p.CustomerId == input.CustomerId.Value)
                .OrderByDescending(p => p.MinQty)
                .FirstOrDefault();
            if (customerPrice != null)
                return new ItemRateResultDto { Rate = customerPrice.PriceListRate, ItemPriceId = customerPrice.Id, Source = "Customer" };
        }
        if (input.SupplierId.HasValue)
        {
            var supplierPrice = candidates
                .Where(p => p.SupplierId == input.SupplierId.Value)
                .OrderByDescending(p => p.MinQty)
                .FirstOrDefault();
            if (supplierPrice != null)
                return new ItemRateResultDto { Rate = supplierPrice.PriceListRate, ItemPriceId = supplierPrice.Id, Source = "Supplier" };
        }

        // Priority 3: generic price (no party, no batch) — highest min_qty that applies
        var genericPrice = candidates
            .Where(p => p.CustomerId == null && p.SupplierId == null && string.IsNullOrEmpty(p.BatchNo))
            .OrderByDescending(p => p.MinQty)
            .FirstOrDefault();

        if (genericPrice != null)
            return new ItemRateResultDto { Rate = genericPrice.PriceListRate, ItemPriceId = genericPrice.Id, Source = "PriceList" };

        return new ItemRateResultDto { Rate = 0, Source = "None" };
    }

    private static PriceListDto MapPriceListToDto(PriceList p) => new()
    {
        Id = p.Id, Name = p.Name, CurrencyCode = p.CurrencyCode,
        IsSelling = p.IsSelling, IsBuying = p.IsBuying,
        IsDefault = p.IsDefault, IsActive = p.IsActive,
        CompanyId = p.CompanyId, CreationTime = p.CreationTime,
    };

    private static ItemPriceDto MapItemPriceToDto(ItemPrice p) => new()
    {
        Id = p.Id, ItemId = p.ItemId, PriceListId = p.PriceListId,
        PriceListRate = p.PriceListRate, Uom = p.Uom, CurrencyCode = p.CurrencyCode,
        MinQty = p.MinQty, ValidFrom = p.ValidFrom, ValidUpto = p.ValidUpto,
        CustomerId = p.CustomerId, SupplierId = p.SupplierId, BatchNo = p.BatchNo,
        CreationTime = p.CreationTime,
    };
}
