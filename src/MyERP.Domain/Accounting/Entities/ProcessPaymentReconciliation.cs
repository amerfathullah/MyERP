using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Process Payment Reconciliation — the automated counterpart to the manual Payment Reconciliation
/// tool: greedy-matches a party's unreconciled payments against its outstanding invoices without a
/// user picking allocations by hand. Maps to ERPNext accounts/doctype/process_payment_reconciliation.
///
/// Diverges from ERPNext's exact 5-phase Queued/Running/Paused state machine in one way: no explicit
/// Pause/Resume — Cancel covers the practical "stop this" need without the extra state-juggling a
/// resumable-pause would add. The chained one-invoice-at-a-time background job architecture (bounded
/// memory/time per job run, self-re-enqueue) is kept, since that's the part that actually matters for
/// not timing out on a party with hundreds of outstanding invoices.
///
/// Lifecycle: Draft (picking party/account) -&gt; Queued (Submit) -&gt; Running (background job) -&gt;
/// Completed (all outstanding reconciled or no more payments to allocate) / PartiallyReconciled (an
/// error hit mid-batch after some allocations succeeded) / Failed (error hit before any succeeded).
/// Cancellable from Draft/Queued/Running.
/// </summary>
public class ProcessPaymentReconciliation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; private set; }

    /// <summary>"Customer" or "Supplier".</summary>
    public string PartyType { get; private set; } = null!;
    public Guid PartyId { get; private set; }

    /// <summary>The receivable (Customer) or payable (Supplier) account to reconcile against.</summary>
    public Guid ReceivablePayableAccountId { get; private set; }

    /// <summary>Optional advance account — included in the active-queue dedup key per ERPNext, so a
    /// request scoped to a specific advance account doesn't block/get blocked by one that isn't.</summary>
    public Guid? DefaultAdvanceAccountId { get; private set; }

    public ProcessPaymentReconciliationStatus Status { get; private set; } = ProcessPaymentReconciliationStatus.Draft;

    /// <summary>Count of invoice-allocation batches successfully reconciled so far — used both for
    /// progress display and to distinguish Failed (0 reconciled) from PartiallyReconciled (some).</summary>
    public int ReconciledCount { get; private set; }

    public string? ErrorLog { get; private set; }

    protected ProcessPaymentReconciliation() { }

    public ProcessPaymentReconciliation(
        Guid id, Guid companyId, string partyType, Guid partyId,
        Guid receivablePayableAccountId, Guid? defaultAdvanceAccountId = null, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        PartyType = Check.NotNullOrWhiteSpace(partyType, nameof(partyType));
        PartyId = Check.NotDefaultOrNull<Guid>(partyId, nameof(partyId));
        ReceivablePayableAccountId = Check.NotDefaultOrNull<Guid>(receivablePayableAccountId, nameof(receivablePayableAccountId));
        DefaultAdvanceAccountId = defaultAdvanceAccountId;
        TenantId = tenantId;
    }

    /// <summary>Draft -&gt; Queued. Hands the request to the background job.</summary>
    public void Submit()
    {
        if (Status != ProcessPaymentReconciliationStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = ProcessPaymentReconciliationStatus.Queued;
    }

    /// <summary>Queued/Running (retry) -&gt; Running. Restart-safe like RepostAccountingLedger's own
    /// StartProcessing — blocks only a finished (Completed/PartiallyReconciled/Failed) or Cancelled
    /// request.</summary>
    public void StartProcessing()
    {
        if (Status is ProcessPaymentReconciliationStatus.Completed
            or ProcessPaymentReconciliationStatus.PartiallyReconciled
            or ProcessPaymentReconciliationStatus.Failed
            or ProcessPaymentReconciliationStatus.Cancelled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        }

        Status = ProcessPaymentReconciliationStatus.Running;
    }

    public void RecordProgress(int reconciledDelta)
    {
        ReconciledCount += reconciledDelta;
    }

    public void Complete()
    {
        Status = ProcessPaymentReconciliationStatus.Completed;
    }

    /// <summary>Per ERPNext: Failed when nothing was reconciled before the error, PartiallyReconciled
    /// when at least one allocation batch succeeded first.</summary>
    public void RecordFailure(string error)
    {
        ErrorLog = error;
        Status = ReconciledCount > 0
            ? ProcessPaymentReconciliationStatus.PartiallyReconciled
            : ProcessPaymentReconciliationStatus.Failed;
    }

    /// <summary>Draft/Queued/Running only — a finished or already-cancelled request can't be
    /// re-cancelled.</summary>
    public void Cancel()
    {
        if (Status is ProcessPaymentReconciliationStatus.Completed
            or ProcessPaymentReconciliationStatus.PartiallyReconciled
            or ProcessPaymentReconciliationStatus.Failed
            or ProcessPaymentReconciliationStatus.Cancelled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        }

        Status = ProcessPaymentReconciliationStatus.Cancelled;
    }
}

public enum ProcessPaymentReconciliationStatus
{
    Draft = 0,
    Queued = 1,
    Running = 2,
    Completed = 3,
    PartiallyReconciled = 4,
    Failed = 5,
    Cancelled = 6
}
