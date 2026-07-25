using System;
using System.Collections.Generic;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Packing Slip — tracks how items from a Delivery Note are packed into cases.
/// Case number ranges validated for overlap (per gotcha #134: 3-condition query).
/// Per DO-NOT: Product Bundles (packed items) in internal transfer documents are hard-blocked.
/// Maps to ERPNext stock/doctype/packing_slip.
/// </summary>
public class PackingSlip : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Linked Delivery Note.</summary>
    public Guid DeliveryNoteId { get; set; }

    /// <summary>Start case number for this packing slip.</summary>
    public int FromCaseNo { get; set; } = 1;

    /// <summary>End case number (FromCaseNo to ToCaseNo = number of cases).</summary>
    public int ToCaseNo { get; set; } = 1;

    /// <summary>Total net weight of all packed items.</summary>
    public decimal NetWeight { get; set; }

    /// <summary>Total gross weight (net + packaging).</summary>
    public decimal GrossWeight { get; set; }

    /// <summary>Weight UOM (e.g., Kg, Lbs).</summary>
    public string? WeightUom { get; set; }

    /// <summary>Number of cases = ToCaseNo - FromCaseNo + 1.</summary>
    public int NumberOfCases => ToCaseNo - FromCaseNo + 1;

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public ICollection<PackingSlipItem> Items { get; private set; } = new List<PackingSlipItem>();

    protected PackingSlip() { }

    public PackingSlip(
        Guid id,
        Guid companyId,
        Guid deliveryNoteId,
        int fromCaseNo,
        int toCaseNo,
        Guid? tenantId = null) : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        DeliveryNoteId = Check.NotDefaultOrNull<Guid>(deliveryNoteId, nameof(deliveryNoteId));
        TenantId = tenantId;

        if (fromCaseNo < 1)
            throw new ArgumentException("From case number must be >= 1", nameof(fromCaseNo));
        if (toCaseNo < fromCaseNo)
            throw new ArgumentException("To case number must be >= from case number", nameof(toCaseNo));

        FromCaseNo = fromCaseNo;
        ToCaseNo = toCaseNo;
    }

    public void AddItem(Guid itemId, decimal qty, decimal netWeight, string? description = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException("MyERP:01001");
        if (qty <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(qty));

        Items.Add(new PackingSlipItem(Guid.NewGuid(), Id, itemId, qty, netWeight, description));
        RecalculateWeight();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException("MyERP:01001");
        if (Items.Count == 0)
            throw new BusinessException("MyERP:01007");
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException("MyERP:01001");
        Status = DocumentStatus.Cancelled;
    }

    private void RecalculateWeight()
    {
        NetWeight = 0;
        foreach (var item in Items)
            NetWeight += item.NetWeight;
    }

    /// <summary>
    /// Validates case number range doesn't overlap with existing packing slips.
    /// Uses 3-condition overlap formula per gotcha #134.
    /// </summary>
    public static bool HasOverlap(int from1, int to1, int from2, int to2)
    {
        // Overlap: from1 between existing, to1 between existing, OR existing contains from1
        return (from1 >= from2 && from1 <= to2) ||
               (to1 >= from2 && to1 <= to2) ||
               (from2 >= from1 && from2 <= to1);
    }
}

/// <summary>
/// Individual item row in a Packing Slip.
/// </summary>
public class PackingSlipItem : Entity<Guid>
{
    public Guid PackingSlipId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public decimal NetWeight { get; set; }
    public string? Description { get; set; }

    /// <summary>Reference to the DN item this packing row covers.</summary>
    public Guid? DeliveryNoteItemId { get; set; }

    /// <summary>Reference to the PI item if billing is linked.</summary>
    public Guid? PurchaseInvoiceItemId { get; set; }

    protected PackingSlipItem() { }

    public PackingSlipItem(Guid id, Guid packingSlipId, Guid itemId, decimal qty, decimal netWeight, string? description = null)
        : base(id)
    {
        PackingSlipId = packingSlipId;
        ItemId = itemId;
        Qty = qty;
        NetWeight = netWeight;
        Description = description;
    }
}
