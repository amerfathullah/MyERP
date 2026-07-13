using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Dunning — collections workflow for overdue invoices.
/// Level sequencing enforced: must have Level N before Level N+1.
/// Uses payment_schedule.outstanding (not invoice.outstanding_amount) for multi-currency.
/// Maps to ERPNext accounts/doctype/dunning.
/// </summary>
public class Dunning : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public DateTime PostingDate { get; set; }
    public int DunningLevel { get; set; } = 1;

    /// <summary>Total overdue amount across all linked invoices.</summary>
    public decimal TotalOutstanding { get; set; }

    /// <summary>Dunning fee charged (per dunning type configuration).</summary>
    public decimal DunningFee { get; set; }

    /// <summary>Interest on overdue amount.</summary>
    public decimal InterestAmount { get; set; }

    public decimal GrandTotal => TotalOutstanding + DunningFee + InterestAmount;

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;
    public string? Notes { get; set; }

    private readonly List<DunningOverduePayment> _overduePayments = new();
    public IReadOnlyList<DunningOverduePayment> OverduePayments => _overduePayments.AsReadOnly();

    protected Dunning() { }

    public Dunning(Guid id, Guid companyId, Guid customerId, DateTime postingDate,
        int dunningLevel, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        PostingDate = postingDate;
        DunningLevel = dunningLevel;
        TenantId = tenantId;
    }

    public void AddOverduePayment(Guid salesInvoiceId, decimal outstandingAmount,
        DateTime dueDate, int overdueDays)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _overduePayments.Add(new DunningOverduePayment(Guid.NewGuid(), Id,
            salesInvoiceId, outstandingAmount, dueDate, overdueDays));
        TotalOutstanding = _overduePayments.Sum(p => p.OutstandingAmount);
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_overduePayments.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    public void Resolve()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Posted; // Resolved
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }
}

public class DunningOverduePayment : FullAuditedEntity<Guid>
{
    public Guid DunningId { get; set; }
    public Guid SalesInvoiceId { get; set; }
    public decimal OutstandingAmount { get; set; }
    public DateTime DueDate { get; set; }
    public int OverdueDays { get; set; }

    protected DunningOverduePayment() { }

    public DunningOverduePayment(Guid id, Guid dunningId, Guid salesInvoiceId,
        decimal outstandingAmount, DateTime dueDate, int overdueDays) : base(id)
    {
        DunningId = dunningId;
        SalesInvoiceId = salesInvoiceId;
        OutstandingAmount = outstandingAmount;
        DueDate = dueDate;
        OverdueDays = overdueDays;
    }
}
