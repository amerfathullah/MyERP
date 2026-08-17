using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Accounting.Entities;

/// <summary>Percentage allocation for a single month within a Monthly Distribution.</summary>
public class MonthlyDistributionPercentage : FullAuditedEntity<Guid>
{
    public Guid MonthlyDistributionId { get; set; }

    /// <summary>1 = January .. 12 = December.</summary>
    public int Month { get; set; }

    public decimal PercentageAllocation { get; set; }

    protected MonthlyDistributionPercentage() { }

    public MonthlyDistributionPercentage(Guid id, Guid monthlyDistributionId, int month, decimal percentageAllocation)
        : base(id)
    {
        MonthlyDistributionId = monthlyDistributionId;
        Month = month;
        PercentageAllocation = percentageAllocation;
    }
}
