using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace MyERP.Tax.Entities;

/// <summary>
/// Transaction Tax Row — a tax line applied to a transaction document.
/// Reusable across Sales Invoice, Purchase Invoice, SO, PO, Quotation, etc.
/// Each row represents one tax/charge (e.g., "SST 8%", "Shipping", "Customs Duty").
/// Linked to parent document via ParentType/ParentId (polymorphic).
/// </summary>
public class TransactionTaxRow : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Parent document type: SalesInvoice, PurchaseInvoice, SalesOrder, etc.</summary>
    public string ParentType { get; set; } = null!;

    /// <summary>Parent document ID.</summary>
    public Guid ParentId { get; set; }

    /// <summary>Display order (1-based).</summary>
    public int RowIndex { get; set; }

    /// <summary>Tax account to post to.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Description (e.g., "SST - Service Tax", "Shipping Charges").</summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// How this tax is calculated:
    /// "On Net Total" — percentage of document net total
    /// "On Previous Row Amount" — percentage of a specific previous row's amount
    /// "On Previous Row Total" — percentage of a specific previous row's total
    /// "On Item Quantity" — fixed rate per item unit
    /// "Actual" — fixed amount distributed proportionally across items
    /// </summary>
    public string ChargeType { get; set; } = "On Net Total";

    /// <summary>Tax rate (percentage for On Net Total, fixed amount for Actual/On Item Qty).</summary>
    public decimal Rate { get; set; }

    /// <summary>Reference row index for "On Previous Row Amount/Total" charge types.</summary>
    public int? ReferenceRowIndex { get; set; }

    /// <summary>
    /// Tax category for purchasing:
    /// "Total" — adds to grand total
    /// "Valuation" — adds to item cost only (not payable)
    /// "Valuation and Total" — both
    /// </summary>
    public string TaxCategory { get; set; } = "Total";

    /// <summary>If true, this tax is included in the item's print rate (tax-inclusive pricing).</summary>
    public bool IncludedInPrintRate { get; set; }

    /// <summary>Calculated tax amount (populated by TaxesAndTotalsService).</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>Tax amount after discount application.</summary>
    public decimal TaxAmountAfterDiscount { get; set; }

    /// <summary>Running total (net + all taxes up to and including this row).</summary>
    public decimal Total { get; set; }

    /// <summary>Base currency equivalents.</summary>
    public decimal BaseTaxAmount { get; set; }
    public decimal BaseTotal { get; set; }

    protected TransactionTaxRow() { }

    public TransactionTaxRow(Guid id, string parentType, Guid parentId, int rowIndex,
        string description, string chargeType, decimal rate)
        : base(id)
    {
        ParentType = parentType;
        ParentId = parentId;
        RowIndex = rowIndex;
        Description = description;
        ChargeType = chargeType;
        Rate = rate;
    }
}
