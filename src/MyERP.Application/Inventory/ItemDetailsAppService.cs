using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Resolves item details for transaction forms — auto-populates fields when user selects an item.
/// Per ERPNext get_item_details.py (1850 lines, 56 functions) — simplified to key fields.
/// Called on every item selection change in SO/PO/SI/PI/DN/PR forms.
/// </summary>
[Authorize]
public class ItemDetailsAppService : ApplicationService
{
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Bin, Guid> _binRepo;
    private readonly IRepository<Entities.ItemGroup, Guid> _itemGroupRepo;

    public ItemDetailsAppService(
        IRepository<Item, Guid> itemRepo,
        IRepository<Bin, Guid> binRepo,
        IRepository<Entities.ItemGroup, Guid> itemGroupRepo)
    {
        _itemRepo = itemRepo;
        _binRepo = binRepo;
        _itemGroupRepo = itemGroupRepo;
    }

    /// <summary>
    /// Resolves item defaults for a transaction row.
    /// Per ERPNext: get_basic_details → 45 fields resolved from Item → ItemGroup → Brand → Company defaults.
    /// </summary>
    public async Task<ItemDetailsDto> GetItemDetailsAsync(GetItemDetailsInput input)
    {
        var item = await _itemRepo.GetAsync(input.ItemId);

        var result = new ItemDetailsDto
        {
            ItemId = item.Id,
            ItemCode = item.ItemCode,
            ItemName = item.ItemName,
            Description = item.Description ?? item.ItemName,
            Uom = item.Uom,
            StockUom = item.Uom,
            ConversionFactor = 1m,
            IsStockItem = item.MaintainStock,
            HasBatchNo = false, // TODO: link to batch settings
            HasSerialNo = false,
            ItemGroup = item.ItemGroup,
        };

        // Price resolution: selling vs buying
        if (input.TransactionType == "Selling")
        {
            result.Rate = item.StandardSellingPrice ?? 0;
            result.IncomeAccountId = item.DefaultIncomeAccountId;
        }
        else
        {
            result.Rate = item.StandardBuyingPrice ?? 0;
            result.ExpenseAccountId = item.DefaultExpenseAccountId;
        }

        // Warehouse resolution: item default → item group default → null
        result.WarehouseId = item.DefaultWarehouseId;
        if (!result.WarehouseId.HasValue && item.ItemGroupId.HasValue)
        {
            var group = await _itemGroupRepo.FindAsync(item.ItemGroupId.Value);
            result.WarehouseId = group?.DefaultWarehouseId;
        }

        // Stock availability: if warehouse specified, get current stock
        var targetWarehouse = input.WarehouseId ?? result.WarehouseId;
        if (targetWarehouse.HasValue && item.MaintainStock)
        {
            var binQuery = await _binRepo.GetQueryableAsync();
            var bin = binQuery.FirstOrDefault(b => b.ItemId == input.ItemId && b.WarehouseId == targetWarehouse.Value);
            if (bin != null)
            {
                result.ActualQty = bin.ActualQty;
                result.ProjectedQty = bin.ProjectedQty;
                result.ReservedQty = bin.ReservedQty;
                result.AvailableQty = bin.ActualQty - bin.ReservedQty;
            }
        }

        // Company-total stock (across all warehouses)
        if (item.MaintainStock)
        {
            var binQuery = await _binRepo.GetQueryableAsync();
            result.CompanyTotalStock = binQuery
                .Where(b => b.ItemId == input.ItemId)
                .Sum(b => (decimal?)b.ActualQty) ?? 0;
        }

        // Last purchase rate (for buying)
        if (input.TransactionType == "Buying" && item.StandardBuyingPrice.HasValue)
        {
            result.LastPurchaseRate = item.StandardBuyingPrice.Value;
        }

        // Min order qty (for buying)
        result.MinOrderQty = item.MinOrderQty;

        return result;
    }
}

// --- DTOs ---

public class GetItemDetailsInput
{
    public Guid ItemId { get; set; }
    /// <summary>"Selling" or "Buying" — determines which price/account to resolve.</summary>
    public string TransactionType { get; set; } = "Selling";
    /// <summary>Optional: specific warehouse to check stock for.</summary>
    public Guid? WarehouseId { get; set; }
    /// <summary>Optional: company for default resolution.</summary>
    public Guid? CompanyId { get; set; }
}

public class ItemDetailsDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public string Uom { get; set; } = "Unit";
    public string StockUom { get; set; } = "Unit";
    public decimal ConversionFactor { get; set; } = 1;
    public bool IsStockItem { get; set; }
    public bool HasBatchNo { get; set; }
    public bool HasSerialNo { get; set; }
    public string? ItemGroup { get; set; }
    public decimal Rate { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? IncomeAccountId { get; set; }
    public Guid? ExpenseAccountId { get; set; }

    // Stock availability at the specified warehouse
    public decimal ActualQty { get; set; }
    public decimal ProjectedQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal AvailableQty { get; set; }
    /// <summary>Total stock across all company warehouses.</summary>
    public decimal CompanyTotalStock { get; set; }

    public decimal LastPurchaseRate { get; set; }
    public decimal MinOrderQty { get; set; }
}
