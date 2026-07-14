using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Supplier Scorecard — rates suppliers based on configurable weighted criteria.
/// Determines standing (and enforcement flags) from calculated score.
/// 
/// Per ERPNext:
/// - Standings must cover 0-100 continuously (no gaps/overlaps)
/// - Criteria weights must sum to 100%
/// - Score of 100 = benefit of doubt (no periods evaluated yet)
/// - Standing determines PO/RFQ enforcement (block/warn)
/// 
/// Source: erpnext/buying/doctype/supplier_scorecard/supplier_scorecard.py
/// </summary>
public class SupplierScorecard : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Period type for auto-generation.</summary>
    public ScorecardPeriodType PeriodType { get; set; } = ScorecardPeriodType.Monthly;

    /// <summary>Formula to weight older periods less (e.g., "max(0, 1 - 0.1 * period_number)").</summary>
    public string? WeightingFunction { get; set; }

    /// <summary>Current overall score (0-100). 100 = perfect or no data.</summary>
    public decimal Score { get; set; } = 100m;

    /// <summary>Current standing name (determined from score bands).</summary>
    public string? CurrentStanding { get; set; }

    /// <summary>Score bands that determine the standing from the score.</summary>
    public ICollection<ScorecardStanding> Standings { get; private set; }
        = new List<ScorecardStanding>();

    /// <summary>Scoring criteria with weights and formulas.</summary>
    public ICollection<ScorecardCriterion> Criteria { get; private set; }
        = new List<ScorecardCriterion>();

    protected SupplierScorecard() { }

    public SupplierScorecard(
        Guid id,
        Guid supplierId,
        Guid companyId,
        ScorecardPeriodType periodType = ScorecardPeriodType.Monthly,
        Guid? tenantId = null) : base(id)
    {
        SupplierId = supplierId;
        CompanyId = companyId;
        PeriodType = periodType;
        TenantId = tenantId;
    }

    /// <summary>
    /// Add a standing band (e.g., "Poor" for 0-30, "Good" for 30-70, "Excellent" for 70-100).
    /// </summary>
    public void AddStanding(string name, decimal minGrade, decimal maxGrade,
        bool preventPos = false, bool preventRfqs = false,
        bool warnPos = false, bool warnRfqs = false)
    {
        Standings.Add(new ScorecardStanding(Guid.NewGuid(), Id, name,
            minGrade, maxGrade, preventPos, preventRfqs, warnPos, warnRfqs));
    }

    /// <summary>
    /// Add a scoring criterion with a weight and formula.
    /// </summary>
    public void AddCriterion(string name, decimal weight, decimal maxScore, string? formula = null)
    {
        Criteria.Add(new ScorecardCriterion(Guid.NewGuid(), Id, name, weight, maxScore, formula));
    }

    /// <summary>
    /// Validate the scorecard configuration.
    /// </summary>
    public void Validate()
    {
        // Criteria weights must sum to 100%
        var totalWeight = Criteria.Sum(c => c.Weight);
        if (Criteria.Any() && totalWeight != 100m)
        {
            throw new BusinessException("MyERP:04009")
                .WithData("totalWeight", totalWeight);
        }

        // Standings must cover 0-100 without gaps
        if (Standings.Any())
        {
            var ordered = Standings.OrderBy(s => s.MinGrade).ToList();

            // First must start at 0
            if (ordered[0].MinGrade != 0)
                throw new BusinessException("MyERP:04010")
                    .WithData("issue", "First standing must start at 0");

            // Last must end at 100
            if (ordered[^1].MaxGrade != 100)
                throw new BusinessException("MyERP:04010")
                    .WithData("issue", "Last standing must end at 100");

            // No gaps or overlaps
            for (int i = 0; i < ordered.Count - 1; i++)
            {
                if (ordered[i].MaxGrade != ordered[i + 1].MinGrade)
                {
                    throw new BusinessException("MyERP:04010")
                        .WithData("issue", $"Gap/overlap between '{ordered[i].Name}' and '{ordered[i + 1].Name}'");
                }
            }

            // Each band must have min < max
            foreach (var s in ordered)
            {
                if (s.MinGrade >= s.MaxGrade)
                    throw new BusinessException("MyERP:04010")
                        .WithData("issue", $"Standing '{s.Name}' has min >= max");
            }
        }
    }

    /// <summary>
    /// Update the score and determine the current standing.
    /// Returns the matching standing (for enforcement flag checks).
    /// </summary>
    public ScorecardStanding? UpdateScore(decimal newScore)
    {
        Score = Math.Clamp(newScore, 0, 100);
        var standing = Standings
            .FirstOrDefault(s => Score >= s.MinGrade && Score < s.MaxGrade);

        // Edge case: score = 100 matches the last standing (MaxGrade = 100 uses <=)
        if (standing == null && Score == 100)
            standing = Standings.OrderByDescending(s => s.MaxGrade).FirstOrDefault();

        CurrentStanding = standing?.Name;
        return standing;
    }

    /// <summary>
    /// Get the enforcement flags for the current standing.
    /// </summary>
    public (bool PreventPos, bool PreventRfqs, bool WarnPos, bool WarnRfqs) GetEnforcementFlags()
    {
        var standing = Standings.FirstOrDefault(s =>
            Score >= s.MinGrade && (Score < s.MaxGrade || (Score == 100 && s.MaxGrade == 100)));

        if (standing == null)
            return (false, false, false, false);

        return (standing.PreventPos, standing.PreventRfqs, standing.WarnPos, standing.WarnRfqs);
    }
}

/// <summary>
/// A standing/grade band within a supplier scorecard.
/// Determines enforcement actions based on score range.
/// </summary>
public class ScorecardStanding : Entity<Guid>
{
    public Guid SupplierScorecardId { get; set; }
    public string Name { get; set; } = null!;
    public decimal MinGrade { get; set; }
    public decimal MaxGrade { get; set; }

    public bool PreventPos { get; set; }
    public bool PreventRfqs { get; set; }
    public bool WarnPos { get; set; }
    public bool WarnRfqs { get; set; }

    protected ScorecardStanding() { }

    public ScorecardStanding(Guid id, Guid scorecardId, string name,
        decimal minGrade, decimal maxGrade,
        bool preventPos, bool preventRfqs, bool warnPos, bool warnRfqs) : base(id)
    {
        SupplierScorecardId = scorecardId;
        Name = name;
        MinGrade = minGrade;
        MaxGrade = maxGrade;
        PreventPos = preventPos;
        PreventRfqs = preventRfqs;
        WarnPos = warnPos;
        WarnRfqs = warnRfqs;
    }
}

/// <summary>
/// A scoring criterion within a supplier scorecard (weight + formula).
/// </summary>
public class ScorecardCriterion : Entity<Guid>
{
    public Guid SupplierScorecardId { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>Weight percentage (all criteria must sum to 100%).</summary>
    public decimal Weight { get; set; }

    /// <summary>Maximum score for this criterion (score is clamped to [0, MaxScore]).</summary>
    public decimal MaxScore { get; set; }

    /// <summary>Formula for calculating this criterion's raw score.</summary>
    public string? Formula { get; set; }

    protected ScorecardCriterion() { }

    public ScorecardCriterion(Guid id, Guid scorecardId, string name,
        decimal weight, decimal maxScore, string? formula) : base(id)
    {
        SupplierScorecardId = scorecardId;
        Name = name;
        Weight = weight;
        MaxScore = maxScore;
        Formula = formula;
    }

    /// <summary>
    /// Clamp a raw score to [0, MaxScore].
    /// </summary>
    public decimal ClampScore(decimal rawScore)
    {
        return Math.Clamp(rawScore, 0, MaxScore);
    }
}

/// <summary>
/// A scored period for a supplier (weekly/monthly/yearly evaluation).
/// </summary>
public class ScorecardPeriod : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid SupplierScorecardId { get; set; }
    public Guid SupplierId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>Total weighted score for this period (0-100).</summary>
    public decimal TotalScore { get; set; }

    /// <summary>Whether this period has been evaluated and submitted.</summary>
    public bool IsSubmitted { get; set; }

    protected ScorecardPeriod() { }

    public ScorecardPeriod(Guid id, Guid scorecardId, Guid supplierId,
        DateTime startDate, DateTime endDate, Guid? tenantId = null) : base(id)
    {
        SupplierScorecardId = scorecardId;
        SupplierId = supplierId;
        StartDate = startDate;
        EndDate = endDate;
        TenantId = tenantId;
    }

    public void Submit(decimal totalScore)
    {
        TotalScore = totalScore;
        IsSubmitted = true;
    }
}
