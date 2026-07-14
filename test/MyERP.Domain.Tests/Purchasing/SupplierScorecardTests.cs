using System;
using System.Linq;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Purchasing;

public class SupplierScorecardTests
{
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

    private SupplierScorecard CreateScorecard()
    {
        var sc = new SupplierScorecard(Guid.NewGuid(), _supplierId, _companyId);
        sc.AddStanding("Poor", 0, 30, preventPos: true, preventRfqs: true);
        sc.AddStanding("Average", 30, 60, warnPos: true);
        sc.AddStanding("Good", 60, 80);
        sc.AddStanding("Excellent", 80, 100);
        return sc;
    }

    [Fact]
    public void Scorecard_DefaultState()
    {
        var sc = new SupplierScorecard(Guid.NewGuid(), _supplierId, _companyId);
        Assert.Equal(100m, sc.Score); // Benefit of the doubt
        Assert.Null(sc.CurrentStanding);
        Assert.Equal(ScorecardPeriodType.Monthly, sc.PeriodType);
        Assert.Empty(sc.Standings);
        Assert.Empty(sc.Criteria);
    }

    [Fact]
    public void Scorecard_UpdateScore_DeterminesStanding()
    {
        var sc = CreateScorecard();
        var standing = sc.UpdateScore(75m);

        Assert.Equal(75m, sc.Score);
        Assert.Equal("Good", sc.CurrentStanding);
        Assert.NotNull(standing);
        Assert.Equal("Good", standing!.Name);
    }

    [Fact]
    public void Scorecard_UpdateScore_PoorStanding()
    {
        var sc = CreateScorecard();
        sc.UpdateScore(15m);
        Assert.Equal("Poor", sc.CurrentStanding);
    }

    [Fact]
    public void Scorecard_UpdateScore_ExcellentStanding()
    {
        var sc = CreateScorecard();
        sc.UpdateScore(90m);
        Assert.Equal("Excellent", sc.CurrentStanding);
    }

    [Fact]
    public void Scorecard_UpdateScore_PerfectScore_100()
    {
        var sc = CreateScorecard();
        sc.UpdateScore(100m);
        Assert.Equal("Excellent", sc.CurrentStanding); // 100 matches last tier
    }

    [Fact]
    public void Scorecard_UpdateScore_BoundaryValue()
    {
        var sc = CreateScorecard();
        sc.UpdateScore(30m); // Exactly at Average threshold
        Assert.Equal("Average", sc.CurrentStanding);
    }

    [Fact]
    public void Scorecard_UpdateScore_ClampsTo0_100()
    {
        var sc = CreateScorecard();
        sc.UpdateScore(150m);
        Assert.Equal(100m, sc.Score);

        sc.UpdateScore(-10m);
        Assert.Equal(0m, sc.Score);
    }

    [Fact]
    public void Scorecard_EnforcementFlags_PoorBlocks()
    {
        var sc = CreateScorecard();
        sc.UpdateScore(20m); // Poor standing

        var flags = sc.GetEnforcementFlags();
        Assert.True(flags.PreventPos);
        Assert.True(flags.PreventRfqs);
        Assert.False(flags.WarnPos);
        Assert.False(flags.WarnRfqs);
    }

    [Fact]
    public void Scorecard_EnforcementFlags_AverageWarns()
    {
        var sc = CreateScorecard();
        sc.UpdateScore(45m); // Average standing

        var flags = sc.GetEnforcementFlags();
        Assert.False(flags.PreventPos);
        Assert.False(flags.PreventRfqs);
        Assert.True(flags.WarnPos);
        Assert.False(flags.WarnRfqs);
    }

    [Fact]
    public void Scorecard_EnforcementFlags_GoodNoRestrictions()
    {
        var sc = CreateScorecard();
        sc.UpdateScore(70m); // Good standing

        var flags = sc.GetEnforcementFlags();
        Assert.False(flags.PreventPos);
        Assert.False(flags.PreventRfqs);
        Assert.False(flags.WarnPos);
        Assert.False(flags.WarnRfqs);
    }

    [Fact]
    public void Scorecard_Validate_Valid()
    {
        var sc = CreateScorecard();
        sc.AddCriterion("Delivery Timeliness", 60m, 100m);
        sc.AddCriterion("Quality Score", 40m, 100m);
        sc.Validate(); // Should not throw
    }

    [Fact]
    public void Scorecard_Validate_WeightsNot100_Throws()
    {
        var sc = CreateScorecard();
        sc.AddCriterion("Delivery", 60m, 100m);
        sc.AddCriterion("Quality", 30m, 100m); // Total = 90, not 100

        var ex = Assert.Throws<BusinessException>(() => sc.Validate());
        Assert.Equal("MyERP:04009", ex.Code);
    }

    [Fact]
    public void Scorecard_Validate_StandingsGap_Throws()
    {
        var sc = new SupplierScorecard(Guid.NewGuid(), _supplierId, _companyId);
        sc.AddStanding("Poor", 0, 30);
        sc.AddStanding("Good", 50, 100); // Gap: 30-50 missing

        var ex = Assert.Throws<BusinessException>(() => sc.Validate());
        Assert.Equal("MyERP:04010", ex.Code);
    }

    [Fact]
    public void Scorecard_Validate_StandingsNotStartAt0_Throws()
    {
        var sc = new SupplierScorecard(Guid.NewGuid(), _supplierId, _companyId);
        sc.AddStanding("Average", 20, 60); // Doesn't start at 0
        sc.AddStanding("Good", 60, 100);

        var ex = Assert.Throws<BusinessException>(() => sc.Validate());
        Assert.Equal("MyERP:04010", ex.Code);
    }

    [Fact]
    public void Scorecard_Validate_StandingsNotEndAt100_Throws()
    {
        var sc = new SupplierScorecard(Guid.NewGuid(), _supplierId, _companyId);
        sc.AddStanding("Poor", 0, 50);
        sc.AddStanding("Good", 50, 80); // Doesn't end at 100

        var ex = Assert.Throws<BusinessException>(() => sc.Validate());
        Assert.Equal("MyERP:04010", ex.Code);
    }

    [Fact]
    public void Scorecard_Validate_StandingMinGteMax_Throws()
    {
        var sc = new SupplierScorecard(Guid.NewGuid(), _supplierId, _companyId);
        sc.AddStanding("Bad", 50, 30); // min > max
        sc.AddStanding("Good", 30, 100);

        var ex = Assert.Throws<BusinessException>(() => sc.Validate());
        Assert.Equal("MyERP:04010", ex.Code);
    }

    [Fact]
    public void Criterion_ClampScore()
    {
        var crit = new ScorecardCriterion(Guid.NewGuid(), Guid.NewGuid(),
            "Quality", 50m, 100m, null);

        Assert.Equal(80m, crit.ClampScore(80m));   // Within range
        Assert.Equal(100m, crit.ClampScore(150m)); // Clamped to max
        Assert.Equal(0m, crit.ClampScore(-10m));   // Clamped to 0
    }

    [Fact]
    public void Period_Submit()
    {
        var period = new ScorecardPeriod(Guid.NewGuid(), Guid.NewGuid(), _supplierId,
            new DateTime(2026, 6, 1), new DateTime(2026, 6, 30));

        Assert.False(period.IsSubmitted);
        period.Submit(85.5m);
        Assert.True(period.IsSubmitted);
        Assert.Equal(85.5m, period.TotalScore);
    }

    [Fact]
    public void Scorecard_AddCriterion_AccumulatesWeight()
    {
        var sc = CreateScorecard();
        sc.AddCriterion("Delivery", 50m, 100m, "on_time_pct");
        sc.AddCriterion("Quality", 30m, 100m, "quality_score");
        sc.AddCriterion("Pricing", 20m, 100m, "price_competitiveness");

        Assert.Equal(3, sc.Criteria.Count);
        Assert.Equal(100m, sc.Criteria.Sum(c => c.Weight));
    }
}
