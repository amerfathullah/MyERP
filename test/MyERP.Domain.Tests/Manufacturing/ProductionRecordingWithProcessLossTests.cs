using System;
using System.Linq;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Manufacturing;

public class ProductionRecordingWithProcessLossTests
{
    private static WorkOrder CreateWo(decimal qty = 100)
    {
        return new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-TEST-001",
            Guid.NewGuid(), Guid.NewGuid(), qty);
    }

    [Fact]
    public void RecordProduction_WithoutProcessLoss_IncreasesProducedQty()
    {
        var wo = CreateWo(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50, overproductionPercentage: 5);
        wo.ProducedQuantity.ShouldBe(50);
    }

    [Fact]
    public void RecordProduction_WithProcessLoss_DoesNotCountLossAsProduced()
    {
        var wo = CreateWo(100);
        wo.Submit();
        wo.Start();
        // Record 80 good + 10 process loss = 90 total FG consumed from BOM
        // But only 80 counts toward produced quantity
        wo.RecordProduction(80, overproductionPercentage: 5);
        wo.ProducedQuantity.ShouldBe(80);
    }

    [Fact]
    public void PendingQty_ReducesWithProduction()
    {
        var wo = CreateWo(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(30, overproductionPercentage: 5);

        var pending = wo.Quantity - wo.ProducedQuantity;
        pending.ShouldBe(70);
    }

    [Fact]
    public void PercentComplete_ReflectsProducedVsTotal()
    {
        var wo = CreateWo(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50, overproductionPercentage: 5);

        wo.PercentComplete.ShouldBe(50);
    }

    [Fact]
    public void PercentComplete_CapsAt100()
    {
        var wo = CreateWo(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100, overproductionPercentage: 5);

        wo.PercentComplete.ShouldBe(100);
    }

    [Fact]
    public void OverproductionBlocked_WithZeroTolerance()
    {
        var wo = CreateWo(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100, overproductionPercentage: 0);

        Should.Throw<BusinessException>(() =>
            wo.RecordProduction(1, overproductionPercentage: 0));
    }

    [Fact]
    public void OverproductionAllowed_WithinTolerance()
    {
        var wo = CreateWo(100);
        wo.Submit();
        wo.Start();
        // 5% overproduction allowed: max = 100 * 1.05 = 105
        wo.RecordProduction(105, overproductionPercentage: 5);
        wo.ProducedQuantity.ShouldBe(105);
    }

    [Fact]
    public void AutoComplete_WhenFullyProduced()
    {
        var wo = CreateWo(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100, overproductionPercentage: 5);

        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void PartialProduction_StaysInProcess()
    {
        var wo = CreateWo(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50, overproductionPercentage: 5);

        wo.Status.ShouldBe(WorkOrderStatus.InProcess);
    }

    [Fact]
    public void CumulativeProduction_TracksAcrossMultipleRuns()
    {
        var wo = CreateWo(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(30, overproductionPercentage: 5);
        wo.RecordProduction(40, overproductionPercentage: 5);
        wo.RecordProduction(30, overproductionPercentage: 5);

        wo.ProducedQuantity.ShouldBe(100);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void ZeroQtyWo_NoDivisionError()
    {
        var wo = CreateWo(0);
        wo.PercentComplete.ShouldBe(0);
    }

    [Theory]
    [InlineData("DefectiveScrapQty")]
    [InlineData("TotalFG")]
    [InlineData("Good")]
    [InlineData("Loss")]
    public void LocalizationKey_ExistsInEnJson(string key)
    {
        var jsonPath = System.IO.Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var content = System.IO.File.ReadAllText(jsonPath);
        content.ShouldContain($"\"{key}\"");
    }

    [Fact]
    public void ProcessLossQty_DefaultsZero_OnWorkOrderRequiredItem()
    {
        var wo = CreateWo(100);
        wo.ProcessLossQty.ShouldBe(0);
    }

    [Fact]
    public void ProductionDialog_PendingQty_CalculatesCorrectly()
    {
        var wo = CreateWo(200);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(75, overproductionPercentage: 10);

        var pending = wo.Quantity - wo.ProducedQuantity;
        pending.ShouldBe(125);
    }

    [Fact]
    public void CalculateSecondaryItemOutputs_ProportionallyScalesByFgQty()
    {
        var woRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<WorkOrder, Guid>>();
        var service = new MyERP.Manufacturing.Services.WorkOrderProductionService(woRepo);

        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid())
        {
            Quantity = 10,
            ScrapWarehouseId = Guid.NewGuid(),
            TargetWarehouseId = Guid.NewGuid()
        };

        var scrapItemId = Guid.NewGuid();
        var coProductItemId = Guid.NewGuid();

        bom.AddSecondaryItem(new BomSecondaryItem(Guid.NewGuid(), bom.Id, scrapItemId, SecondaryItemType.Scrap, 2m)
        {
            ItemName = "Metal Shavings",
            Rate = 5m
        });

        bom.AddSecondaryItem(new BomSecondaryItem(Guid.NewGuid(), bom.Id, coProductItemId, SecondaryItemType.CoProduct, 4m)
        {
            ItemName = "By-product Oil",
            Rate = 12m,
            CostAllocationPercentage = 15m
        });

        // Produce 5 units (50% of BOM qty 10)
        var outputs = service.CalculateSecondaryItemOutputs(bom, 5m);

        outputs.Count.ShouldBe(2);

        var scrapOutput = outputs.First(o => o.ItemId == scrapItemId);
        scrapOutput.Quantity.ShouldBe(1m); // 2 * (5/10) = 1
        scrapOutput.SecondaryItemType.ShouldBe(SecondaryItemType.Scrap);
        scrapOutput.WarehouseId.ShouldBe(bom.ScrapWarehouseId);

        var coProductOutput = outputs.First(o => o.ItemId == coProductItemId);
        coProductOutput.Quantity.ShouldBe(2m); // 4 * (5/10) = 2
        coProductOutput.CostAllocationPercentage.ShouldBe(15m);
        coProductOutput.WarehouseId.ShouldBe(bom.TargetWarehouseId);
    }

    [Fact]
    public void CalculateRawMaterialConsumption_PreservesOriginalItemIdAttribution()
    {
        var woRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<WorkOrder, Guid>>();
        var service = new MyERP.Manufacturing.Services.WorkOrderProductionService(woRepo);

        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), quantity: 10);
        var originalItemId = Guid.NewGuid();
        var alternativeItemId = Guid.NewGuid();

        var directItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "Direct Item", 10m)
        {
            TransferredQuantity = 10m
        };

        var alternativeItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, alternativeItemId, "Alternative Item", 20m)
        {
            TransferredQuantity = 20m,
            IsAlternativeItem = true,
            OriginalItemId = originalItemId
        };

        wo.RequiredItems.Add(directItem);
        wo.RequiredItems.Add(alternativeItem);

        var consumptions = service.CalculateRawMaterialConsumption(wo, 5m, "Material Transferred");

        consumptions.Count.ShouldBe(2);

        var directConsumption = consumptions.First(c => c.ItemId == directItem.ItemId);
        directConsumption.OriginalItemId.ShouldBeNull();
        directConsumption.Quantity.ShouldBe(5m);

        var altConsumption = consumptions.First(c => c.ItemId == alternativeItemId);
        altConsumption.OriginalItemId.ShouldBe(originalItemId);
        altConsumption.Quantity.ShouldBe(10m);
    }
}
