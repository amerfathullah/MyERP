using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Repost Accounting Ledger — an admin tool that rebuilds the GL/PLE of a batch of already-Posted
/// vouchers from their current field values, without re-running the full submit workflow. Used to
/// correct drift after a retroactive account/dimension fix, not for routine reposting. Maps to
/// ERPNext accounts/doctype/repost_accounting_ledger.
///
/// Diverges from ERPNext's own implementation in one deliberate way: ERPNext optionally *deletes*
/// the voucher's existing GL Entry rows before rebuilding (delete_cancelled_entries). This codebase
/// never deletes ledger rows anywhere (see DocumentPostingOrchestrator.ReverseGlForDocumentAsync) —
/// every repost always reverses the existing entry via a contra-entry first, same as every cancel
/// path, so both the original and the reversal stay individually visible for audit.
///
/// Lifecycle: Draft (vouchers being picked) -&gt; Queued (Submit) -&gt; InProgress (background job) -&gt;
/// Completed/PartiallyReposted/Failed (derived from per-voucher outcomes). Cancellable from
/// Draft/Queued only — not while InProgress (a running job owns the vouchers) or once finished.
/// </summary>
public class RepostAccountingLedger : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>Max vouchers per repost — mirrors ERPNext's MAX_VOUCHERS_PER_REPOST (a batch has to
    /// finish well within one background job's timeout).</summary>
    public const int MaxVouchersPerRepost = 50;

    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; private set; }

    public RepostAccountingLedgerStatus Status { get; private set; } = RepostAccountingLedgerStatus.Draft;
    public string? ErrorLog { get; private set; }

    private readonly List<RepostAccountingLedgerVoucher> _vouchers = new();
    public IReadOnlyList<RepostAccountingLedgerVoucher> Vouchers => _vouchers.AsReadOnly();

    protected RepostAccountingLedger() { }

    public RepostAccountingLedger(Guid id, Guid companyId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        TenantId = tenantId;
    }

    /// <summary>Replaces the voucher set. Only allowed while Draft — mirrors InvoiceDiscounting's
    /// editable-grid-until-submit behavior.</summary>
    public void SetVouchers(IEnumerable<RepostAccountingLedgerVoucher> vouchers)
    {
        if (Status != RepostAccountingLedgerStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        var list = vouchers.ToList();

        if (list.Count > MaxVouchersPerRepost)
            throw new BusinessException(MyERPDomainErrorCodes.RepostTooManyVouchers)
                .WithData("maxVouchers", MaxVouchersPerRepost);

        var duplicate = list
            .GroupBy(v => (v.VoucherType, v.VoucherId))
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
            throw new BusinessException(MyERPDomainErrorCodes.RepostDuplicateVoucher)
                .WithData("voucherType", duplicate.Key.VoucherType)
                .WithData("voucherNo", duplicate.Key.VoucherId);

        _vouchers.Clear();
        _vouchers.AddRange(list);
    }

    /// <summary>Draft -&gt; Queued. Hands the batch to the background job.</summary>
    public void Submit()
    {
        if (Status != RepostAccountingLedgerStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (_vouchers.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.RepostNoVouchersSelected);

        Status = RepostAccountingLedgerStatus.Queued;
    }

    /// <summary>Queued/Failed/PartiallyReposted -&gt; InProgress. Restart-safe: a stuck/retried job can
    /// call this again on anything except a finished (Completed) or Cancelled document — matches
    /// ERPNext's own start_repost guard.</summary>
    public void StartProcessing()
    {
        if (Status is RepostAccountingLedgerStatus.Completed or RepostAccountingLedgerStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.RepostInvalidStatusForStart)
                .WithData("status", Status.ToString());

        Status = RepostAccountingLedgerStatus.InProgress;
    }

    /// <summary>Looks up one voucher by (type, id) — vouchers not touched by a partial retry keep
    /// their prior outcome, mirroring ERPNext's "a retry leaves handled vouchers alone" behavior.</summary>
    public RepostAccountingLedgerVoucher? FindVoucher(string voucherType, Guid voucherId) =>
        _vouchers.FirstOrDefault(v => v.VoucherType == voucherType && v.VoucherId == voucherId);

    /// <summary>Derives the aggregate status from each voucher's outcome once a processing pass ends —
    /// mirrors ERPNext's _derive_status(). Call after the background job finishes its pass.</summary>
    public void Finish()
    {
        var handled = _vouchers.Count(v =>
            v.Status is RepostVoucherStatus.Reposted or RepostVoucherStatus.Skipped);

        Status = handled == _vouchers.Count
            ? RepostAccountingLedgerStatus.Completed
            : handled == 0
                ? RepostAccountingLedgerStatus.Failed
                : RepostAccountingLedgerStatus.PartiallyReposted;
    }

    public void RecordFailure(string error)
    {
        ErrorLog = error;
        Status = RepostAccountingLedgerStatus.Failed;
    }

    /// <summary>Draft/Queued only — a running job owns the vouchers once InProgress, and a finished
    /// document (Completed/Cancelled) can't be re-cancelled.</summary>
    public void Cancel()
    {
        if (Status == RepostAccountingLedgerStatus.InProgress)
            throw new BusinessException(MyERPDomainErrorCodes.RepostAccountingLedgerAlreadyInProgress);

        if (Status is RepostAccountingLedgerStatus.Completed or RepostAccountingLedgerStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = RepostAccountingLedgerStatus.Cancelled;
    }
}

/// <summary>One voucher queued for GL repost, and the outcome of processing it.</summary>
public class RepostAccountingLedgerVoucher : Entity<Guid>
{
    public Guid RepostAccountingLedgerId { get; set; }
    public string VoucherType { get; private set; } = null!;
    public Guid VoucherId { get; private set; }
    public string VoucherNumber { get; private set; } = null!;
    public RepostVoucherStatus Status { get; private set; } = RepostVoucherStatus.Pending;
    public string? ErrorMessage { get; private set; }

    protected RepostAccountingLedgerVoucher() { }

    public RepostAccountingLedgerVoucher(Guid id, Guid repostAccountingLedgerId, string voucherType, Guid voucherId, string voucherNumber)
        : base(id)
    {
        RepostAccountingLedgerId = repostAccountingLedgerId;
        VoucherType = voucherType;
        VoucherId = voucherId;
        VoucherNumber = voucherNumber;
    }

    public void MarkReposted()
    {
        Status = RepostVoucherStatus.Reposted;
        ErrorMessage = null;
    }

    public void MarkFailed(string error)
    {
        Status = RepostVoucherStatus.Failed;
        ErrorMessage = error;
    }

    public void MarkSkipped(string reason)
    {
        Status = RepostVoucherStatus.Skipped;
        ErrorMessage = reason;
    }
}

public enum RepostAccountingLedgerStatus
{
    Draft = 0,
    Queued = 1,
    InProgress = 2,
    PartiallyReposted = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6
}

public enum RepostVoucherStatus
{
    Pending = 0,
    Reposted = 1,
    Failed = 2,
    Skipped = 3
}
