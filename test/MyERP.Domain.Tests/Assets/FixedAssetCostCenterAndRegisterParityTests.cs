using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Assets;
using MyERP.Assets.DomainServices;
using MyERP.Assets.Entities;
using MyERP.Core;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Assets;

public class FixedAssetCostCenterAndRegisterParityTests
{
    [Fact]
    public async Task SplitAsset_PreservesCostCenterId()
    {
        // Per ERPNext PR #59326 / gotchas: cost center must be preserved on asset split
        var assetId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var costCenterId = Guid.NewGuid();

        var asset = new Asset(assetId, companyId, "AST-001", "Office Laptop", DateTime.UtcNow, 3000m)
        {
            AssetQuantity = 3,
            CostCenterId = costCenterId,
        };
        asset.Submit();

        var assetRepo = Substitute.For<IRepository<Asset, Guid>>();
        assetRepo.WithDetailsAsync(Arg.Any<Expression<Func<Asset, object>>[]>())
            .Returns(Task.FromResult(new List<Asset> { asset }.AsQueryable()));

        var catRepo = Substitute.For<IRepository<AssetCategory, Guid>>();
        var compRepo = Substitute.For<IRepository<MyERP.Core.Entities.Company, Guid>>();
        var fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var numberGenerator = Substitute.For<MyERP.Core.DomainServices.IDocumentNumberGenerator>();
        numberGenerator.GenerateAsync("Asset", companyId).Returns(Task.FromResult("AST-002"));

        var manager = new AssetLifecycleManager(assetRepo, catRepo, compRepo, fyRepo, jeRepo, numberGenerator);
        var split = await manager.SplitAssetAsync(assetId, 1);

        split.ShouldNotBeNull();
        split.CostCenterId.ShouldBe(costCenterId);
        split.AssetQuantity.ShouldBe(1);
        asset.AssetQuantity.ShouldBe(2);
    }

    [Fact]
    public async Task FixedAssetRegister_IncludesChildCostCenters_WhenParentFiltered()
    {
        // Per ERPNext PR #59326 (commit 30ce0fbf04):
        // Filter by parent cost center must include assets assigned to child/descendant cost centers
        var companyId = Guid.NewGuid();
        var parentCcId = Guid.NewGuid();
        var childCcId = Guid.NewGuid();
        var grandchildCcId = Guid.NewGuid();
        var otherCcId = Guid.NewGuid();

        var parentCc = new CostCenter(parentCcId, companyId, "Operations", isGroup: true);
        var childCc = new CostCenter(childCcId, companyId, "IT Department", isGroup: true, parentId: parentCcId);
        var grandchildCc = new CostCenter(grandchildCcId, companyId, "Software Development", isGroup: false, parentId: childCcId);
        var otherCc = new CostCenter(otherCcId, companyId, "Marketing", isGroup: false);

        var costCenterList = new List<CostCenter> { parentCc, childCc, grandchildCc, otherCc };
        var costCenterRepo = Substitute.For<IRepository<CostCenter, Guid>>();
        costCenterRepo.GetListAsync(Arg.Any<Expression<Func<CostCenter, bool>>>())
            .Returns(callInfo =>
            {
                var predicate = callInfo.Arg<Expression<Func<CostCenter, bool>>>().Compile();
                return Task.FromResult(costCenterList.Where(predicate).ToList());
            });

        // 3 assets:
        // Asset 1 in IT Dept (child) -> Should be included
        var asset1 = new Asset(Guid.NewGuid(), companyId, "AST-001", "Server Blade", DateTime.UtcNow, 10000m)
        {
            CostCenterId = childCcId
        };
        asset1.Submit();

        // Asset 2 in Software Dev (grandchild) -> Should be included
        var asset2 = new Asset(Guid.NewGuid(), companyId, "AST-002", "Developer Workstation", DateTime.UtcNow, 5000m)
        {
            CostCenterId = grandchildCcId
        };
        asset2.Submit();

        // Asset 3 in Marketing (different root) -> Must be excluded
        var asset3 = new Asset(Guid.NewGuid(), companyId, "AST-003", "Camera Kit", DateTime.UtcNow, 3000m)
        {
            CostCenterId = otherCcId
        };
        asset3.Submit();

        var assetRepo = Substitute.For<IRepository<Asset, Guid>>();
        assetRepo.WithDetailsAsync(Arg.Any<Expression<Func<Asset, object>>[]>())
            .Returns(Task.FromResult(new List<Asset> { asset1, asset2, asset3 }.AsQueryable()));

        var categoryRepo = Substitute.For<IRepository<AssetCategory, Guid>>();
        categoryRepo.GetListAsync().Returns(Task.FromResult(new List<AssetCategory>()));

        var adjRepo = Substitute.For<IRepository<AssetValueAdjustment, Guid>>();
        adjRepo.GetQueryableAsync().Returns(Task.FromResult(new List<AssetValueAdjustment>().AsQueryable()));

        var financeBookRepo = Substitute.For<IRepository<FinanceBook, Guid>>();
        financeBookRepo.GetQueryableAsync().Returns(Task.FromResult(new List<FinanceBook>().AsQueryable()));

        var capitalizationRepo = Substitute.For<IRepository<AssetCapitalization, Guid>>();
        capitalizationRepo.WithDetailsAsync(Arg.Any<Expression<Func<AssetCapitalization, object>>[]>())
            .Returns(Task.FromResult(new List<AssetCapitalization>().AsQueryable()));

        var reportingService = new AssetReportingAppService(
            assetRepo,
            categoryRepo,
            adjRepo,
            financeBookRepo,
            capitalizationRepo,
            costCenterRepo);

        // Filter by parent CostCenter ("Operations")
        var report = await reportingService.GetFixedAssetRegisterAsync(new FixedAssetRegisterRequestDto
        {
            CompanyId = companyId,
            CostCenterId = parentCcId,
            FilterBasedOn = null,
        });

        report.ShouldNotBeNull();
        report.Rows.Count.ShouldBe(2);
        report.Rows.Select(r => r.AssetNumber).ShouldBe(new[] { "AST-001", "AST-002" }, ignoreOrder: true);
    }

    [Fact]
    public async Task FixedAssetRegister_GroupByCostCenter_AggregatesRows()
    {
        var companyId = Guid.NewGuid();
        var itCcId = Guid.NewGuid();
        var mktCcId = Guid.NewGuid();

        var itCc = new CostCenter(itCcId, companyId, "IT Department");
        var mktCc = new CostCenter(mktCcId, companyId, "Marketing Department");

        var costCenterList = new List<CostCenter> { itCc, mktCc };
        var costCenterRepo = Substitute.For<IRepository<CostCenter, Guid>>();
        costCenterRepo.GetListAsync(Arg.Any<Expression<Func<CostCenter, bool>>>())
            .Returns(callInfo =>
            {
                var predicate = callInfo.Arg<Expression<Func<CostCenter, bool>>>().Compile();
                return Task.FromResult(costCenterList.Where(predicate).ToList());
            });

        var asset1 = new Asset(Guid.NewGuid(), companyId, "AST-001", "Workstation 1", DateTime.UtcNow, 4000m)
        {
            CostCenterId = itCcId
        };
        asset1.Submit();

        var asset2 = new Asset(Guid.NewGuid(), companyId, "AST-002", "Workstation 2", DateTime.UtcNow, 6000m)
        {
            CostCenterId = itCcId
        };
        asset2.Submit();

        var asset3 = new Asset(Guid.NewGuid(), companyId, "AST-003", "Booth Setup", DateTime.UtcNow, 8000m)
        {
            CostCenterId = mktCcId
        };
        asset3.Submit();

        var assetRepo = Substitute.For<IRepository<Asset, Guid>>();
        assetRepo.WithDetailsAsync(Arg.Any<Expression<Func<Asset, object>>[]>())
            .Returns(Task.FromResult(new List<Asset> { asset1, asset2, asset3 }.AsQueryable()));

        var categoryRepo = Substitute.For<IRepository<AssetCategory, Guid>>();
        categoryRepo.GetListAsync().Returns(Task.FromResult(new List<AssetCategory>()));

        var adjRepo = Substitute.For<IRepository<AssetValueAdjustment, Guid>>();
        adjRepo.GetQueryableAsync().Returns(Task.FromResult(new List<AssetValueAdjustment>().AsQueryable()));

        var financeBookRepo = Substitute.For<IRepository<FinanceBook, Guid>>();
        financeBookRepo.GetQueryableAsync().Returns(Task.FromResult(new List<FinanceBook>().AsQueryable()));

        var capitalizationRepo = Substitute.For<IRepository<AssetCapitalization, Guid>>();
        capitalizationRepo.WithDetailsAsync(Arg.Any<Expression<Func<AssetCapitalization, object>>[]>())
            .Returns(Task.FromResult(new List<AssetCapitalization>().AsQueryable()));

        var reportingService = new AssetReportingAppService(
            assetRepo,
            categoryRepo,
            adjRepo,
            financeBookRepo,
            capitalizationRepo,
            costCenterRepo);

        var report = await reportingService.GetFixedAssetRegisterAsync(new FixedAssetRegisterRequestDto
        {
            CompanyId = companyId,
            GroupBy = "Cost Center",
            FilterBasedOn = null,
        });

        report.ShouldNotBeNull();
        report.Rows.Count.ShouldBe(2);

        var itRow = report.Rows.First(r => r.CostCenterId == itCcId);
        itRow.CostCenterName.ShouldBe("IT Department");
        itRow.GrossPurchaseAmount.ShouldBe(10000m);

        var mktRow = report.Rows.First(r => r.CostCenterId == mktCcId);
        mktRow.CostCenterName.ShouldBe("Marketing Department");
        mktRow.GrossPurchaseAmount.ShouldBe(8000m);
    }
}
