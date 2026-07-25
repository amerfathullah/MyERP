using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MyERP.Sales;

namespace MyERP.Application.Contracts.Sales;

// ─── Output DTOs ───

public class ProformaInvoiceDto
{
    public Guid Id { get; set; }
    public string ProformaNumber { get; set; } = null!;
    public DateTime ProformaDate { get; set; }
    public Guid SalesOrderId { get; set; }
    public string? SalesOrderNumber { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public ProformaInvoiceBasis BasedOn { get; set; }
    public bool HideItemQty { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal TotalQty { get; set; }
    public ProformaInvoiceStatus Status { get; set; }
    public string? ProformaPdfUrl { get; set; }
    public DateTime? SentOn { get; set; }
    public string? EmailedTo { get; set; }
    public List<ProformaInvoiceItemDto> Items { get; set; } = new();
}

public class ProformaInvoiceItemDto
{
    public Guid Id { get; set; }
    public Guid SalesOrderItemId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Uom { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}

// ─── Input DTOs ───

public class CreateProformaInvoiceDto
{
    [Required]
    public Guid SalesOrderId { get; set; }

    public ProformaInvoiceBasis BasedOn { get; set; } = ProformaInvoiceBasis.Quantity;
    public bool HideItemQty { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    public List<CreateProformaInvoiceItemDto> Items { get; set; } = new();
}

public class CreateProformaInvoiceItemDto
{
    [Required]
    public Guid SalesOrderItemId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    /// <summary>For Amount basis: user-entered amount (rate derived). For Quantity basis: ignored (uses SO rate).</summary>
    public decimal? Amount { get; set; }
}

public class SendProformaEmailDto
{
    [Required]
    public string Recipients { get; set; } = null!;
}

/// <summary>Per-item proformed totals (already issued against this SO item).</summary>
public class ProformedTotalsDto
{
    public Guid SalesOrderItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal OrderedQty { get; set; }
    public decimal OrderedAmount { get; set; }
    public decimal ProformedQty { get; set; }
    public decimal ProformedAmount { get; set; }
    public decimal RemainingQty { get; set; }
    public decimal RemainingAmount { get; set; }
}
