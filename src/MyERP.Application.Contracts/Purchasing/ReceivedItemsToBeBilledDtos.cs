using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing;

/// <summary>
/// Filter input for Received Items to be Billed report.
/// Maps to ERPNext accounts/report/received_items_to_be_billed.
/// </summary>
public class GetReceivedItemsToBeBilledInput : IEntityDto
{
    public Guid CompanyId { get; set; }

    /// <summary>Cutoff date: only purchase receipts on or before this date are included.</summary>
    public DateTime? PostingDate { get; set; }

    /// <summary>Optional filter by specific purchase receipt.</summary>
    public Guid? PurchaseReceiptId { get; set; }

    /// <summary>Optional filter by supplier.</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>Optional filter by item.</summary>
    public Guid? ItemId { get; set; }
}

/// <summary>
/// Row item for Received Items to be Billed report.
/// </summary>
public class ReceivedItemToBeBilledRowDto
{
    public Guid PurchaseReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }

    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public decimal ReceivedQty { get; set; }
    public decimal BilledQty { get; set; }
    public decimal ReturnedQty { get; set; }
    public decimal PendingQty { get; set; }

    public decimal Rate { get; set; }

    /// <summary>Total base amount in company currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>Billed amount in company currency.</summary>
    public decimal BilledAmount { get; set; }

    /// <summary>Returned amount in company currency.</summary>
    public decimal ReturnedAmount { get; set; }

    /// <summary>Pending amount to be billed in company currency.</summary>
    public decimal PendingAmount { get; set; }

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public string Currency { get; set; } = "MYR";
}

/// <summary>
/// Response payload for Received Items to be Billed report.
/// </summary>
public class ReceivedItemsToBeBilledReportDto
{
    public List<ReceivedItemToBeBilledRowDto> Rows { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal TotalBilledAmount { get; set; }
    public decimal TotalReturnedAmount { get; set; }
    public decimal TotalPendingAmount { get; set; }
    public string Currency { get; set; } = "MYR";
}
