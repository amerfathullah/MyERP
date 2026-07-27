using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Resolves item details for transaction forms — auto-populates fields when user selects an item.
/// Delegates to ItemDetailsResolverService for proper 3-tier resolution:
/// ItemDefault (per company) → Item → ItemGroup hierarchy → Company defaults.
/// Per ERPNext get_item_details.py (1850 lines, 56 functions).
/// </summary>
[Authorize]
public class ItemDetailsAppService : ApplicationService
{
    private readonly ItemDetailsResolverService _resolver;
    private readonly IRepository<Bin, Guid> _binRepo;

    public ItemDetailsAppService(
        ItemDetailsResolverService resolver,
        IRepository<Bin, Guid> binRepo)
    {
        _resolver = resolver;
        _binRepo = binRepo;
    }

    /// <summary>
    /// Resolves item defaults for a transaction row.
    /// Per ERPNext: get_basic_details → 45 fields resolved from Item → ItemDefault → ItemGroup → Brand → Company defaults.
    /// </summary>
    public async Task<ItemDetailsDto> GetItemDetailsAsync(GetItemDetailsInput input)
    {
        var txType = input.TransactionType == "Buying"
            ? TransactionType.Buying
            : TransactionType.Selling;

        var context = new ItemResolutionContext
        {
            ItemId = input.ItemId,
            CompanyId = input.CompanyId,
            TransactionType = txType,
            WarehouseOverride = input.WarehouseId,
        };

        var resolved = await _resolver.ResolveAsync(context);

        var result = new ItemDetailsDto
        {
            ItemId = resolved.ItemId,
            ItemCode = resolved.ItemCode,
            ItemName = resolved.ItemName,
            Description = resolved.Description,
            Uom = resolved.Uom,
            StockUom = resolved.StockUom,
            ConversionFactor = resolved.ConversionFactor,
            IsStockItem = resolved.IsStockItem,
            HasBatchNo = resolved.HasBatchNo,
            HasSerialNo = resolved.HasSerialNo,
            ItemGroup = resolved.ItemGroup,
            Rate = resolved.Rate,
            WarehouseId = resolved.WarehouseId,
            IncomeAccountId = resolved.IncomeAccountId,
            ExpenseAccountId = resolved.ExpenseAccountId,
            ActualQty = resolved.ActualQty,
            ProjectedQty = resolved.ProjectedQty,
            ReservedQty = resolved.ReservedQty,
            AvailableQty = resolved.AvailableQty,
            CompanyTotalStock = resolved.CompanyTotalStock,
            LastPurchaseRate = resolved.LastPurchaseRate,
            MinOrderQty = resolved.MinOrderQty,
            DefaultSupplierId = resolved.DefaultSupplierId,
            DefaultDiscountPercentage = resolved.DefaultDiscountPercentage,
        };

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

    /// <summary>Resolved default supplier for buying transactions (from ItemDefault per company).</summary>
    public Guid? DefaultSupplierId { get; set; }
    /// <summary>Resolved default discount percentage (from ItemDefault per company).</summary>
    public decimal DefaultDiscountPercentage { get; set; }
}
