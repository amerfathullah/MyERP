using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

/// <summary>
/// Filter input for Delivered Items to be Billed report.
/// Maps to ERPNext accounts/report/delivered_items_to_be_billed.
/// </summary>
public class GetDeliveredItemsToBeBilledInput : IEntityDto
{
    public Guid CompanyId { get; set; }

    /// <summary>Cutoff date: only delivery notes on or before this date are included.</summary>
    public DateTime? PostingDate { get; set; }

    /// <summary>Optional filter by specific delivery note.</summary>
    public Guid? DeliveryNoteId { get; set; }

    /// <summary>Optional filter by customer.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Optional filter by item.</summary>
    public Guid? ItemId { get; set; }
}

/// <summary>
/// Row item for Delivered Items to be Billed report.
/// </summary>
public class DeliveredItemToBeBilledRowDto
{
    public Guid DeliveryNoteId { get; set; }
    public string DeliveryNumber { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }

    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public decimal DeliveredQty { get; set; }
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
/// Response payload for Delivered Items to be Billed report.
/// </summary>
public class DeliveredItemsToBeBilledReportDto
{
    public List<DeliveredItemToBeBilledRowDto> Rows { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal TotalBilledAmount { get; set; }
    public decimal TotalReturnedAmount { get; set; }
    public decimal TotalPendingAmount { get; set; }
    public string Currency { get; set; } = "MYR";
}
