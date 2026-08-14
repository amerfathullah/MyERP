using System;

namespace MyERP.Inventory;

public class GetItemDetailsInput
{
    public Guid ItemId { get; set; }
    /// <summary>"Selling" or "Buying" — determines which price/account to resolve.</summary>
    public string TransactionType { get; set; } = "Selling";
    /// <summary>Optional: specific warehouse to check stock for.</summary>
    public Guid? WarehouseId { get; set; }
    /// <summary>Optional: company for default resolution.</summary>
    public Guid? CompanyId { get; set; }
    /// <summary>Optional: supplier ID for supplier-specific pricing on purchase documents.</summary>
    public Guid? SupplierId { get; set; }
    /// <summary>Optional: customer ID for customer-specific pricing on sales documents.</summary>
    public Guid? CustomerId { get; set; }
    /// <summary>Optional: price list ID for rate lookup.</summary>
    public Guid? PriceListId { get; set; }
    /// <summary>Optional: transaction date for date-valid price lookup.</summary>
    public DateTime? TransactionDate { get; set; }
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

    /// <summary>Current weighted-average valuation rate at the resolved warehouse (for margin display).</summary>
    public decimal ValuationRate { get; set; }

    /// <summary>Active Blanket Order ID if item has a contracted rate.</summary>
    public Guid? BlanketOrderId { get; set; }
    /// <summary>Blanket Order number for display.</summary>
    public string? BlanketOrderNumber { get; set; }
    /// <summary>Contracted rate from the Blanket Order (takes precedence over standard rate).</summary>
    public decimal? BlanketOrderRate { get; set; }
    /// <summary>Remaining qty available on the Blanket Order line.</summary>
    public decimal? BlanketOrderRemainingQty { get; set; }
}
