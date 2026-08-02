using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Purchasing;

/// <summary>
/// Per-item supplier selection for MR→PO conversion.
/// Per ERPNext PR #57676: enables per-item supplier assignment + qty adjustment.
/// </summary>
public class CreatePurchaseOrdersFromMrDto
{
    [Required]
    public Guid MaterialRequestId { get; set; }

    [Required]
    public List<SupplierSelectionItemDto> Items { get; set; } = new();
}

/// <summary>
/// Per-item data for supplier selection dialog.
/// </summary>
public class SupplierSelectionItemDto
{
    [Required]
    public Guid MaterialRequestItemId { get; set; }

    [Required]
    public Guid SupplierId { get; set; }

    /// <summary>Quantity to order. Must be &gt; 0 and ≤ pending qty.</summary>
    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }
}

/// <summary>Result of multi-supplier PO creation from MR.</summary>
public class SupplierSelectionResultDto
{
    public List<CreatedPurchaseOrderInfo> PurchaseOrders { get; set; } = new();
    public int TotalItemsOrdered { get; set; }
}

public class CreatedPurchaseOrderInfo
{
    public Guid PurchaseOrderId { get; set; }
    public string? OrderNumber { get; set; }
    public string? SupplierName { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
}
