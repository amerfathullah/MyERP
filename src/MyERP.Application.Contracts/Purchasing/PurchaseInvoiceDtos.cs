using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing;

public class PurchaseInvoiceDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string? SupplierInvoiceNumber { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierTin { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal BaseNetTotal { get; set; }
    public decimal BaseTaxAmount { get; set; }
    public decimal BaseGrandTotal { get; set; }
    public decimal BaseOutstandingAmount { get; set; }
    public string Status { get; set; } = null!;
    public string EInvoiceStatus { get; set; } = null!;
    public string? LhdnUuid { get; set; }
    public bool IsReturn { get; set; }
    public Guid? ReturnAgainstId { get; set; }
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }
    public Guid CreditToAccountId { get; set; }
    public List<PurchaseInvoiceItemDto> Items { get; set; } = new();
}

public class PurchaseInvoiceItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? PurchaseOrderItemId { get; set; }
    public Guid? PurchaseReceiptItemId { get; set; }
}

/// <summary>3-way matching detail per PI item — enriched with PO/PR quantities and rates.</summary>
public class ThreeWayMatchingItemDto
{
    public Guid PiItemId { get; set; }
    public string ItemDescription { get; set; } = null!;
    public decimal BilledQty { get; set; }
    public decimal BilledRate { get; set; }
    public decimal? OrderedQty { get; set; }
    public decimal? OrderedRate { get; set; }
    public decimal? ReceivedQty { get; set; }
    /// <summary>Qty variance: received - billed (negative = under-receipt, positive = over-receipt vs billing)</summary>
    public decimal? QtyVariance { get; set; }
    /// <summary>Rate variance: PO rate - PI rate (negative = PI charged more than PO)</summary>
    public decimal? RateVariance { get; set; }
    /// <summary>3-Way (PO+PR+PI), 2-Way (PO+PI), Direct (PI only)</summary>
    public string MatchLevel { get; set; } = "Direct";
    public bool HasQtyDiscrepancy { get; set; }
    public bool HasRateDiscrepancy { get; set; }
}

public class CreatePurchaseInvoiceDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid SupplierId { get; set; }
    [Required] public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid? PaymentTermsTemplateId { get; set; }
    [StringLength(100)] public string? SupplierInvoiceNumber { get; set; }
    [StringLength(3)] public string CurrencyCode { get; set; } = "MYR";
    public string? Notes { get; set; }

    /// <summary>Mark as opening balance invoice (data migration). Blocks update_stock, clears payment terms.</summary>
    public bool IsOpening { get; set; }

    /// <summary>Mark as return (debit note). Items must have negative quantities.</summary>
    public bool IsReturn { get; set; }

    /// <summary>Original invoice this return is against.</summary>
    public Guid? ReturnAgainstId { get; set; }

    /// <summary>When true, stock is received on invoice submit (direct purchase without PR).</summary>
    public bool UpdateStock { get; set; }

    /// <summary>Warehouse for stock receipt when UpdateStock=true.</summary>
    public Guid? WarehouseId { get; set; }

    [Required][MinLength(1)] public List<CreatePurchaseInvoiceItemDto> Items { get; set; } = new();
}

public class CreatePurchaseInvoiceItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required][StringLength(500)] public string Description { get; set; } = null!;
    /// <summary>Quantity (positive for normal invoices, negative for debit notes/returns).</summary>
    [Required] public decimal Quantity { get; set; }
    [Required][Range(0, double.MaxValue)] public decimal UnitPrice { get; set; }
    [Range(0, double.MaxValue)] public decimal TaxAmount { get; set; }
    [StringLength(50)] public string Uom { get; set; } = "Unit";
}
