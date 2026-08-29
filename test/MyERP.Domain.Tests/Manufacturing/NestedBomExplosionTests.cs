using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class NestedBomExplosionTests
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly BomValidationService _service;

    public NestedBomExplosionTests()
    {
        _bomRepository = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        _service = new BomValidationService(_bomRepository);
    }

    [Fact]
    public async Task ExplodeBomAsync_WithStockQtyAndSubBomOutputQuantity_CalculatesExactQuantity()
    {
        // Setup matching ERPNext test_nested_bom_uses_stock_qty_for_output_normalization:
        // Parent BOM requires 2 Boxes (conversion factor 10 -> 20 StockUnits) of Sub-Assembly (Phantom).
        // Sub-Assembly BOM produces 5 units per batch, consuming 3 units of Raw Material Leaf.
        // Expected Leaf quantity = (20 / 1) * (3 / 5) = 12 units.

        var parentBomId = Guid.NewGuid();
        var subBomId = Guid.NewGuid();
        var parentItemId = Guid.NewGuid();
        var subAssemblyItemId = Guid.NewGuid();
        var rawLeafItemId = Guid.NewGuid();

        var subBom = new BillOfMaterials(subBomId, Guid.NewGuid(), "BOM-SUB-001", subAssemblyItemId)
        {
            Quantity = 5m
        };
        subBom.Items.Add(new BomItem(Guid.NewGuid(), subBomId, rawLeafItemId, "Leaf Raw Material", 3m, 10m, uom: "Unit", conversionFactor: 1m));

        var parentBom = new BillOfMaterials(parentBomId, Guid.NewGuid(), "BOM-PARENT-001", parentItemId)
        {
            Quantity = 1m
        };
        var subBomItem = new BomItem(Guid.NewGuid(), parentBomId, subAssemblyItemId, "Sub Assembly", 2m, 50m, uom: "Box", conversionFactor: 10m)
        {
            IsPhantom = true,
            SubBomId = subBomId
        };
        parentBom.Items.Add(subBomItem);

        _bomRepository.GetAsync(parentBomId).Returns(Task.FromResult(parentBom));
        _bomRepository.GetAsync(subBomId).Returns(Task.FromResult(subBom));

        var result = await _service.ExplodeBomAsync(parentBomId, multiplier: 1m);

        result.Count.ShouldBe(1);
        result[0].ItemId.ShouldBe(rawLeafItemId);
        result[0].Quantity.ShouldBe(12m);
    }

    [Fact]
    public async Task ExplodeBomAsync_MultiLevelNesting_MultipliesCorrectly()
    {
        // Root requires 8 Parent (Phantom)
        // Parent requires 4 Child (Phantom)
        // Child requires 2 Raw Material
        // Total Raw Material = 8 * 4 * 2 = 64

        var rootBomId = Guid.NewGuid();
        var parentBomId = Guid.NewGuid();
        var childBomId = Guid.NewGuid();

        var rootItemId = Guid.NewGuid();
        var parentItemId = Guid.NewGuid();
        var childItemId = Guid.NewGuid();
        var rawItemId = Guid.NewGuid();

        var childBom = new BillOfMaterials(childBomId, Guid.NewGuid(), "BOM-CHILD", childItemId)
        {
            Quantity = 1m
        };
        childBom.Items.Add(new BomItem(Guid.NewGuid(), childBomId, rawItemId, "Raw Material", 2m, 5m));

        var parentBom = new BillOfMaterials(parentBomId, Guid.NewGuid(), "BOM-PARENT", parentItemId)
        {
            Quantity = 1m
        };
        var childItem = new BomItem(Guid.NewGuid(), parentBomId, childItemId, "Child Assembly", 4m, 10m)
        {
            IsPhantom = true,
            SubBomId = childBomId
        };
        parentBom.Items.Add(childItem);

        var rootBom = new BillOfMaterials(rootBomId, Guid.NewGuid(), "BOM-ROOT", rootItemId)
        {
            Quantity = 1m
        };
        var parentItem = new BomItem(Guid.NewGuid(), rootBomId, parentItemId, "Parent Assembly", 8m, 40m)
        {
            IsPhantom = true,
            SubBomId = parentBomId
        };
        rootBom.Items.Add(parentItem);

        _bomRepository.GetAsync(rootBomId).Returns(Task.FromResult(rootBom));
        _bomRepository.GetAsync(parentBomId).Returns(Task.FromResult(parentBom));
        _bomRepository.GetAsync(childBomId).Returns(Task.FromResult(childBom));

        var result = await _service.ExplodeBomAsync(rootBomId, multiplier: 1m);

        result.Count.ShouldBe(1);
        result[0].ItemId.ShouldBe(rawItemId);
        result[0].Quantity.ShouldBe(64m);
    }
}
