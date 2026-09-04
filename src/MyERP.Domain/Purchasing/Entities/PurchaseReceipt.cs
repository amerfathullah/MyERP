using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Purchase Receipt — records goods received from supplier.
/// Maps to ERPNext stock/doctype/purchase_receipt.
/// Links to Purchase Order; subsequently linked by Purchase Invoice.
/// On submit: increases warehouse stock via stock ledger.
/// </summary>
public class PurchaseReceipt : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAccountableDocument, IAmendable
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }

    public string ReceiptNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }

    public Guid SupplierId { get; set; }

    /// <summary>Reference to Purchase Order this receipt fulfills.</summary>
    public Guid? PurchaseOrderId { get; set; }

    /// <summary>Target warehouse where goods are received.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Supplier's delivery note / DO number.</summary>
    public string? SupplierDeliveryNote { get; set; }

    /// <summary>
    /// Total purchase expense after deducting LCV amounts (for SRBNB/Purchase Expense GL entries).
    /// </summary>
    public decimal PurchaseExpenseTotal => _items.Sum(i => i.GetPurchaseExpenseGlAmount(ExchangeRate));

    public string CurrencyCode { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Whether this is a return receipt (reversal).</summary>
    public bool IsReturn { get; set; }

    /// <summary>If IsReturn, reference to the original purchase receipt.</summary>
    public Guid? ReturnAgainstId { get; set; }

    /// <summary>Whether this receipt is for a subcontracted purchase order.</summary>
    public bool IsSubcontracted { get; set; }

    public string? Notes { get; set; }

    // Amendment support
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    /// <summary>Exchange rate for multi-currency receipts (transaction → company currency).</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    // IAccountableDocument
    string IAccountableDocument.DocumentType => "PurchaseReceipt";
    Guid? IAccountableDocument.CustomerId => null;
    Guid? IAccountableDocument.SupplierId => SupplierId;
    decimal IAccountableDocument.PurchaseExpenseTotal => PurchaseExpenseTotal;

    private readonly List<PurchaseReceiptItem> _items = new();
    public IReadOnlyList<PurchaseReceiptItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Billing completion percentage. Uses MIN% formula per ERPNext StatusUpdater.
    /// Excludes closed rows (settled by close, per ERPNext PR #57596).
    /// </summary>
    public decimal PerBilled
    {
        get
        {
            if (!_items.Any()) return 0;
            var openItems = _items.Where(i => !i.IsClosed).ToList();
            var basis = openItems.Count > 0 ? openItems : _items;
            return Math.Round(basis.Min(i =>
            {
                var absQty = Math.Abs(i.Quantity);
                return absQty == 0 ? 100 : Math.Min(100, Math.Abs(i.BilledQty) / absQty * 100);
            }), 2);
        }
    }

    /// <summary>
    /// Purchase Receipt Billing Status indicator per ERPNext status updater:
    /// Draft -> To Bill (0%) -> Partially Billed (0-100%) -> Completed (100%) / Return / Cancelled / Closed
    /// </summary>
    public string BillingStatus
    {
        get
        {
            if (Status == DocumentStatus.Draft) return "Draft";
            if (Status == DocumentStatus.Cancelled) return "Cancelled";
            if (Status == DocumentStatus.Closed) return "Closed";
            if (PerBilled >= 100m) return "Completed";
            if (IsReturn) return "Return";
            if (PerBilled > 0m) return "Partially Billed";
            return "To Bill";
        }
    }

    protected PurchaseReceipt() { }

    public PurchaseReceipt(Guid id, Guid companyId, Guid supplierId, Guid warehouseId, string receiptNumber, DateTime postingDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        SupplierId = Check.NotDefaultOrNull<Guid>(supplierId, nameof(supplierId));
        WarehouseId = Check.NotDefaultOrNull<Guid>(warehouseId, nameof(warehouseId));
        ReceiptNumber = Check.NotNullOrWhiteSpace(receiptNumber, nameof(receiptNumber), 50);
        PostingDate = postingDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom = "Unit", Guid? purchaseOrderItemId = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Per DO-NOT: returns must always have negative qty
        if (!IsReturn && quantity <= 0)
            throw new ArgumentException("Quantity must be positive for non-return receipts.", nameof(quantity));
        if (IsReturn && quantity >= 0)
            throw new ArgumentException("Quantity must be negative for return receipts.", nameof(quantity));

        _items.Add(new PurchaseReceiptItem(
            Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom, purchaseOrderItemId));

        RecalculateTotals();
    }

    public void ClearItems()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Clear();
        RecalculateTotals();
    }

    public void ValidatePostingDateWithPo(DateTime poTransactionDate)
    {
        if (PostingDate.Date < poTransactionDate.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Posting Date cannot be before the linked Purchase Order date.");
        }
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (PostingDate.Date > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Posting Date cannot be in the future.");
        }

        // Validate from_warehouse, UOM conversion factor, and accepted/rejected quantities per ERPNext buying/stock controller
        foreach (var item in _items)
        {
            item.ValidateAcceptedRejectedQty(IsReturn);

            // Auto-correct conversion factor when UOM equals StockUOM (gotcha #6171)
            if (!string.IsNullOrEmpty(item.Uom) && !string.IsNullOrEmpty(item.StockUom)
                && string.Equals(item.Uom, item.StockUom, StringComparison.OrdinalIgnoreCase)
                && item.ConversionFactor != 1.0m)
            {
                item.ConversionFactor = 1.0m;
            }

            // From Warehouse validation (gotcha #6179)
            if (item.FromWarehouseId.HasValue)
            {
                if (IsSubcontracted)
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", "From Warehouse cannot be set for subcontracted receipts.");

                var targetWarehouse = item.WarehouseId ?? WarehouseId;
                if (item.FromWarehouseId.Value == targetWarehouse)
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", "From Warehouse and Target Warehouse cannot be the same.");
            }
        }

        if (IsReturn && !_items.Any(i => i.Quantity < 0))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "At least one item must be entered with negative quantity in a return document.");
        }

        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = DocumentStatus.Cancelled;
    }

    /// <summary>Closes an individual item row (per ERPNext PR #57596).</summary>
    public void CloseItem(Guid itemRowId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemRowId);
        if (item == null || item.IsClosed) return;
        if (Math.Abs(item.BilledQty) >= Math.Abs(item.Quantity))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Item {item.Description} is already completed in full, so there is nothing to close.");
        }
        item.IsClosed = true;
        if (_items.All(i => i.IsClosed))
        {
            Status = DocumentStatus.Closed;
        }
    }

    /// <summary>Reopens an individual item row.</summary>
    public void ReopenItem(Guid itemRowId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemRowId);
        if (item == null || !item.IsClosed) return;
        item.IsClosed = false;
        if (Status == DocumentStatus.Closed)
        {
            Status = DocumentStatus.Submitted;
        }
    }

    /// <summary>Closes the Purchase Receipt.</summary>
    public void Close()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Closed;
    }

    /// <summary>Reopens a closed Purchase Receipt.</summary>
    public void Reopen()
    {
        if (Status != DocumentStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (_items.Count > 0 && _items.All(i => i.IsClosed))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Every row of Purchase Receipt is closed. Reopen the rows you need instead.");
        }
        Status = DocumentStatus.Submitted;
    }

    private void RecalculateTotals()
    {
        NetTotal = _items.Sum(i => i.LineTotal);
        TaxAmount = _items.Sum(i => i.TaxAmount);
        GrandTotal = NetTotal + TaxAmount;
    }
}
