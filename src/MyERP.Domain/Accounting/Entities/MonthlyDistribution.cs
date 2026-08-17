using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Monthly Distribution — spreads a yearly Budget/target across the 12 months to model
/// business seasonality. Consumed by Budget's accumulated-monthly enforcement and by
/// sales target variance reporting. Maps to ERPNext accounts/doctype/monthly_distribution.
/// </summary>
public class MonthlyDistribution : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string DistributionName { get; set; } = null!;
    public Guid? FiscalYearId { get; set; }

    private readonly List<MonthlyDistributionPercentage> _percentages = new();
    public IReadOnlyList<MonthlyDistributionPercentage> Percentages => _percentages.AsReadOnly();

    protected MonthlyDistribution() { }

    public MonthlyDistribution(Guid id, string distributionName, Guid? fiscalYearId = null, Guid? tenantId = null)
        : base(id)
    {
        DistributionName = Check.NotNullOrWhiteSpace(distributionName, nameof(distributionName), 140);
        FiscalYearId = fiscalYearId;
        TenantId = tenantId;
    }

    /// <summary>Resets the 12 rows to an even 100/12 split, per ERPNext's get_months().</summary>
    public void SetEvenSplit()
    {
        _percentages.Clear();
        for (var month = 1; month <= 12; month++)
            _percentages.Add(new MonthlyDistributionPercentage(Guid.NewGuid(), Id, month, 100m / 12));
    }

    public void SetPercentages(IEnumerable<(int Month, decimal PercentageAllocation)> rows)
    {
        _percentages.Clear();
        foreach (var (month, percentage) in rows)
            _percentages.Add(new MonthlyDistributionPercentage(Guid.NewGuid(), Id, month, percentage));
        Validate();
    }

    /// <summary>Per ERPNext MonthlyDistribution.validate — total allocation must equal 100%.</summary>
    public void Validate()
    {
        var total = _percentages.Sum(p => p.PercentageAllocation);
        if (Math.Round(total, 2) != 100m)
            throw new BusinessException(MyERPDomainErrorCodes.MonthlyDistributionMustTotal100)
                .WithData("total", Math.Round(total, 2));
    }

    /// <summary>
    /// Percentage of the yearly amount allocated to a run of months starting at <paramref name="startMonth"/>.
    /// Per ERPNext get_percentage(): sums the rows whose month falls within the period, wrapping year-end.
    /// </summary>
    public decimal GetPercentageForPeriod(int startMonth, int monthsInPeriod)
    {
        var months = Enumerable.Range(0, monthsInPeriod)
            .Select(offset => ((startMonth - 1 + offset) % 12) + 1)
            .ToHashSet();
        return _percentages.Where(p => months.Contains(p.Month)).Sum(p => p.PercentageAllocation);
    }
}
