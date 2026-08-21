using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Subscription — recurring invoice/order generation.
/// Supports trial periods, catch-up billing for missed periods, proration.
/// Maps to ERPNext accounts/doctype/subscription.
/// </summary>
public class Subscription : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = "Customer";
    public string? PartyName { get; set; }

    public string? SubscriptionNumber { get; set; }

    /// <summary>The document template to generate (Sales Invoice, Sales Order).</summary>
    public string GenerateDocumentType { get; set; } = "SalesInvoice";

    /// <summary>Billing interval: Monthly, Quarterly, Half-Yearly, Yearly.</summary>
    public string BillingInterval { get; set; } = "Monthly";
    public int BillingIntervalCount { get; set; } = 1;

    /// <summary>Whether billing aligns strictly to calendar months (gotcha #545).</summary>
    public bool FollowCalendarMonths { get; set; }

    /// <summary>Whether subscription is prepaid (full invoice upfront) or postpaid (prorated) (gotcha #320).</summary>
    public bool IsPrepaid { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? CurrentInvoiceStart { get; set; }
    public DateTime? CurrentInvoiceEnd { get; set; }

    /// <summary>Days past due before cancellation.</summary>
    public int DaysUntilDue { get; set; } = 0;
    public int CancelAfterGraceDays { get; set; } = 0;

    /// <summary>Trial period (100% discount during trial).</summary>
    public int TrialPeriodDays { get; set; } = 0;
    public DateTime? TrialEndDate { get; set; }

    public SubscriptionStatus Status { get; private set; } = SubscriptionStatus.Active;
    public decimal TotalPerInterval { get; set; }

    /// <summary>Per PR #57615: auto-filled from plan item's company default cost center.</summary>
    public Guid? CostCenterId { get; set; }

    private readonly List<SubscriptionPlan> _plans = new();
    public IReadOnlyList<SubscriptionPlan> Plans => _plans.AsReadOnly();

    /// <summary>
    /// Validates subscription end date and calendar alignment rules per ERPNext subscription controller (gotchas #544, #545).
    /// </summary>
    public void ValidateSubscriptionPeriod()
    {
        var months = BillingInterval switch
        {
            "Monthly" => 1 * BillingIntervalCount,
            "Quarterly" => 3 * BillingIntervalCount,
            "Half-Yearly" => 6 * BillingIntervalCount,
            "Yearly" => 12 * BillingIntervalCount,
            _ => 1
        };

        if (EndDate.HasValue && EndDate.Value < StartDate.AddMonths(months))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Subscription End Date must exceed at least one full billing cycle.");
        }

        if (FollowCalendarMonths)
        {
            if (!EndDate.HasValue)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "End Date is mandatory when Follow Calendar Months is enabled.");
            }

            if (!string.Equals(BillingInterval, "Monthly", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Billing Interval must be Monthly when Follow Calendar Months is enabled.");
            }
        }
    }

    protected Subscription() { }

    public Subscription(Guid id, Guid companyId, Guid partyId, string partyType,
        DateTime startDate, string billingInterval, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        PartyId = partyId;
        PartyType = partyType;
        StartDate = startDate;
        BillingInterval = billingInterval;
        TenantId = tenantId;
    }

    public void AddPlan(Guid itemId, decimal qty, decimal rate, string? itemName = null, Guid? costCenterId = null)
    {
        var plan = new SubscriptionPlan(Guid.NewGuid(), Id, itemId, qty, rate, itemName);
        plan.CostCenterId = costCenterId;
        _plans.Add(plan);
        TotalPerInterval = _plans.Sum(p => p.Amount);
        // Per PR #57615: fill-empty-only — first plan with a cost center sets subscription-level CC
        if (CostCenterId == null && costCenterId != null)
            CostCenterId = costCenterId;
    }

    public void Cancel()
    {
        if (Status == SubscriptionStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = SubscriptionStatus.Cancelled;
    }

    public void Pause()
    {
        if (Status != SubscriptionStatus.Active)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = SubscriptionStatus.PastDueDate;
    }

    public void Reactivate()
    {
        if (Status is not (SubscriptionStatus.PastDueDate or SubscriptionStatus.Unpaid))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = SubscriptionStatus.Active;
    }

    /// <summary>Advance the billing period to next interval.</summary>
    public void AdvancePeriod()
    {
        var months = BillingInterval switch
        {
            "Monthly" => 1 * BillingIntervalCount,
            "Quarterly" => 3 * BillingIntervalCount,
            "Half-Yearly" => 6 * BillingIntervalCount,
            "Yearly" => 12 * BillingIntervalCount,
            _ => 1
        };
        CurrentInvoiceStart = CurrentInvoiceEnd?.AddDays(1) ?? StartDate;
        CurrentInvoiceEnd = CurrentInvoiceStart.Value.AddMonths(months).AddDays(-1);
    }
}

public enum SubscriptionStatus
{
    Active = 0,
    PastDueDate = 1,
    Unpaid = 2,
    Cancelled = 3,
    Completed = 4
}

public class SubscriptionPlan : FullAuditedEntity<Guid>
{
    public Guid SubscriptionId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount => Qty * Rate;

    /// <summary>Per PR #57615: cost center from plan item defaults (selling/buying by party type).</summary>
    public Guid? CostCenterId { get; set; }

    protected SubscriptionPlan() { }

    public SubscriptionPlan(Guid id, Guid subscriptionId, Guid itemId,
        decimal qty, decimal rate, string? itemName) : base(id)
    {
        SubscriptionId = subscriptionId;
        ItemId = itemId;
        Qty = qty;
        Rate = rate;
        ItemName = itemName;
    }
}
