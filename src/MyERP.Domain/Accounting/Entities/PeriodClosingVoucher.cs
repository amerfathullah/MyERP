using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Period Closing Voucher — closes a fiscal period by posting reversing entries
/// for all P&L accounts to the Closing Account (retained earnings).
/// Creates per-P&L-account per-dimension-group GL entries (not a single aggregated entry).
/// Maps to ERPNext accounts/doctype/period_closing_voucher.
/// </summary>
public class PeriodClosingVoucher : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }

    public string? VoucherNumber { get; set; }
    public DateTime PostingDate { get; set; }

    /// <summary>The retained earnings / closing account to transfer P&L balance to.</summary>
    public Guid ClosingAccountId { get; set; }

    /// <summary>Closing up to this date (all P&L entries up to this date are reversed).</summary>
    public DateTime TransactionDate { get; set; }

    public string? Remarks { get; set; }
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    /// <summary>Total P&L balance transferred (sum of absolute values).</summary>
    public decimal TotalClosingAmount { get; private set; }

    private readonly List<PeriodClosingEntry> _entries = new();
    public IReadOnlyList<PeriodClosingEntry> Entries => _entries.AsReadOnly();

    protected PeriodClosingVoucher() { }

    public PeriodClosingVoucher(Guid id, Guid companyId, Guid fiscalYearId,
        DateTime postingDate, DateTime transactionDate, Guid closingAccountId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        FiscalYearId = fiscalYearId;
        PostingDate = postingDate;
        TransactionDate = transactionDate;
        ClosingAccountId = closingAccountId;
        TenantId = tenantId;
    }

    /// <summary>
    /// Adds a closing entry for a specific P&L account + dimension combination.
    /// Each unique (account, cost_center, dimension) gets its own reversing entry.
    /// </summary>
    public void AddEntry(Guid accountId, Guid? costCenterId, decimal amount, bool isDebit)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _entries.Add(new PeriodClosingEntry(Guid.NewGuid(), Id, accountId, costCenterId, amount, isDebit));
        TotalClosingAmount = _entries.Sum(e => Math.Abs(e.Amount));
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_entries.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }
}

/// <summary>Individual closing entry line per P&L account + dimension.</summary>
public class PeriodClosingEntry : FullAuditedEntity<Guid>
{
    public Guid PeriodClosingVoucherId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public decimal Amount { get; set; }
    public bool IsDebit { get; set; }

    protected PeriodClosingEntry() { }

    public PeriodClosingEntry(Guid id, Guid pcvId, Guid accountId,
        Guid? costCenterId, decimal amount, bool isDebit) : base(id)
    {
        PeriodClosingVoucherId = pcvId;
        AccountId = accountId;
        CostCenterId = costCenterId;
        Amount = amount;
        IsDebit = isDebit;
    }
}
