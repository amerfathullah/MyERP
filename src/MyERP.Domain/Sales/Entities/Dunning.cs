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

    /// <summary>Dunning Type this document was configured from (fee/interest/income account defaults).</summary>
    public Guid? DunningTypeId { get; set; }

    /// <summary>Total overdue amount across all linked invoices.</summary>
    public decimal TotalOutstanding { get; set; }

    /// <summary>Dunning fee charged (per dunning type configuration).</summary>
    public decimal DunningFee { get; set; }

    /// <summary>Interest on overdue amount.</summary>
    public decimal InterestAmount { get; set; }

    public decimal GrandTotal => TotalOutstanding + DunningFee + InterestAmount;

    /// <summary>Total dunning fee and interest charges (DunningFee + InterestAmount).</summary>
    public decimal DunningAmount => DunningFee + InterestAmount;

    /// <summary>Paid dunning fee and interest amount tracked from Payment Entry deductions.</summary>
    public decimal PaidDunningAmount { get; set; }

    /// <summary>Remaining uncollected dunning fee and interest amount (ERPNext PR #58227).</summary>
    public decimal UnpaidDunningAmount => Math.Max(0, DunningAmount - PaidDunningAmount);

    /// <summary>Currency of the dunning charges and invoices (defaults to company currency).</summary>
    public string Currency { get; set; } = "MYR";

    /// <summary>Exchange rate from document currency to company base currency.</summary>
    public decimal ConversionRate { get; set; } = 1m;

    /// <summary>Total dunning fee and interest in company currency (DunningAmount × ConversionRate).</summary>
    public decimal BaseDunningAmount => Math.Round(DunningAmount * ConversionRate, 2);

    /// <summary>Income account to credit dunning fee and interest revenue.</summary>
    public Guid? IncomeAccountId { get; set; }

    /// <summary>Cost center for departmental GL reporting.</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Yearly interest rate (%) on overdue amounts.</summary>
    public decimal RateOfInterest { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;
    public string? Notes { get; set; }

    /// <summary>When the dunning notice email was sent to the customer.</summary>
    public DateTime? EmailSentAt { get; private set; }

    /// <summary>Email address(es) the dunning notice was sent to.</summary>
    public string? EmailSentTo { get; private set; }

    public void MarkEmailSent(string recipientEmail)
    {
        EmailSentAt = DateTime.UtcNow;
        EmailSentTo = recipientEmail;
    }

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
        DateTime dueDate, int overdueDays, Guid? paymentScheduleId = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (outstandingAmount <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Outstanding amount must be positive.");
        var payment = new DunningOverduePayment(Guid.NewGuid(), Id,
            salesInvoiceId, outstandingAmount, dueDate, overdueDays)
        {
            PaymentScheduleId = paymentScheduleId
        };
        _overduePayments.Add(payment);
        TotalOutstanding = _overduePayments.Sum(p => p.OutstandingAmount);
    }

    /// <summary>
    /// Calculates interest amount across overdue payments based on yearly rate of interest (gotcha #2713).
    /// Formula per item: outstanding × (rateOfInterest / 100 / 365) × overdueDays.
    /// </summary>
    public void CalculateInterest(decimal rateOfInterest)
    {
        if (rateOfInterest <= 0)
        {
            InterestAmount = 0;
            return;
        }

        InterestAmount = Math.Round(_overduePayments.Sum(p =>
            p.OutstandingAmount * (rateOfInterest / 100m / 365m) * p.OverdueDays), 2);
    }

    public void ValidateIncomeAccount()
    {
        // Per ERPNext PR #61620 (commit 308143fc67): income account is required to collect dunning fees or interest
        if (DunningAmount > 0 && (!IncomeAccountId.HasValue || IncomeAccountId.Value == Guid.Empty))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Income Account is required to collect dunning fees or interest.");
        }
    }

    public void Submit(bool validateIncomeAccount = false)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_overduePayments.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Per ERPNext PR #61620 (commit 0850b876a3): conversion rate must be greater than zero
        if (ConversionRate <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Conversion Rate must be greater than zero.");
        }

        if (validateIncomeAccount)
        {
            ValidateIncomeAccount();
        }

        Status = DocumentStatus.Submitted;
    }

    public void Resolve()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Posted; // Resolved
    }

    public void Reopen()
    {
        if (Status != DocumentStatus.Posted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    public void RecordDunningPayment(decimal amount)
    {
        PaidDunningAmount = Math.Max(0, PaidDunningAmount + amount);
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        // Per ERPNext PR #61620 (commit 58e9c6f8a8): require payment cancellation before dunning cancellation
        if (PaidDunningAmount > 0)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Cannot cancel Dunning with paid fees or interest. Cancel the linked Payment Entry first.");
        Status = DocumentStatus.Cancelled;
    }
}

public class DunningOverduePayment : FullAuditedEntity<Guid>
{
    public Guid DunningId { get; set; }
    public Guid SalesInvoiceId { get; set; }
    public Guid? PaymentScheduleId { get; set; }
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
