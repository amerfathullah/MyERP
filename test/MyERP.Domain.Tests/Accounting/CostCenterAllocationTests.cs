using System;
using System.Linq;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class CostCenterAllocationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _mainCcId = Guid.NewGuid();
    private readonly Guid _childCc1 = Guid.NewGuid();
    private readonly Guid _childCc2 = Guid.NewGuid();
    private readonly Guid _childCc3 = Guid.NewGuid();

    [Fact]
    public void Create_DefaultState()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, new DateTime(2026, 1, 1));

        Assert.Equal(_companyId, alloc.CompanyId);
        Assert.Equal(_mainCcId, alloc.MainCostCenterId);
        Assert.Equal(new DateTime(2026, 1, 1), alloc.ValidFrom);
        Assert.True(alloc.IsActive);
        Assert.Empty(alloc.Entries);
    }

    [Fact]
    public void AddEntry_Success()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);

        alloc.AddEntry(_childCc1, 60m);
        alloc.AddEntry(_childCc2, 40m);

        Assert.Equal(2, alloc.Entries.Count);
        Assert.Contains(alloc.Entries, e => e.ChildCostCenterId == _childCc1 && e.Percentage == 60m);
        Assert.Contains(alloc.Entries, e => e.ChildCostCenterId == _childCc2 && e.Percentage == 40m);
    }

    [Fact]
    public void AddEntry_MainEqualsChild_Throws()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);

        var ex = Assert.Throws<BusinessException>(() => alloc.AddEntry(_mainCcId, 100m));
        Assert.Equal("MyERP:02038", ex.Code);
    }

    [Fact]
    public void AddEntry_ZeroPercentage_Throws()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);

        Assert.Throws<BusinessException>(() => alloc.AddEntry(_childCc1, 0m));
    }

    [Fact]
    public void AddEntry_NegativePercentage_Throws()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);

        Assert.Throws<BusinessException>(() => alloc.AddEntry(_childCc1, -10m));
    }

    [Fact]
    public void AddEntry_Over100Percentage_Throws()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);

        Assert.Throws<BusinessException>(() => alloc.AddEntry(_childCc1, 101m));
    }

    [Fact]
    public void AddEntry_DuplicateChild_Throws()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);
        alloc.AddEntry(_childCc1, 50m);

        var ex = Assert.Throws<BusinessException>(() => alloc.AddEntry(_childCc1, 50m));
        Assert.Equal("MyERP:02040", ex.Code);
    }

    [Fact]
    public void ValidatePercentages_Exact100_Passes()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);
        alloc.AddEntry(_childCc1, 60m);
        alloc.AddEntry(_childCc2, 40m);

        alloc.ValidatePercentages(); // Should not throw
    }

    [Fact]
    public void ValidatePercentages_NotSum100_Throws()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);
        alloc.AddEntry(_childCc1, 60m);
        alloc.AddEntry(_childCc2, 30m); // Total = 90%

        var ex = Assert.Throws<BusinessException>(() => alloc.ValidatePercentages());
        Assert.Equal("MyERP:02042", ex.Code);
    }

    [Fact]
    public void ValidatePercentages_Empty_Throws()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);

        var ex = Assert.Throws<BusinessException>(() => alloc.ValidatePercentages());
        Assert.Equal("MyERP:02041", ex.Code);
    }

    [Fact]
    public void Distribute_TwoEntries_EvenSplit()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);
        alloc.AddEntry(_childCc1, 50m);
        alloc.AddEntry(_childCc2, 50m);

        var result = alloc.Distribute(1000m);

        Assert.Equal(2, result.Count);
        Assert.Equal(1000m, result.Sum(r => r.Amount)); // Total distributed equals input
        Assert.All(result, r => Assert.Equal(500m, r.Amount)); // Each gets exactly 50%
    }

    [Fact]
    public void Distribute_ThreeEntries_Rounding()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);
        alloc.AddEntry(_childCc1, 33.33m);
        alloc.AddEntry(_childCc2, 33.33m);
        alloc.AddEntry(_childCc3, 33.34m);

        var result = alloc.Distribute(100m);

        // Total distributed should exactly equal original amount (rounding absorbed by first entry)
        Assert.Equal(100m, result.Sum(r => r.Amount));
    }

    [Fact]
    public void Distribute_EmptyEntries_ReturnsEmpty()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);

        var result = alloc.Distribute(1000m);

        Assert.Empty(result);
    }

    [Fact]
    public void Distribute_ZeroAmount_AllZero()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);
        alloc.AddEntry(_childCc1, 60m);
        alloc.AddEntry(_childCc2, 40m);

        var result = alloc.Distribute(0m);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(0m, r.Amount));
    }

    [Fact]
    public void Distribute_UnevenSplit_60_40()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);
        alloc.AddEntry(_childCc1, 60m);
        alloc.AddEntry(_childCc2, 40m);

        var result = alloc.Distribute(1000m);

        // One should have 600, other 400 (exact for these percentages)
        Assert.Equal(1000m, result.Sum(r => r.Amount));
    }

    [Fact]
    public void Entry_Properties()
    {
        var allocId = Guid.NewGuid();
        var entry = new CostCenterAllocationEntry(Guid.NewGuid(), allocId, _childCc1, 75.5m);

        Assert.Equal(allocId, entry.CostCenterAllocationId);
        Assert.Equal(_childCc1, entry.ChildCostCenterId);
        Assert.Equal(75.5m, entry.Percentage);
    }

    [Fact]
    public void Create_EmptyCompanyId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CostCenterAllocation(Guid.NewGuid(), Guid.Empty, _mainCcId, DateTime.Today));
    }

    [Fact]
    public void Create_EmptyMainCcId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CostCenterAllocation(Guid.NewGuid(), _companyId, Guid.Empty, DateTime.Today));
    }

    [Fact]
    public void ThreeWaySplit_TotalsCorrect()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, _mainCcId, DateTime.Today);
        alloc.AddEntry(_childCc1, 50m);
        alloc.AddEntry(_childCc2, 30m);
        alloc.AddEntry(_childCc3, 20m);

        var result = alloc.Distribute(999.99m);

        // Round-off absorbed by first entry ensures total invariant
        Assert.Equal(999.99m, result.Sum(r => r.Amount));
    }
}
