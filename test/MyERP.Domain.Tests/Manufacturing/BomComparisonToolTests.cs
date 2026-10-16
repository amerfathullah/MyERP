using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

/// <summary>
/// Unit tests for BOM Comparison / Diff tool per ERPNext PR #60755 / commit 5461b27589 / 1795a37bef / 88b92db7ce.
/// Verifies:
/// - Same BOM validation throws BomDiffSameBom error code.
/// - Header changes detection across quantity, UOM, costs, routing.
/// - Repeated rows preservation across all 8 permutations from ERPNext test_bom_diff_preserves_repeated_rows.
/// - Operations and secondary items comparison.
/// </summary>
public class BomComparisonToolTests
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
    private readonly IRepository<WorkOrder, Guid> _woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
    private readonly IRepository<MaterialRequest, Guid> _mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
    private readonly IDocumentNumberGenerator _numGen = Substitute.For<IDocumentNumberGenerator>();
    private readonly StockValuationService _valService = Substitute.For<StockValuationService>(
        Substitute.For<IRepository<StockLedgerEntry, Guid>>(),
        Substitute.For<IRepository<Item, Guid>>(),
        Substitute.For<Volo.Abp.Settings.ISettingProvider>());
    private readonly BinService _binService = Substitute.For<BinService>(
        Substitute.For<IRepository<Bin, Guid>>());

    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Operation, Guid> _opRepo = Substitute.For<IRepository<Operation, Guid>>();

    private readonly ManufacturingAppService _appService;

    public BomComparisonToolTests()
    {
        _appService = new ManufacturingAppService(
            _bomRepo, _woRepo, _mrRepo, _numGen, _valService, _binService);

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<Item, Guid>>().Returns(_itemRepo);
        lazySp.LazyGetService<IRepository<Operation, Guid>>().Returns(_opRepo);
        _appService.LazyServiceProvider = lazySp;

        _itemRepo.GetQueryableAsync().Returns(new List<Item>().AsQueryable());
        _opRepo.GetQueryableAsync().Returns(new List<Operation>().AsQueryable());
    }

    [Fact]
    public async Task GetBomDiff_SameBom_ThrowsBomDiffSameBom()
    {
        var bomId = Guid.NewGuid();
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _appService.GetBomDiffAsync(new BomDiffRequestDto
            {
                BomId1 = bomId,
                BomId2 = bomId
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.BomDiffSameBom);
    }

    [Fact]
    public async Task GetBomDiff_HeaderDifferences_Detected()
    {
        var companyId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();

        var bom1 = new BillOfMaterials(Guid.NewGuid(), companyId, "BOM-001", fgItemId, null)
        {
            Quantity = 1,
            Uom = "Unit",
            TotalMaterialCost = 100,
            OperatingCost = 20,
            WithOperations = false,
            ProcessLossPercentage = 5
        };

        var bom2 = new BillOfMaterials(Guid.NewGuid(), companyId, "BOM-002", fgItemId, null)
        {
            Quantity = 2,
            Uom = "Box",
            TotalMaterialCost = 180,
            OperatingCost = 40,
            WithOperations = true,
            ProcessLossPercentage = 10
        };

        _bomRepo.GetAsync(bom1.Id, true).Returns(bom1);
        _bomRepo.GetAsync(bom2.Id, true).Returns(bom2);

        var diff = await _appService.GetBomDiffAsync(new BomDiffRequestDto
        {
            BomId1 = bom1.Id,
            BomId2 = bom2.Id
        });

        diff.BomNumber1.ShouldBe("BOM-001");
        diff.BomNumber2.ShouldBe("BOM-002");

        var qtyChange = diff.HeaderChanges.FirstOrDefault(c => c.FieldName == "Quantity");
        qtyChange.ShouldNotBeNull();
        qtyChange.OldValue.ShouldBe("1");
        qtyChange.NewValue.ShouldBe("2");

        var uomChange = diff.HeaderChanges.FirstOrDefault(c => c.FieldName == "Uom");
        uomChange.ShouldNotBeNull();
        uomChange.OldValue.ShouldBe("Unit");
        uomChange.NewValue.ShouldBe("Box");

        var opsChange = diff.HeaderChanges.FirstOrDefault(c => c.FieldName == "WithOperations");
        opsChange.ShouldNotBeNull();
        opsChange.OldValue.ShouldBe("False");
        opsChange.NewValue.ShouldBe("True");

        var plChange = diff.HeaderChanges.FirstOrDefault(c => c.FieldName == "ProcessLossPercentage");
        plChange.ShouldNotBeNull();
        plChange.OldValue.ShouldBe("5");
        plChange.NewValue.ShouldBe("10");
    }

    /// <summary>
    /// Covers all 8 repeated row permutations from ERPNext test_bom.py:
    /// test_bom_diff_preserves_repeated_rows:
    /// ([1, 2], [1, 2], [], [], 0),
    /// ([1, 2], [2, 1], [], [], 0),
    /// ([1, 2], [2, 2], [], [], 1),
    /// ([1, 2], [3, 2], [], [], 1),
    /// ([1, 2], [3, 1, 2], [3], [], 0),
    /// ([1, 2], [2], [], [1], 0),
    /// ([1], [1, 1], [1], [], 0),
    /// ([1, 1], [1], [], [1], 0),
    /// </summary>
    [Theory]
    [InlineData(new[] { 1.0, 2.0 }, new[] { 1.0, 2.0 }, 0, 0, 0)]
    [InlineData(new[] { 1.0, 2.0 }, new[] { 2.0, 1.0 }, 0, 0, 0)]
    [InlineData(new[] { 1.0, 2.0 }, new[] { 2.0, 2.0 }, 0, 0, 1)]
    [InlineData(new[] { 1.0, 2.0 }, new[] { 3.0, 2.0 }, 0, 0, 1)]
    [InlineData(new[] { 1.0, 2.0 }, new[] { 3.0, 1.0, 2.0 }, 1, 0, 0)]
    [InlineData(new[] { 1.0, 2.0 }, new[] { 2.0 }, 0, 1, 0)]
    [InlineData(new[] { 1.0 }, new[] { 1.0, 1.0 }, 1, 0, 0)]
    [InlineData(new[] { 1.0, 1.0 }, new[] { 1.0 }, 0, 1, 0)]
    public async Task GetBomDiff_PreservesRepeatedRows_All8Permutations(
        double[] oldValues, double[] newValues, int expectedAdded, int expectedRemoved, int expectedChanged)
    {
        var companyId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var rawMaterialId = Guid.NewGuid();

        var bom1 = new BillOfMaterials(Guid.NewGuid(), companyId, "BOM-OLD", fgItemId, null);
        foreach (var val in oldValues)
        {
            bom1.AddItem(new BomItem(Guid.NewGuid(), bom1.Id, rawMaterialId, "RM Repeated", (decimal)val, 10m));
        }

        var bom2 = new BillOfMaterials(Guid.NewGuid(), companyId, "BOM-NEW", fgItemId, null);
        foreach (var val in newValues)
        {
            bom2.AddItem(new BomItem(Guid.NewGuid(), bom2.Id, rawMaterialId, "RM Repeated", (decimal)val, 10m));
        }

        _bomRepo.GetAsync(bom1.Id, true).Returns(bom1);
        _bomRepo.GetAsync(bom2.Id, true).Returns(bom2);

        var diff = await _appService.GetBomDiffAsync(new BomDiffRequestDto
        {
            BomId1 = bom1.Id,
            BomId2 = bom2.Id
        });

        diff.AddedRows.Count(r => r.TableName == "Items").ShouldBe(expectedAdded);
        diff.RemovedRows.Count(r => r.TableName == "Items").ShouldBe(expectedRemoved);
        diff.RowChanges.Count(r => r.TableName == "Items").ShouldBe(expectedChanged);

        if (expectedChanged > 0)
        {
            var rowChange = diff.RowChanges.First(r => r.TableName == "Items");
            var change = rowChange.Changes.FirstOrDefault(c => c.FieldName == "Quantity");
            change.ShouldNotBeNull();

            if (newValues.SequenceEqual(new[] { 2.0, 2.0 }))
            {
                rowChange.RowIndex.ShouldBe(1);
                change.OldValue.ShouldBe("1");
                change.NewValue.ShouldBe("2");
            }
            else if (newValues.SequenceEqual(new[] { 3.0, 2.0 }))
            {
                rowChange.RowIndex.ShouldBe(0);
                change.OldValue.ShouldBe("1");
                change.NewValue.ShouldBe("3");
            }
        }

        if (expectedAdded > 0)
        {
            var addedRow = diff.AddedRows.First(r => r.TableName == "Items");
            if (newValues.Length == 3)
            {
                addedRow.Quantity.ShouldBe(3m);
            }
            else if (newValues.Length == 2)
            {
                addedRow.Quantity.ShouldBe(1m);
            }
        }

        if (expectedRemoved > 0)
        {
            var removedRow = diff.RemovedRows.First(r => r.TableName == "Items");
            removedRow.Quantity.ShouldBe(1m);
        }
    }

    [Fact]
    public async Task GetBomDiff_OperationsAndSecondaryItems_ComparedCorrectly()
    {
        var companyId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var opId = Guid.NewGuid();
        var secItemId = Guid.NewGuid();

        var bom1 = new BillOfMaterials(Guid.NewGuid(), companyId, "BOM-OP1", fgItemId, null);
        bom1.AddOperation(new BomOperation(Guid.NewGuid(), bom1.Id, opId, 1, 30m, null, null) { OperatingCost = 50m });
        bom1.AddSecondaryItem(new BomSecondaryItem(Guid.NewGuid(), bom1.Id, secItemId, SecondaryItemType.Scrap, 2m) { ItemName = "Scrap A", Rate = 5m });

        var bom2 = new BillOfMaterials(Guid.NewGuid(), companyId, "BOM-OP2", fgItemId, null);
        bom2.AddOperation(new BomOperation(Guid.NewGuid(), bom2.Id, opId, 1, 45m, null, null) { OperatingCost = 75m });
        bom2.AddSecondaryItem(new BomSecondaryItem(Guid.NewGuid(), bom2.Id, secItemId, SecondaryItemType.Scrap, 3m) { ItemName = "Scrap A", Rate = 5m });

        _bomRepo.GetAsync(bom1.Id, true).Returns(bom1);
        _bomRepo.GetAsync(bom2.Id, true).Returns(bom2);

        var diff = await _appService.GetBomDiffAsync(new BomDiffRequestDto
        {
            BomId1 = bom1.Id,
            BomId2 = bom2.Id
        });

        var opChange = diff.RowChanges.FirstOrDefault(r => r.TableName == "Operations");
        opChange.ShouldNotBeNull();
        opChange.Changes.Any(c => c.FieldName == "TimeInMins" && c.OldValue == "30" && c.NewValue == "45").ShouldBeTrue();
        opChange.Changes.Any(c => c.FieldName == "OperatingCost" && c.OldValue == "50.00" && c.NewValue == "75.00").ShouldBeTrue();

        var secChange = diff.RowChanges.FirstOrDefault(r => r.TableName == "SecondaryItems");
        secChange.ShouldNotBeNull();
        secChange.Changes.Any(c => c.FieldName == "Quantity" && c.OldValue == "2" && c.NewValue == "3").ShouldBeTrue();
    }
}
