using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Repost Item Valuation — tracks pending/completed stock valuation repost operations.
/// Persists status for audit trail and dedup prevention.
/// Per ERPNext: advisory locking required, timeslot enforcement, parallel processing.
/// Maps to ERPNext stock/doctype/repost_item_valuation.
/// </summary>
public class RepostItemValuation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Repost method: ItemAndWarehouse (specific) or ItemWise or EntireCompany.</summary>
    public RepostMethod BasedOn { get; set; }

    /// <summary>Specific item to repost (null for entire company).</summary>
    public Guid? ItemId { get; set; }

    /// <summary>Specific warehouse to repost (null for all warehouses).</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Repost SLE from this date forward.</summary>
    public DateTime PostingDate { get; set; }

    /// <summary>Posting time for precise SLE ordering.</summary>
    public TimeSpan? PostingTime { get; set; }

    /// <summary>Current processing status.</summary>
    public RepostStatus Status { get; private set; } = RepostStatus.Queued;

    /// <summary>Whether GL entries should also be reposted (slower but ensures GL consistency).</summary>
    public bool AllowZeroRate { get; set; }

    /// <summary>Whether to also repost GL entries after valuation fix.</summary>
    public bool RepostGlEntries { get; set; } = true;

    /// <summary>Whether this repost is only for accounting ledgers (GL), leaving stock ledgers and valuation rates untouched (PR #59307 / commit 18093079d9).</summary>
    public bool RepostOnlyAccountingLedgers { get; set; }

    /// <summary>
    /// Whether to recalculate valuation rate (FIFO/MA/LIFO) on the affected voucher.
    /// Only allowed for Purchase Receipt, Purchase Invoice with Update Stock,
    /// or Stock Entry that is not a material transfer (ERPNext PR #62287 / commit 712806f9ae, 81940327fe).
    /// </summary>
    public bool RecalculateValuationRate { get; set; }

    /// <summary>
    /// Stop reposting an item and warehouse at a later Stock Reconciliation, or when its balance is the same
    /// as the saved ledger. Reposts made by stock transactions set this (ERPNext PR #68488 / commit cd04caf3d6).
    /// </summary>
    public bool SkipUnchangedEntries { get; set; }

    /// <summary>Total SLE entries affected by this repost.</summary>
    public int TotalAffectedEntries { get; set; }

    /// <summary>Entries processed so far (for progress tracking).</summary>
    public int CurrentIndex { get; set; }

    /// <summary>Error message if repost failed.</summary>
    public string? ErrorLog { get; set; }

    /// <summary>Source document that triggered this repost (e.g., backdated Stock Entry).</summary>
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }

    /// <summary>Items that have been reposted (tracking for dedup).</summary>
    public string? ItemsToBeReposted { get; set; }

    /// <summary>Whether this repost has been marked as a duplicate of another.</summary>
    public bool IsDeduplicated { get; set; }

    /// <summary>Reference to another repost that covers this one's scope.</summary>
    public Guid? DedupRepostId { get; set; }

    protected RepostItemValuation() { }

    public RepostItemValuation(Guid id, Guid companyId, RepostMethod basedOn,
        DateTime postingDate, Guid? itemId = null, Guid? warehouseId = null, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        BasedOn = basedOn;
        PostingDate = postingDate;
        ItemId = itemId;
        WarehouseId = warehouseId;
        TenantId = tenantId;
    }

    public void StartProcessing()
    {
        if (Status != RepostStatus.Queued)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = RepostStatus.InProgress;
    }

    public void Complete(int totalAffected)
    {
        Status = RepostStatus.Completed;
        TotalAffectedEntries = totalAffected;
        CurrentIndex = totalAffected;
    }

    public void Fail(string error)
    {
        Status = RepostStatus.Failed;
        ErrorLog = error;
    }

    public void MarkSkipped(string reason)
    {
        Status = RepostStatus.Skipped;
        ErrorLog = reason;
    }

    public void Restart()
    {
        if (Status is not (RepostStatus.Failed or RepostStatus.Skipped or RepostStatus.Cancelled))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = RepostStatus.Queued;
        ErrorLog = null;
        CurrentIndex = 0;
        IsDeduplicated = false;
        DedupRepostId = null;
    }

    public void Cancel()
    {
        if (Status is RepostStatus.Completed or RepostStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = RepostStatus.Cancelled;
    }

    public void ResetRepostOnlyAccountingLedgers()
    {
        if (RepostOnlyAccountingLedgers && BasedOn != RepostMethod.Transaction)
        {
            RepostOnlyAccountingLedgers = false;
        }
    }

    public void ValidateRepostOnlyAccountingLedgers()
    {
        if (!RepostOnlyAccountingLedgers)
            return;

        if (string.Equals(VoucherType, "GL Entry", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "GL reposting is not allowed against the voucher type GL Entry.");
        }
    }

    public void ValidateRecalculateValuationRate(bool? isUpdateStock = null, bool? isTransferStockEntry = null)
    {
        if (!RecalculateValuationRate)
            return;

        if (BasedOn != RepostMethod.Transaction)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Recalculate Valuation Rate is only allowed when Based On is Transaction.");
        }

        if (string.Equals(VoucherType, "Purchase Receipt", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(VoucherType, "Purchase Invoice", StringComparison.OrdinalIgnoreCase))
        {
            if (isUpdateStock != true)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Recalculate Valuation Rate is only allowed for a Purchase Invoice with Update Stock.");
            }
            return;
        }

        if (string.Equals(VoucherType, "Stock Entry", StringComparison.OrdinalIgnoreCase))
        {
            if (isTransferStockEntry == true)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Recalculate Valuation Rate is only allowed for a Purchase Receipt, a Purchase Invoice with Update Stock or a Stock Entry with no row that has both a Source and Target Warehouse.");
            }
            return;
        }

        throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
            .WithData("detail", "Recalculate Valuation Rate is only allowed for a Purchase Receipt, a Purchase Invoice with Update Stock or a Stock Entry with no row that has both a Source and Target Warehouse.");
    }

    public void ValidateUpdateStock(bool? isUpdateStock = null)
    {
        if (BasedOn == RepostMethod.Transaction && string.Equals(VoucherType, "Purchase Invoice", StringComparison.OrdinalIgnoreCase))
        {
            if (isUpdateStock != true)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Update Stock must be enabled for Purchase Invoice {VoucherId}.");
            }
        }
    }

    /// <summary>Check if another repost makes this one redundant (covers same or broader scope).</summary>
    public bool IsCoveredBy(RepostItemValuation other)
    {
        if (other.Status == RepostStatus.Completed || other.Status == RepostStatus.Skipped || other.Status == RepostStatus.Cancelled)
            return false; // Already done/cancelled, not covering

        // GL-only repost and full valuation repost do not cover each other
        if (other.RepostOnlyAccountingLedgers != RepostOnlyAccountingLedgers)
            return false;

        if (other.RecalculateValuationRate != RecalculateValuationRate)
            return false;

        // Transaction-based repost: exact voucher match
        if (BasedOn == RepostMethod.Transaction && other.BasedOn == RepostMethod.Transaction)
        {
            return other.VoucherType == VoucherType && other.VoucherId == VoucherId;
        }

        // Entire company covers everything
        if (other.BasedOn == RepostMethod.EntireCompany && other.CompanyId == CompanyId)
            return other.PostingDate <= PostingDate;

        // Same item+warehouse, earlier or same date
        if (other.ItemId == ItemId && other.WarehouseId == WarehouseId)
            return other.PostingDate <= PostingDate;

        return false;
    }

    /// <summary>
    /// Evaluates whether a repost stream for an (item, warehouse) has settled at the current Stock Ledger Entry
    /// and can pause early per ERPNext PR #68488 / commit cd04caf3d6 (<c>is_settled</c> in <c>stock_ledger.py</c>):
    /// - Requires <paramref name="canSkipUnchangedEntries"/> and <paramref name="entryPostingDateTime"/> &gt; <paramref name="walkStartDateTime"/>.
    /// - Settles immediately at a non-adjustment, non-batch, non-dimension Stock Reconciliation balance reset
    ///   (provided serial valuation is off or item is non-serial).
    /// - Otherwise settles when the running balance (<c>QtyAfterTransaction</c>, <c>StockValue</c>, <c>ValuationRate</c>, <c>StockQueue</c>)
    ///   equals the saved balance at ledger precision and no un-reset ledger change occurred on or before <paramref name="entryPostingDateTime"/>.
    /// </summary>
    public static bool IsRepostSettledAtEntry(
        bool canSkipUnchangedEntries,
        DateTime entryPostingDateTime,
        DateTime walkStartDateTime,
        string? voucherType,
        bool isAdjustmentEntry,
        bool hasBatchNo,
        bool hasSerialNo,
        bool skipSerialBatchValuation,
        bool hasInventoryDimensions,
        (decimal QtyAfterTransaction, decimal StockValue, decimal ValuationRate, string? StockQueue) runningBalance,
        (decimal QtyAfterTransaction, decimal StockValue, decimal ValuationRate, string? StockQueue) savedBalance,
        bool isLedgerShifted,
        int qtyPrecision = 4,
        int currencyPrecision = 2)
    {
        var isBalanceReset = string.Equals(voucherType?.Replace(" ", string.Empty), "StockReconciliation", StringComparison.OrdinalIgnoreCase)
            && !isAdjustmentEntry
            && !hasBatchNo
            && !hasInventoryDimensions
            && (!hasSerialNo || skipSerialBatchValuation);

        if (!canSkipUnchangedEntries || entryPostingDateTime <= walkStartDateTime)
        {
            return false;
        }

        if (isBalanceReset)
        {
            return true;
        }

        if ((hasSerialNo || hasBatchNo) && !skipSerialBatchValuation)
        {
            return false;
        }

        var matchesSavedBalance =
            Math.Round(runningBalance.QtyAfterTransaction, qtyPrecision, MidpointRounding.AwayFromZero)
                == Math.Round(savedBalance.QtyAfterTransaction, qtyPrecision, MidpointRounding.AwayFromZero)
            && Math.Round(runningBalance.StockValue, currencyPrecision, MidpointRounding.AwayFromZero)
                == Math.Round(savedBalance.StockValue, currencyPrecision, MidpointRounding.AwayFromZero)
            && Math.Round(runningBalance.ValuationRate, 9, MidpointRounding.AwayFromZero)
                == Math.Round(savedBalance.ValuationRate, 9, MidpointRounding.AwayFromZero)
            && string.Equals(runningBalance.StockQueue ?? "[]", savedBalance.StockQueue ?? "[]", StringComparison.Ordinal);

        return matchesSavedBalance && !isLedgerShifted;
    }

    /// <summary>
    /// Determines whether a queued repost at <paramref name="queuedRepostDateTime"/> is covered by a completed repost
    /// that paused early at <paramref name="repostedTill"/> per ERPNext commit cd04caf3d6 (<c>is_covered_till</c>):
    /// When <paramref name="repostedTill"/> is set, only a queued repost with <paramref name="queuedSkipUnchangedEntries"/> == true
    /// and <paramref name="queuedRepostDateTime"/> &lt; <paramref name="repostedTill"/> is covered.
    /// </summary>
    public static bool IsQueuedRepostCoveredTill(
        bool queuedSkipUnchangedEntries,
        DateTime queuedRepostDateTime,
        DateTime? repostedTill)
    {
        if (!repostedTill.HasValue)
        {
            return true;
        }

        return queuedSkipUnchangedEntries && queuedRepostDateTime < repostedTill.Value;
    }
}

public enum RepostMethod
{
    ItemAndWarehouse = 0,
    ItemWise = 1,
    EntireCompany = 2,
    Transaction = 3
}

public enum RepostStatus
{
    Queued = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Skipped = 4,
    Cancelled = 5
}
