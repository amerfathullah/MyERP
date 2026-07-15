using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

public class ItemAppService :
    CrudAppService<
        Item,
        ItemDto,
        Guid,
        GetItemListDto,
        CreateUpdateItemDto>,
    IItemAppService
{
    public ItemAppService(IRepository<Item, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Items.Default;
        GetListPolicyName = MyERPPermissions.Items.Default;
        CreatePolicyName = MyERPPermissions.Items.Create;
        UpdatePolicyName = MyERPPermissions.Items.Edit;
        DeletePolicyName = MyERPPermissions.Items.Delete;
    }

    /// <summary>
    /// Override UpdateAsync to prevent deactivation of items in active orders.
    /// Per DO-NOT: "Show disabled/variant-template/expired items in link field queries"
    /// </summary>
    public override async Task<ItemDto> UpdateAsync(Guid id, CreateUpdateItemDto input)
    {
        var existing = await Repository.GetAsync(id);

        // If deactivating (was active → now inactive), validate no active orders use this item
        if (existing.IsActive && !input.IsActive)
        {
            await ValidateCanDeactivateAsync(id);
        }

        return await base.UpdateAsync(id, input);
    }

    private async Task ValidateCanDeactivateAsync(Guid itemId)
    {
        // Check SO items in active status (not Draft/Cancelled/Completed)
        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
        var soQuery = await soRepo.GetQueryableAsync();
        var hasActiveSO = soQuery.Any(so =>
            so.Items.Any(i => i.ItemId == itemId)
            && so.Status != DocumentStatus.Draft
            && so.Status != DocumentStatus.Cancelled
            && so.Status != DocumentStatus.Completed);

        if (hasActiveSO)
        {
            throw new BusinessException("MyERP:05017")
                .WithData("itemId", itemId)
                .WithData("reason", "Item is used in active Sales Orders.");
        }

        // Check PO items in active status
        var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseOrder, Guid>>();
        var poQuery = await poRepo.GetQueryableAsync();
        var hasActivePO = poQuery.Any(po =>
            po.Items.Any(i => i.ItemId == itemId)
            && po.Status != DocumentStatus.Draft
            && po.Status != DocumentStatus.Cancelled
            && po.Status != DocumentStatus.Completed);

        if (hasActivePO)
        {
            throw new BusinessException("MyERP:05017")
                .WithData("itemId", itemId)
                .WithData("reason", "Item is used in active Purchase Orders.");
        }
    }

    /// <summary>
    /// Override DeleteAsync to prevent deletion of items with stock ledger entries.
    /// Items with any stock history cannot be deleted (only deactivated).
    /// </summary>
    public override async Task DeleteAsync(Guid id)
    {
        // Check for stock ledger entries
        var sleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var sleQuery = await sleRepo.GetQueryableAsync();
        var hasStockHistory = sleQuery.Any(s => s.ItemId == id);

        if (hasStockHistory)
        {
            throw new BusinessException("MyERP:05018")
                .WithData("itemId", id)
                .WithData("reason", "Item has stock ledger history. Deactivate instead of deleting.");
        }

        // Check for active orders (same as deactivation)
        await ValidateCanDeactivateAsync(id);

        await base.DeleteAsync(id);
    }

    public override async Task<PagedResultDto<ItemDto>> GetListAsync(GetItemListDto input)
    {
        var filter = input.Filter;
        var companyId = input.CompanyId;

        var queryable = await Repository.GetQueryableAsync();

        if (companyId.HasValue)
        {
            queryable = queryable.Where(i => i.CompanyId == companyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterLower = filter.ToLower();
            queryable = queryable.Where(i =>
                i.ItemCode.ToLower().Contains(filterLower)
                || i.ItemName.ToLower().Contains(filterLower));
        }

        var totalCount = queryable.Count();
        var items = queryable
            .OrderBy(i => i.ItemName)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<ItemDto>(
            totalCount,
            items.Select(MapToDto).ToList());
    }

    private static ItemDto MapToDto(Item i) => new()
    {
        Id = i.Id,
        CompanyId = i.CompanyId,
        ItemCode = i.ItemCode,
        ItemName = i.ItemName,
        Barcode = i.Barcode,
        Description = i.Description,
        ItemType = i.ItemType,
        ItemGroup = i.ItemGroup,
        Brand = i.Brand,
        Uom = i.Uom,
        ValuationMethod = i.ValuationMethod,
        StandardSellingPrice = i.StandardSellingPrice,
        StandardBuyingPrice = i.StandardBuyingPrice,
        TaxCategoryId = i.TaxCategoryId,
        MaintainStock = i.MaintainStock,
        DefaultIncomeAccountId = i.DefaultIncomeAccountId,
        DefaultExpenseAccountId = i.DefaultExpenseAccountId,
        IsActive = i.IsActive,
        ReorderLevel = i.ReorderLevel,
        ReorderQty = i.ReorderQty,
        SafetyStock = i.SafetyStock,
        DefaultWarehouseId = i.DefaultWarehouseId,
        MinOrderQty = i.MinOrderQty,
        InspectionRequiredBeforePurchase = i.InspectionRequiredBeforePurchase,
        InspectionRequiredBeforeDelivery = i.InspectionRequiredBeforeDelivery,
        CreationTime = i.CreationTime,
        LastModificationTime = i.LastModificationTime,
    };
}
