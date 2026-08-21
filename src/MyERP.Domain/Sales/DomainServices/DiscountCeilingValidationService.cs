using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Domain service for validating discount ceiling limits against Item master definitions (Gotcha #3222).
/// Enforces:
/// 1. discountPercentage must be within [0, 100].
/// 2. If Item.MaxDiscount is set and discountPercentage > Item.MaxDiscount, throws MaxDiscountExceeded.
/// </summary>
public class DiscountCeilingValidationService : DomainService
{
    private readonly IRepository<Item, Guid> _itemRepository;

    public DiscountCeilingValidationService(IRepository<Item, Guid> itemRepository)
    {
        _itemRepository = itemRepository;
    }

    /// <summary>
    /// Validates discount percentage for a single item.
    /// </summary>
    public async Task ValidateItemDiscountAsync(Guid itemId, decimal discountPercentage)
    {
        if (discountPercentage < 0 || discountPercentage > 100)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDiscountPercentage);
        }

        var item = await _itemRepository.FindAsync(itemId);
        if (item == null) return;

        ValidateItemDiscount(item, discountPercentage);
    }

    /// <summary>
    /// Validates discount percentage directly against an item entity.
    /// </summary>
    public void ValidateItemDiscount(Item item, decimal discountPercentage)
    {
        if (discountPercentage < 0 || discountPercentage > 100)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDiscountPercentage);
        }

        if (item.MaxDiscount.HasValue && discountPercentage > item.MaxDiscount.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.MaxDiscountExceeded)
                .WithData("discountPercentage", discountPercentage)
                .WithData("maxDiscount", item.MaxDiscount.Value)
                .WithData("itemCode", item.ItemCode);
        }
    }

    /// <summary>
    /// Batch validates discount percentages for multiple items.
    /// </summary>
    public async Task ValidateDiscountsAsync(IEnumerable<(Guid ItemId, decimal DiscountPercentage)> itemDiscounts)
    {
        var discountList = itemDiscounts.ToList();
        if (!discountList.Any()) return;

        foreach (var (_, pct) in discountList)
        {
            if (pct < 0 || pct > 100)
            {
                throw new BusinessException(MyERPDomainErrorCodes.InvalidDiscountPercentage);
            }
        }

        var itemIds = discountList.Select(d => d.ItemId).Distinct().ToList();
        var items = await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id));
        var itemMap = items.ToDictionary(i => i.Id);

        foreach (var (itemId, discountPercentage) in discountList)
        {
            if (itemMap.TryGetValue(itemId, out var item))
            {
                ValidateItemDiscount(item, discountPercentage);
            }
        }
    }
}
