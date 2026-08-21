using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class SalesOrderDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPoNumber { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Terms { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = null!;
    public Guid? QuotationId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public decimal PerDelivered { get; set; }
    public decimal PerBilled { get; set; }
    public decimal AdvancePaid { get; set; }
    public decimal PerAdvancePaid { get; set; }

    /// <summary>Date of first delivery note against this order (null if none yet).</summary>
    public DateTime? FirstDeliveryDate { get; set; }
    /// <summary>Date of last delivery note against this order (null if none yet).</summary>
    public DateTime? LastDeliveryDate { get; set; }
    /// <summary>Date of first invoice against this order (null if none yet).</summary>
    public DateTime? FirstBilledDate { get; set; }
    /// <summary>Date of first payment received for this order (null if none yet).</summary>
    public DateTime? FirstPaymentDate { get; set; }

    /// <summary>Warning message if customer has overdue invoices (advisory, not blocking).</summary>
    public string? OverdueWarning { get; set; }

    public List<SalesOrderItemDto> Items { get; set; } = new();

    /// <summary>Sales team allocations for commission tracking.</summary>
    public List<SalesTeamEntryDto> SalesTeam { get; set; } = new();
}

/// <summary>Payment entries linked to this Sales Order (advance payments).</summary>
public class OrderPaymentDto
{
    public Guid PaymentEntryId { get; set; }
    public string PaymentNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public decimal PaidAmount { get; set; }
    public string PaymentType { get; set; } = null!;
    public string? ReferenceNumber { get; set; }
    public string Status { get; set; } = null!;
}

/// <summary>Purchase Receipt entries linked to a Purchase Order (receipt tracking).</summary>
public class OrderReceiptDto
{
    public Guid PurchaseReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public string Status { get; set; } = null!;
    public int ItemCount { get; set; }
}

public class SalesOrderItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal BilledQty { get; set; }
    public Guid? WarehouseId { get; set; }

    /// <summary>Blanket Order this line draws from (null if not linked to one).</summary>
    public Guid? BlanketOrderId { get; set; }

    /// <summary>Available stock qty for this item (populated on create/get for warehouse visibility).</summary>
    public decimal AvailableQty { get; set; }

    /// <summary>True when ordered qty exceeds available stock (triggers low-stock warning in UI).</summary>
    public bool IsInsufficientStock { get; set; }
}

public class CreateSalesOrderDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public DateTime OrderDate { get; set; }

    public DateTime? DeliveryDate { get; set; }

    [StringLength(SalesOrderConsts.MaxCustomerPoLength)]
    public string? CustomerPoNumber { get; set; }

    [StringLength(SalesOrderConsts.MaxCurrencyCodeLength)]
    public string CurrencyCode { get; set; } = "MYR";

    [StringLength(SalesOrderConsts.MaxTermsLength)]
    public string? Terms { get; set; }

    [StringLength(SalesOrderConsts.MaxNoteLength)]
    public string? Notes { get; set; }

    /// <summary>Source quotation ID when converting from quotation.</summary>
    public Guid? QuotationId { get; set; }

    /// <summary>Cost center for departmental P&L attribution.</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Project for project-wise cost tracking.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Shipping destination country code (for shipping rule matching).</summary>
    public string? ShippingCountry { get; set; }

    /// <summary>Coupon code to apply pricing discount (validated + recorded on creation).</summary>
    public string? CouponCode { get; set; }

    /// <summary>Loyalty points to redeem (reduces grand total by redemption value).</summary>
    public int LoyaltyPointsToRedeem { get; set; }

    /// <summary>Sales team allocations (allocated percentage must total exactly 100%).</summary>
    public List<SalesTeamAllocationInputDto>? SalesTeam { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateSalesOrderItemDto> Items { get; set; } = new();
}

public class CreateSalesOrderItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = null!;

    [Required]
    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TaxAmount { get; set; }

    [StringLength(20)]
    public string Uom { get; set; } = "Unit";

    public Guid? WarehouseId { get; set; }

    /// <summary>Blanket Order this line draws from — qty deducted from the blanket allocation on submit.</summary>
    public Guid? BlanketOrderId { get; set; }
}

/// <summary>
/// Delivery schedule entry for planned delivery windows per SO item.
/// </summary>
public class DeliveryScheduleEntryDto
{
    public Guid Id { get; set; }
    public Guid SalesOrderItemId { get; set; }
    public DateTime ScheduledDate { get; set; }
    public decimal ScheduledQty { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal PendingQty { get; set; }
    public bool IsFullyDelivered { get; set; }
}
