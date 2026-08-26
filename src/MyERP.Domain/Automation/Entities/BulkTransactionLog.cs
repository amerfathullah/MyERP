using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Automation.Entities;

/// <summary>
/// Bulk Transaction Log — tracks batch document processing across transactions.
/// Maps to ERPNext bulk_transaction/doctype/bulk_transaction_log.
/// </summary>
public class BulkTransactionLog : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Title { get; set; } = null!;
    public DateTime BatchDate { get; set; }
    public int TotalEntries { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }

    public virtual ICollection<BulkTransactionLogDetail> Details { get; protected set; } = new List<BulkTransactionLogDetail>();

    protected BulkTransactionLog() { }

    public BulkTransactionLog(
        Guid id,
        string title,
        DateTime batchDate,
        Guid? tenantId = null)
        : base(id)
    {
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), BulkTransactionConsts.MaxTitleLength);
        BatchDate = batchDate;
        TenantId = tenantId;
    }

    public BulkTransactionLogDetail AddDetail(
        Guid detailId,
        string transactionName,
        string fromDocType,
        string toDocType)
    {
        var detail = new BulkTransactionLogDetail(
            detailId,
            Id,
            transactionName,
            fromDocType,
            toDocType,
            TenantId);

        Details.Add(detail);
        RecalculateCounts();
        return detail;
    }

    public void RecordSuccess(Guid detailId)
    {
        var detail = Details.FirstOrDefault(d => d.Id == detailId);
        if (detail == null)
        {
            throw new UserFriendlyException($"Detail with id {detailId} not found");
        }

        detail.Status = BulkTransactionStatus.Success;
        detail.ExecutedTime = DateTime.UtcNow;
        detail.ErrorDescription = null;
        RecalculateCounts();
    }

    public void RecordFailure(Guid detailId, string errorDescription)
    {
        var detail = Details.FirstOrDefault(d => d.Id == detailId);
        if (detail == null)
        {
            throw new UserFriendlyException($"Detail with id {detailId} not found");
        }

        detail.Status = BulkTransactionStatus.Failed;
        detail.ExecutedTime = DateTime.UtcNow;
        detail.ErrorDescription = errorDescription;
        RecalculateCounts();
    }

    public void RetryDetail(Guid detailId)
    {
        var detail = Details.FirstOrDefault(d => d.Id == detailId);
        if (detail == null)
        {
            throw new UserFriendlyException($"Detail with id {detailId} not found");
        }

        detail.Status = BulkTransactionStatus.Retried;
        detail.RetriedCount++;
        RecalculateCounts();
    }

    private void RecalculateCounts()
    {
        TotalEntries = Details.Count;
        SucceededCount = Details.Count(d => d.Status == BulkTransactionStatus.Success);
        FailedCount = Details.Count(d => d.Status == BulkTransactionStatus.Failed);
    }
}

/// <summary>
/// Bulk Transaction Log Detail — individual item record in a batch run.
/// Maps to ERPNext bulk_transaction/doctype/bulk_transaction_log_detail.
/// </summary>
public class BulkTransactionLogDetail : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid BulkTransactionLogId { get; set; }
    public string TransactionName { get; set; } = null!;
    public string FromDocType { get; set; } = null!;
    public string ToDocType { get; set; } = null!;
    public BulkTransactionStatus Status { get; set; } = BulkTransactionStatus.Queued;
    public string? ErrorDescription { get; set; }
    public DateTime? ExecutedTime { get; set; }
    public int RetriedCount { get; set; }

    protected BulkTransactionLogDetail() { }

    public BulkTransactionLogDetail(
        Guid id,
        Guid bulkTransactionLogId,
        string transactionName,
        string fromDocType,
        string toDocType,
        Guid? tenantId = null)
        : base(id)
    {
        BulkTransactionLogId = bulkTransactionLogId;
        TransactionName = Check.NotNullOrWhiteSpace(transactionName, nameof(transactionName), BulkTransactionConsts.MaxTransactionNameLength);
        FromDocType = Check.NotNullOrWhiteSpace(fromDocType, nameof(fromDocType), BulkTransactionConsts.MaxDocTypeLength);
        ToDocType = Check.NotNullOrWhiteSpace(toDocType, nameof(toDocType), BulkTransactionConsts.MaxDocTypeLength);
        TenantId = tenantId;
        Status = BulkTransactionStatus.Queued;
    }
}
