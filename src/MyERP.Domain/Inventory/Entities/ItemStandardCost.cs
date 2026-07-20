using System;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Item Standard Cost — defines fixed standard rate for items using Standard Cost valuation.
/// Creates auto-revaluation Stock Reconciliation on submit for all warehouses with stock.
/// Cannot be cancelled when stock activity exists on/after effective_date.
/// Maps to ERPNext stock/doctype/item_standard_cost.
/// </summary>
public class ItemStandardCost : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }

    /// <summary>Standard rate for this item (fixed cost for valuation).</summary>
    public decimal StandardRate { get; set; }

    /// <summary>Date from which this rate is effective. Must be ≤ today.</summary>
    public DateTime EffectiveDate { get; set; }

    /// <summary>Previous standard rate (for PPV calculation on change).</summary>
    public decimal? PreviousRate { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    /// <summary>Auto-created Stock Reconciliation for revaluation.</summary>
    public Guid? RevaluationStockReconciliationId { get; set; }

    protected ItemStandardCost() { }

    public ItemStandardCost(Guid id, Guid companyId, Guid itemId, decimal standardRate,
        DateTime effectiveDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        ItemId = Check.NotDefaultOrNull<Guid>(itemId, nameof(itemId));

        if (standardRate <= 0)
            throw new ArgumentException("Standard rate must be positive.", nameof(standardRate));

        StandardRate = standardRate;
        TenantId = tenantId;
        SetEffectiveDate(effectiveDate);
    }

    /// <summary>Validate and set effective date (must be ≤ today).</summary>
    private void SetEffectiveDate(DateTime date)
    {
        if (date.Date > DateTime.UtcNow.Date)
            throw new BusinessException(MyERPDomainErrorCodes.StandardCostEffectiveDateInFuture)
                .WithData("effectiveDate", date)
                .WithData("today", DateTime.UtcNow.Date);
        EffectiveDate = date.Date;
    }

    /// <summary>Validate effective date is not before last SLE for this item.</summary>
    public void ValidateAgainstLastSle(DateTime? lastSleDate)
    {
        if (lastSleDate.HasValue && EffectiveDate < lastSleDate.Value.Date)
            throw new BusinessException(MyERPDomainErrorCodes.StandardCostEffectiveDateBeforeLastSle)
                .WithData("effectiveDate", EffectiveDate)
                .WithData("lastSleDate", lastSleDate.Value.Date);
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    /// <summary>Cancel is only allowed when no stock activity exists on/after effective date.</summary>
    public void Cancel(bool hasStockActivityOnOrAfter)
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (hasStockActivityOnOrAfter)
            throw new BusinessException(MyERPDomainErrorCodes.StandardCostCannotCancel)
                .WithData("effectiveDate", EffectiveDate);
        Status = DocumentStatus.Cancelled;
    }

    /// <summary>Calculate Purchase Price Variance for a transaction.</summary>
    public decimal CalculatePpv(decimal actualRate, decimal qty)
    {
        return (actualRate - StandardRate) * qty;
    }
}
