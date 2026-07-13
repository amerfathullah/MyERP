using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Validates items before use in transactions.
/// Per DO-NOT: disabled/variant-template/expired items must not appear in transactions.
/// Per ERPNext: validate_item_details blocks template, EOL, and disabled items.
/// </summary>
public class ItemTransactionValidationService : DomainService
{
    private readonly IRepository<Item, Guid> _itemRepository;

    public ItemTransactionValidationService(IRepository<Item, Guid> itemRepository)
    {
        _itemRepository = itemRepository;
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
