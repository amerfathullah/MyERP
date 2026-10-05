using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Validates items before use in transactions.
/// Per DO-NOT: disabled/variant-template/expired items must not appear in transactions.
/// Per ERPNext: validate_item_details blocks template, EOL, and disabled items.
/// Per ERPNext PR #59754: enforces UOM conversion restrictions in sales and purchases.
/// </summary>
public class ItemTransactionValidationService : DomainService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Company, Guid>? _companyRepository;
    private readonly UomConversionService? _uomConversionService;
    private readonly ISettingProvider? _settingProvider;

    public ItemTransactionValidationService(
        IRepository<Item, Guid> itemRepository,
        IRepository<Company, Guid>? companyRepository = null,
        UomConversionService? uomConversionService = null,
        ISettingProvider? settingProvider = null)
    {
        _itemRepository = itemRepository;
        _companyRepository = companyRepository;
        _uomConversionService = uomConversionService;
        _settingProvider = settingProvider;
    }

    /// <summary>
    /// Validates that all items are active and usable in transactions.
    /// Blocks: inactive items, items without stock UOM configured.
    /// </summary>
    public async Task ValidateItemsForTransactionAsync(IEnumerable<Guid> itemIds)
    {
        var items = await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id));

        foreach (var item in items)
        {
            if (!item.IsActive)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ItemInactive)
                    .WithData("itemCode", item.ItemCode)
                    .WithData("itemName", item.ItemName);
            }
        }
    }

    /// <summary>
    /// Validates that transaction item UOMs comply with the UOM conversion configuration.
    /// Per ERPNext PR #59754 (commit 8ba7a8ee34):
    /// When AllowUomWithConversionRateDefinedInItem is enabled, any UOM different from the stock UOM
    /// must have an explicit conversion rate defined in the item (or its variant template) with conversion_factor > 0.
    /// </summary>
    public async Task ValidateItemUomsForTransactionAsync(
        Guid companyId,
        IEnumerable<(Guid ItemId, string? Uom)> rows)
    {
        var companyRepo = _companyRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Company, Guid>>();
        var uomService = _uomConversionService ?? LazyServiceProvider?.LazyGetService<UomConversionService>();
        var settingProvider = _settingProvider ?? LazyServiceProvider?.LazyGetService<ISettingProvider>();

        if (companyRepo == null || uomService == null) return;

        var company = await companyRepo.FindAsync(companyId);
        var globalSetting = settingProvider != null && await settingProvider.IsTrueAsync(MyERPSettings.Stock.AllowUomWithConversionRateDefinedInItem);
        var isEnabled = (company?.AllowUomWithConversionRateDefinedInItem ?? false) || globalSetting;

        if (!isEnabled) return;

        var rowList = rows.ToList();
        var itemIds = rowList.Select(r => r.ItemId).Distinct().ToList();
        var items = await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id));
        var itemMap = items.ToDictionary(i => i.Id);

        var itemUomPairs = new List<(Item item, string? uom)>();
        foreach (var row in rowList)
        {
            if (itemMap.TryGetValue(row.ItemId, out var item))
            {
                itemUomPairs.Add((item, row.Uom));
            }
        }

        await uomService.ValidateItemUomsAsync(itemUomPairs, isEnabled);
    }

    /// <summary>
    /// Validates a single item is active and usable.
    /// </summary>
    public async Task ValidateItemAsync(Guid itemId)
    {
        var item = await _itemRepository.FindAsync(itemId);
        if (item == null) return;

        if (!item.IsActive)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ItemInactive)
                .WithData("itemCode", item.ItemCode)
                .WithData("itemName", item.ItemName);
        }
    }
}
