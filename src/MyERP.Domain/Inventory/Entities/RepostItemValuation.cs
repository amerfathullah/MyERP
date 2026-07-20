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

    /// <summary>Check if another repost makes this one redundant (covers same or broader scope).</summary>
    public bool IsCoveredBy(RepostItemValuation other)
    {
        if (other.Status == RepostStatus.Completed || other.Status == RepostStatus.Skipped)
            return false; // Already done, not covering

        // Entire company covers everything
        if (other.BasedOn == RepostMethod.EntireCompany && other.CompanyId == CompanyId)
            return other.PostingDate <= PostingDate;

        // Same item+warehouse, earlier or same date
        if (other.ItemId == ItemId && other.WarehouseId == WarehouseId)
            return other.PostingDate <= PostingDate;

        return false;
    }
}

public enum RepostMethod
{
    ItemAndWarehouse = 0,
    ItemWise = 1,
    EntireCompany = 2
}

public enum RepostStatus
{
    Queued = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Skipped = 4
}
