using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing;

/// <summary>
/// Filter input for Billed Items to be Received report.
/// Maps to ERPNext accounts/report/billed_items_to_be_received.
/// </summary>
public class GetBilledItemsToBeReceivedInput : IEntityDto
{
    public Guid CompanyId { get; set; }

    /// <summary>Cutoff date: only purchase invoices on or before this date are included.</summary>
    public DateTime? PostingDate { get; set; }

    /// <summary>Optional filter by specific purchase invoice.</summary>
    public Guid? PurchaseInvoiceId { get; set; }

    /// <summary>Optional filter by supplier.</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>Optional filter by item.</summary>
    public Guid? ItemId { get; set; }
}

/// <summary>
/// Row item for Billed Items to be Received report.
/// </summary>
public class BilledItemToBeReceivedRowDto
{
    public Guid PurchaseInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }

    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Uom { get; set; } = "Unit";

    public decimal InvoicedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal PendingQty { get; set; }

    public decimal Rate { get; set; }

    /// <summary>Total base amount in company currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>Received amount in company currency.</summary>
    public decimal ReceivedAmount { get; set; }

    /// <summary>Pending amount to be received in company currency.</summary>
    public decimal PendingAmount { get; set; }

    public string Currency { get; set; } = "MYR";
}

/// <summary>
/// Response payload for Billed Items to be Received report.
/// </summary>
public class BilledItemsToBeReceivedReportDto
{
    public List<BilledItemToBeReceivedRowDto> Rows { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal TotalReceivedAmount { get; set; }
    public decimal TotalPendingAmount { get; set; }
    public string Currency { get; set; } = "MYR";
}
