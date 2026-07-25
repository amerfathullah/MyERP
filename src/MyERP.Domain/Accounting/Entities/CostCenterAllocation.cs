using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Cost Center Allocation — distributes GL postings from a main cost center
/// to child cost centers based on configured percentages.
/// 
/// Per ERPNext:
/// - Percentages must sum to exactly 100%
/// - DAG validation: main CC cannot be child elsewhere, child CC cannot be main elsewhere
/// - valid_from date must be >= last GL posting date for the main cost center
/// - Only 4 GL fields distributed: debit, credit, debit_in_account_currency, credit_in_account_currency
/// - Round-off from allocation goes to FIRST sub-cost-center
/// </summary>
public class CostCenterAllocation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Main cost center whose postings will be distributed.</summary>
    public Guid MainCostCenterId { get; set; }

    /// <summary>Date from which this allocation becomes effective.</summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>Whether this allocation is currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Allocation entries defining distribution to child cost centers.</summary>
    public ICollection<CostCenterAllocationEntry> Entries { get; private set; } = new List<CostCenterAllocationEntry>();

    protected CostCenterAllocation() { }

    public CostCenterAllocation(
        Guid id,
        Guid companyId,
        Guid mainCostCenterId,
        DateTime validFrom,
        Guid? tenantId = null) : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        MainCostCenterId = Check.NotDefaultOrNull<Guid>(mainCostCenterId, nameof(mainCostCenterId));
        ValidFrom = validFrom;
        TenantId = tenantId;
    }

    /// <summary>
    /// Adds an allocation entry. Main CC cannot equal child CC.
    /// </summary>
    public void AddEntry(Guid childCostCenterId, decimal percentage)
    {
        if (childCostCenterId == MainCostCenterId)
            throw new BusinessException("MyERP:02038")
                .WithData("mainCostCenter", MainCostCenterId.ToString());

        if (percentage <= 0 || percentage > 100)
            throw new BusinessException("MyERP:02039")
                .WithData("percentage", percentage.ToString());

        if (Entries.Any(e => e.ChildCostCenterId == childCostCenterId))
            throw new BusinessException("MyERP:02040")
                .WithData("childCostCenter", childCostCenterId.ToString());

        Entries.Add(new CostCenterAllocationEntry(Guid.NewGuid(), Id, childCostCenterId, percentage, TenantId));
    }

    /// <summary>
    /// Validates that all entries sum to exactly 100%.
    /// </summary>
    public void ValidatePercentages()
    {
        if (!Entries.Any())
            throw new BusinessException("MyERP:02041");

        var total = Entries.Sum(e => e.Percentage);
        if (Math.Abs(total - 100m) > 0.001m)
            throw new BusinessException("MyERP:02042")
                .WithData("total", total.ToString("F2"));
    }

    /// <summary>
    /// Distributes a GL amount across child cost centers based on allocation percentages.
    /// Round-off remainder goes to the FIRST entry.
    /// </summary>
    public List<(Guid CostCenterId, decimal Amount)> Distribute(decimal amount)
    {
        if (!Entries.Any()) return new List<(Guid, decimal)>();

        var results = new List<(Guid CostCenterId, decimal Amount)>();
        var distributed = 0m;
        var orderedEntries = Entries.OrderBy(e => e.ChildCostCenterId).ToList();

        for (int i = 0; i < orderedEntries.Count; i++)
        {
            var entry = orderedEntries[i];
            var entryAmount = Math.Round(amount * entry.Percentage / 100m, 4);
            distributed += entryAmount;
            results.Add((entry.ChildCostCenterId, entryAmount));
        }

        // Round-off remainder goes to FIRST entry (per ERPNext)
        var remainder = amount - distributed;
        if (remainder != 0 && results.Count > 0)
        {
            results[0] = (results[0].CostCenterId, results[0].Amount + remainder);
        }

        return results;
    }
}

/// <summary>
/// Individual allocation entry — maps a percentage to a child cost center.
/// </summary>
public class CostCenterAllocationEntry : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CostCenterAllocationId { get; set; }

    /// <summary>Child cost center receiving the allocated portion.</summary>
    public Guid ChildCostCenterId { get; set; }

    /// <summary>Percentage of the main cost center's postings to allocate here (0-100).</summary>
    public decimal Percentage { get; set; }

    protected CostCenterAllocationEntry() { }

    public CostCenterAllocationEntry(
        Guid id,
        Guid allocationId,
        Guid childCostCenterId,
        decimal percentage,
        Guid? tenantId = null) : base(id)
    {
        CostCenterAllocationId = allocationId;
        ChildCostCenterId = childCostCenterId;
        Percentage = percentage;
        TenantId = tenantId;
    }
}
