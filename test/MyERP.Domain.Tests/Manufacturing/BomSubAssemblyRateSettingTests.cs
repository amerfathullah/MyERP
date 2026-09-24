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

public class BomSubAssemblyRateSettingTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Fact]
    public void BomItem_SetRateOfSubAssemblyItemBasedOnBom_DefaultsTrue()
    {
        var item = new BomItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Sub-Assembly Part", 5, 50);

        item.SetRateOfSubAssemblyItemBasedOnBom.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateCostAndPropagate_WhenSettingIsTrue_UpdatesParentSubAssemblyRate()
    {
        var childBomId = Guid.NewGuid();
        var parentBomId = Guid.NewGuid();
        var subAssemblyItemId = Guid.NewGuid();

        var childBom = new BillOfMaterials(childBomId, CompanyId, "BOM-CHILD", subAssemblyItemId)
        {
            Quantity = 1
        };
        childBom.AddItem(new BomItem(Guid.NewGuid(), childBomId, Guid.NewGuid(), "Raw Material", 1, 150));
        childBom.RecalculateCost(); // childBom unit cost = 150

        var parentBom = new BillOfMaterials(parentBomId, CompanyId, "BOM-PARENT", Guid.NewGuid())
        {
            Quantity = 1
        };
        var parentItem = new BomItem(Guid.NewGuid(), parentBomId, subAssemblyItemId, "Sub-Assembly Part", 2, 50)
        {
            SubBomId = childBomId,
            SetRateOfSubAssemblyItemBasedOnBom = true // Setting is ON (default)
        };
        parentBom.AddItem(parentItem);
        parentBom.RecalculateCost();

        parentItem.Rate.ShouldBe(50); // Initial rate before propagation

        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        bomRepo.GetAsync(childBomId, includeDetails: true).Returns(childBom);
        bomRepo.GetQueryableAsync().Returns(Task.FromResult(new List<BillOfMaterials> { parentBom }.AsQueryable()));

        var service = new BomCostPropagationService(bomRepo);
        var updatedCount = await service.UpdateCostAndPropagateAsync(childBomId);

        updatedCount.ShouldBeGreaterThanOrEqualTo(2);
        parentItem.Rate.ShouldBe(150); // Updated to match child BOM unit cost
        parentItem.Amount.ShouldBe(300); // 2 * 150
        parentBom.TotalMaterialCost.ShouldBe(300);
    }

    [Fact]
    public async Task UpdateCostAndPropagate_WhenSettingIsFalse_PreservesParentSubAssemblyRate()
    {
        var childBomId = Guid.NewGuid();
        var parentBomId = Guid.NewGuid();
        var subAssemblyItemId = Guid.NewGuid();

        var childBom = new BillOfMaterials(childBomId, CompanyId, "BOM-CHILD", subAssemblyItemId)
        {
            Quantity = 1
        };
        childBom.AddItem(new BomItem(Guid.NewGuid(), childBomId, Guid.NewGuid(), "Raw Material", 1, 150));
        childBom.RecalculateCost(); // childBom unit cost = 150

        var parentBom = new BillOfMaterials(parentBomId, CompanyId, "BOM-PARENT", Guid.NewGuid())
        {
            Quantity = 1
        };
        var parentItem = new BomItem(Guid.NewGuid(), parentBomId, subAssemblyItemId, "Sub-Assembly Part", 2, 50)
        {
            SubBomId = childBomId,
            SetRateOfSubAssemblyItemBasedOnBom = false // Per PR #59178: User explicitly turned setting OFF for this item
        };
        parentBom.AddItem(parentItem);
        parentBom.RecalculateCost();

        parentItem.Rate.ShouldBe(50);

        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        bomRepo.GetAsync(childBomId, includeDetails: true).Returns(childBom);
        bomRepo.GetQueryableAsync().Returns(Task.FromResult(new List<BillOfMaterials> { parentBom }.AsQueryable()));

        var service = new BomCostPropagationService(bomRepo);
        var updatedCount = await service.UpdateCostAndPropagateAsync(childBomId);

        parentItem.Rate.ShouldBe(50); // Preserved custom valuation rate, not overwritten by child BOM cost
        parentItem.Amount.ShouldBe(100); // 2 * 50
        parentBom.TotalMaterialCost.ShouldBe(100);
    }

    [Fact]
    public async Task ReplaceBom_WhenSettingIsFalse_UpdatesSubBomId_PreservesRate()
    {
        var oldBomId = Guid.NewGuid();
        var newBomId = Guid.NewGuid();
        var parentBomId = Guid.NewGuid();
        var subAssemblyItemId = Guid.NewGuid();

        var oldBom = new BillOfMaterials(oldBomId, CompanyId, "BOM-OLD", subAssemblyItemId) { Quantity = 1 };
        oldBom.AddItem(new BomItem(Guid.NewGuid(), oldBomId, Guid.NewGuid(), "RM", 1, 100));
        oldBom.RecalculateCost();

        var newBom = new BillOfMaterials(newBomId, CompanyId, "BOM-NEW", subAssemblyItemId) { Quantity = 1 };
        newBom.AddItem(new BomItem(Guid.NewGuid(), newBomId, Guid.NewGuid(), "RM", 1, 300));
        newBom.RecalculateCost(); // new unit cost = 300

        var parentBom = new BillOfMaterials(parentBomId, CompanyId, "BOM-PARENT", Guid.NewGuid()) { Quantity = 1 };
        var parentItem = new BomItem(Guid.NewGuid(), parentBomId, subAssemblyItemId, "Sub-Assembly", 2, 75)
        {
            SubBomId = oldBomId,
            SetRateOfSubAssemblyItemBasedOnBom = false // Setting OFF
        };
        parentBom.AddItem(parentItem);
        parentBom.RecalculateCost();

        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        bomRepo.GetAsync(oldBomId).Returns(oldBom);
        bomRepo.GetAsync(newBomId).Returns(newBom);
        bomRepo.GetAsync(newBomId, includeDetails: true).Returns(newBom);
        bomRepo.GetQueryableAsync().Returns(Task.FromResult(new List<BillOfMaterials> { parentBom }.AsQueryable()));

        var service = new BomCostPropagationService(bomRepo);
        var (updatedItems, _) = await service.ReplaceBomAsync(oldBomId, newBomId);

        updatedItems.ShouldBe(1);
        parentItem.SubBomId.ShouldBe(newBomId);
        parentItem.Rate.ShouldBe(75); // Rate preserved because SetRateOfSubAssemblyItemBasedOnBom == false
    }
}
