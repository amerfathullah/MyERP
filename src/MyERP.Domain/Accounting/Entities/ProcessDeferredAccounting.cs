using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Process Deferred Accounting — converts deferred revenue to income (Sales) or deferred expense to expense (Purchases).
/// Maps to ERPNext accounts/doctype/process_deferred_accounting.
/// </summary>
public class ProcessDeferredAccounting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string ProcessNumber { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public DeferredAccountingType Type { get; set; }
    public Guid? AccountId { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsSubmitted { get; set; }
    public bool IsCancelled { get; set; }
    public int EntriesProcessed { get; set; }

    protected ProcessDeferredAccounting() { }

    public ProcessDeferredAccounting(
        Guid id,
        string processNumber,
        Guid companyId,
        DeferredAccountingType type,
        DateTime postingDate,
        DateTime startDate,
        DateTime endDate,
        Guid? accountId = null,
        Guid? tenantId = null)
        : base(id)
    {
        ProcessNumber = Check.NotNullOrWhiteSpace(processNumber, nameof(processNumber), ProcessDeferredAccountingConsts.MaxProcessNumberLength);
        CompanyId = companyId;
        Type = type;
        PostingDate = postingDate.Date;
        StartDate = startDate.Date;
        EndDate = endDate.Date;
        AccountId = accountId;
        TenantId = tenantId;

        ValidateDates();
    }

    public void ValidateDates()
    {
        if (EndDate < StartDate)
        {
            throw new BusinessException("MyERP:ProcessDeferredAccounting:EndDateBeforeStartDate", "End date cannot be before start date.");
        }
    }

    public void Submit(int entriesCount)
    {
        ValidateDates();
        if (IsSubmitted)
        {
            throw new BusinessException("MyERP:ProcessDeferredAccounting:AlreadySubmitted", "Process Deferred Accounting is already submitted.");
        }

        IsSubmitted = true;
        EntriesProcessed = entriesCount;
    }

    public void Cancel()
    {
        if (!IsSubmitted)
        {
            throw new BusinessException("MyERP:ProcessDeferredAccounting:CannotCancelDraft", "Cannot cancel an unsubmitted Process Deferred Accounting.");
        }
        if (IsCancelled)
        {
            throw new BusinessException("MyERP:ProcessDeferredAccounting:AlreadyCancelled", "Process Deferred Accounting is already cancelled.");
        }

        IsCancelled = true;
    }
}
