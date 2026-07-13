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

    private readonly List<SubscriptionPlan> _plans = new();
    public IReadOnlyList<SubscriptionPlan> Plans => _plans.AsReadOnly();

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

    public void AddPlan(Guid itemId, decimal qty, decimal rate, string? itemName = null)
    {
        _plans.Add(new SubscriptionPlan(Guid.NewGuid(), Id, itemId, qty, rate, itemName));
        TotalPerInterval = _plans.Sum(p => p.Amount);
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
