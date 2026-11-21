using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Manufacturing;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Subcontracting Receipt — receipt of finished goods from subcontractor.
/// Triggers: SLE for FG stock-in, consumed RM stock-out, cost calculation, GL posting.
/// </summary>
public class SubcontractingReceipt : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAccountableDocument
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string ReceiptNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }

    public Guid SupplierId { get; set; }
    public Guid SubcontractingOrderId { get; set; }

    public Guid? WarehouseId { get; set; }

    /// <summary>Warehouse where raw materials are held by the subcontractor. Per ERPNext PR #64867 (commit 1bcdf9c96f).</summary>
    public Guid? SupplierWarehouseId { get; set; }

    public decimal NetTotal { get; set; }

    // IAccountableDocument implementation
    public string DocumentType => "SubcontractingReceipt";
    public decimal GrandTotal => NetTotal;
    public decimal TaxAmount => 0;
    public Guid? CustomerId => null;
    Guid? IAccountableDocument.SupplierId => SupplierId;
    public string CurrencyCode { get; set; } = "MYR";
    public decimal ExchangeRate { get; set; } = 1m;

    public SubcontractingReceiptStatus Status { get; private set; } = SubcontractingReceiptStatus.Draft;
    public bool IsReturn { get; set; }
    public Guid? ReturnAgainstReceiptId { get; set; }

    /// <summary>Linked Project (propagated from SCO and enforced per PR #58965).</summary>
    public Guid? ProjectId { get; set; }

    private readonly List<SubcontractingReceiptItem> _items = new();
    public IReadOnlyList<SubcontractingReceiptItem> Items => _items.AsReadOnly();

    protected SubcontractingReceipt() { }

    public SubcontractingReceipt(Guid id, Guid companyId, string receiptNumber, DateTime postingDate,
        Guid supplierId, Guid scoId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ReceiptNumber = receiptNumber;
        PostingDate = postingDate;
        SupplierId = supplierId;
        SubcontractingOrderId = scoId;
        TenantId = tenantId;
    }

    public void AddItem(SubcontractingReceiptItem item)
    {
        if (Status != SubcontractingReceiptStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Add(item);
        NetTotal = _items.Sum(i => i.Amount);
    }

    public void Submit()
    {
        if (Status != SubcontractingReceiptStatus.Draft || !_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = SubcontractingReceiptStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status == SubcontractingReceiptStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = SubcontractingReceiptStatus.Cancelled;
    }

    /// <summary>
    /// Validates that a return Subcontracting Receipt keeps the original receipt's supplier warehouse.
    /// Per ERPNext PR #65109 (commit d7feab165b).
    /// </summary>
    public void ValidateReturnSupplierWarehouse(Guid? originalSupplierWarehouseId, string originalReceiptNumber)
    {
        if (!IsReturn || !ReturnAgainstReceiptId.HasValue) return;

        if (originalSupplierWarehouseId.HasValue && SupplierWarehouseId != originalSupplierWarehouseId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Supplier Warehouse must be {originalSupplierWarehouseId.Value}, the same as in {originalReceiptNumber}.");
        }
    }

    /// <summary>
    /// Validates that every Secondary Item row on this Subcontracting Receipt references a Finished Good row
    /// that is present on this receipt per ERPNext commits 31d2158574 and 3c355788bb (<c>validate_secondary_items</c>).
    /// </summary>
    public void ValidateSecondaryItemsHaveFinishedGoodRow()
    {
        var fgRowIds = _items
            .Where(i => string.IsNullOrWhiteSpace(i.SecondaryItemType) && !i.ValuationType.HasValue)
            .Select(i => i.Id)
            .ToHashSet();

        for (var idx = 0; idx < _items.Count; idx++)
        {
            var item = _items[idx];
            if (!string.IsNullOrWhiteSpace(item.SecondaryItemType) || item.ValuationType.HasValue)
            {
                if (!item.ReferenceFinishedGoodRowId.HasValue || !fgRowIds.Contains(item.ReferenceFinishedGoodRowId.Value))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Row #{idx + 1}: The Finished Good row of Secondary Item {item.ItemName} is not on this receipt. Remove the Secondary Item row.");
                }
            }
        }
    }
}

public class SubcontractingReceiptItem : Entity<Guid>
{
    public Guid SubcontractingReceiptId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal RejectedQty { get; set; }
    public decimal ProcessLossQty { get; set; }
    public decimal ReturnedQty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount => Qty * Rate;
    public Guid? WarehouseId { get; set; }
    public Guid? RejectedWarehouseId { get; set; }
    public bool ReturnQtyFromRejectedWarehouse { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public Guid? ServiceExpenseAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }

    /// <summary>Finished Good row on this receipt that a Secondary Item row belongs to (ERPNext reference_name, commit 31d2158574).</summary>
    public Guid? ReferenceFinishedGoodRowId { get; set; }

    /// <summary>Returnable quantity excluding process loss (<c>ReceivedQty - ProcessLossQty</c>) per ERPNext commit 17c2629110.</summary>
    public decimal ReturnableQty => Math.Max(0m, (Qty + RejectedQty) - ProcessLossQty);

    /// <summary>
    /// Determines whether a return row with <paramref name="currentReturnReceivedQty"/> (negative) completes the return
    /// of this receipt row's returnable quantity (<c>ReceivedQty - ProcessLossQty</c>) per ERPNext commit 22222b39a8.
    /// </summary>
    public bool IsFinalFinishedGoodReturn(decimal currentReturnReceivedQty, int precision = 4)
    {
        var totalReturned = Math.Round(ReturnedQty - currentReturnReceivedQty, precision, MidpointRounding.AwayFromZero);
        var returnable = Math.Round((Qty + RejectedQty) - ProcessLossQty, precision, MidpointRounding.AwayFromZero);
        return totalReturned >= returnable;
    }

    /// <summary>For secondary items: Co-Product, By-Product, Scrap, Additional Finished Good (PR #59021).</summary>
    public string? SecondaryItemType { get; set; }

    /// <summary>Valuation method for secondary item (PR #59021 / commit bec627c3eb).</summary>
    public SecondaryItemValuationType? ValuationType { get; set; }

    /// <summary>Cost allocation percentage when ValuationType is PercentageOfComponentCost (PR #59021).</summary>
    public decimal? CostAllocationPercentage { get; set; }

    /// <summary>BOM Secondary Item reference when created from a BOM secondary item (PR #59021).</summary>
    public Guid? BomSecondaryItemId { get; set; }

    /// <summary>Batch ID for batch-tracked finished/secondary item (PR #61505).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Guid? BatchId { get; set; }

    protected SubcontractingReceiptItem() { }
    public SubcontractingReceiptItem(Guid id, Guid scrId, Guid itemId, string itemName, decimal qty, decimal rate, Guid? projectId = null)
        : base(id)
    {
        SubcontractingReceiptId = scrId; ItemId = itemId; ItemName = itemName; Qty = qty; Rate = rate; ProjectId = projectId;
    }
}
