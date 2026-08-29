using System;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Manufacturing;

public class PercentageBomTests
{
    [Fact]
    public void Bom_PercentageFormulation_ComputesCorrectQuantitiesAndAbsorbsBalance()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid())
        {
            Quantity = 200m,
            SetQtyBasedOnPercentage = true
        };

        var rm1 = new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM1", 0, 100m) { Percentage = 40m };
        var rm2 = new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM2", 0, 100m) { Percentage = 35m };
        var rm3 = new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM3", 0, 100m) { IsBalanceItem = true };

        bom.Items.Add(rm1);
        bom.Items.Add(rm2);
        bom.Items.Add(rm3);

        bom.RecalculateCost();

        bom.Items[0].Quantity.ShouldBe(80m);
        bom.Items[1].Quantity.ShouldBe(70m);
        bom.Items[2].Percentage.ShouldBe(25m);
        bom.Items[2].Quantity.ShouldBe(50m);
        bom.TotalMaterialCost.ShouldBe(80m * 100m + 70m * 100m + 50m * 100m);
    }

    [Fact]
    public void Bom_PercentageFormulation_WhenTotalNot100_ThrowsException()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-002", Guid.NewGuid())
        {
            Quantity = 100m,
            SetQtyBasedOnPercentage = true
        };

        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM1", 0, 10m) { Percentage = 40m });
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM2", 0, 10m) { Percentage = 30m });

        var ex = Should.Throw<BusinessException>(() => bom.RecalculateCost());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Bom_PercentageFormulation_MultipleBalanceItems_ThrowsException()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-003", Guid.NewGuid())
        {
            Quantity = 100m,
            SetQtyBasedOnPercentage = true
        };

        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM1", 0, 10m) { IsBalanceItem = true });
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM2", 0, 10m) { IsBalanceItem = true });

        var ex = Should.Throw<BusinessException>(() => bom.RecalculateCost());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Bom_PercentageFormulation_WithTrackSemiFinishedGoods_ThrowsException()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-004", Guid.NewGuid())
        {
            Quantity = 100m,
            SetQtyBasedOnPercentage = true,
            TrackSemiFinishedGoods = true
        };

        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM1", 0, 10m) { Percentage = 100m });

        var ex = Should.Throw<BusinessException>(() => bom.RecalculateCost());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Bom_PercentageFormulation_ZeroPercentageOnNonBalanceItem_ThrowsException()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-005", Guid.NewGuid())
        {
            Quantity = 100m,
            SetQtyBasedOnPercentage = true
        };

        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM1", 0, 10m) { Percentage = 100m });
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM2", 0, 10m) { Percentage = 0m });

        var ex = Should.Throw<BusinessException>(() => bom.RecalculateCost());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }
}
