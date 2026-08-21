using System;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Assets;
using MyERP.Assets.DomainServices;
using MyERP.Assets.Entities;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Assets;

/// <summary>
/// Unit tests for Asset splitting and proportional cost scaling.
/// Verifies rules migrated from erpnext/assets/doctype/asset/mapper.py (Gotcha #969).
/// </summary>
public class AssetSplitTests
{
    private readonly IRepository<Asset, Guid> _assetRepository = Substitute.For<IRepository<Asset, Guid>>();
    private readonly IRepository<AssetCategory, Guid> _categoryRepository = Substitute.For<IRepository<AssetCategory, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepository = Substitute.For<IRepository<Company, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IDocumentNumberGenerator _numberGenerator = Substitute.For<IDocumentNumberGenerator>();

    private readonly AssetLifecycleManager _lifecycleManager;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _assetId = Guid.NewGuid();

    public AssetSplitTests()
    {
        _lifecycleManager = new AssetLifecycleManager(
            _assetRepository,
            _categoryRepository,
            _companyRepository,
            _fiscalYearRepository,
            _journalEntryRepository,
            _numberGenerator);

        _numberGenerator.GenerateAsync("Asset", _companyId, Arg.Any<DateTime?>())
            .Returns("AST-SPLIT-001");
    }

    [Fact]
    public async Task SplitAssetAsync_ScalesProportionalCostAndSchedule_Correctly()
    {
        // Arrange: 5 laptops @ RM 10,000 total (RM 2,000 each) with 36 months useful life
        var existingAsset = new Asset(_assetId, _companyId, "AST-0001", "MacBook Pro Bundle", DateTime.UtcNow, 10000m)
        {
            AssetQuantity = 5,
            CalculateDepreciation = true,
            DepreciationMethod = DepreciationMethod.StraightLine,
            UsefulLifeMonths = 36,
            FrequencyMonths = 12
        };
        existingAsset.Submit();
        existingAsset.GenerateDepreciationSchedule();

        _assetRepository.GetAsync(_assetId, includeDetails: true).Returns(existingAsset);

        Asset? newAsset = null;
        await _assetRepository.InsertAsync(Arg.Do<Asset>(a => newAsset = a));

        // Act: Split 2 out of 5 laptops
        var result = await _lifecycleManager.SplitAssetAsync(_assetId, 2);

        // Assert: New asset gets 2 qty and 2/5 = 40% of costs (RM 4,000)
        Assert.NotNull(newAsset);
        Assert.Equal("AST-SPLIT-001", newAsset.AssetNumber);
        Assert.Equal(2, newAsset.AssetQuantity);
        Assert.Equal(4000m, newAsset.PurchaseAmount);
        Assert.Equal(4000m, newAsset.ValueAfterDepreciation);
        Assert.Equal(_assetId, newAsset.SplitFromAssetId);
        Assert.Equal(AssetStatus.Submitted, newAsset.Status);
        Assert.Equal(3, newAsset.DepreciationSchedule.Count); // 36 months / 12 = 3 periods
        Assert.Equal(4000m, newAsset.DepreciationSchedule[^1].AccumulatedDepreciation);

        // Existing asset reduced to 3 qty and 3/5 = 60% of costs (RM 6,000)
        Assert.Equal(3, existingAsset.AssetQuantity);
        Assert.Equal(6000m, existingAsset.PurchaseAmount);
        Assert.Equal(6000m, existingAsset.ValueAfterDepreciation);
        Assert.Equal(6000m, existingAsset.DepreciationSchedule[^1].AccumulatedDepreciation);
    }

    [Fact]
    public async Task SplitAssetAsync_QuantityEqualOrGreater_ThrowsValidationException()
    {
        var existingAsset = new Asset(_assetId, _companyId, "AST-0002", "Monitors", DateTime.UtcNow, 3000m)
        {
            AssetQuantity = 3
        };
        existingAsset.Submit();
        _assetRepository.GetAsync(_assetId, includeDetails: true).Returns(existingAsset);

        // Attempting to split 3 out of 3 (or more) must throw
        await Assert.ThrowsAsync<BusinessException>(() => _lifecycleManager.SplitAssetAsync(_assetId, 3));
        await Assert.ThrowsAsync<BusinessException>(() => _lifecycleManager.SplitAssetAsync(_assetId, 4));
    }

    [Fact]
    public async Task SplitAssetAsync_DraftOrCancelledAsset_ThrowsInvalidStatusTransition()
    {
        var draftAsset = new Asset(_assetId, _companyId, "AST-0003", "Chairs", DateTime.UtcNow, 1000m)
        {
            AssetQuantity = 5
        }; // Still Draft
        _assetRepository.GetAsync(_assetId, includeDetails: true).Returns(draftAsset);

        await Assert.ThrowsAsync<BusinessException>(() => _lifecycleManager.SplitAssetAsync(_assetId, 2));
    }
}
