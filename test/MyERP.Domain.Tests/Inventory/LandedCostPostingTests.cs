using System;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Inventory;

public class LandedCostPostingTests
{
    private static LandedCostVoucher CreateLcv()
    {
        return new LandedCostVoucher(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
    }

    [Fact]
    public void LCV_SubmitDistributes_ByAmount()
    {
        var lcv = CreateLcv();
        lcv.DistributionMethod = LandedCostDistributionMethod.BasedOnAmount;

        // Item 1: amount 800, Item 2: amount 200 → 80% / 20% split
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 800m);
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 5, 200m);
        lcv.AddCharge("Freight", Guid.NewGuid(), 100m);

        lcv.Submit();

        lcv.Items[0].ApplicableCharges.ShouldBe(80m); // 80% of 100
        lcv.Items[1].ApplicableCharges.ShouldBe(20m); // 20% of 100
        lcv.TotalDistributedAmount.ShouldBe(100m);
    }

    [Fact]
    public void LCV_SubmitDistributes_ByQuantity()
    {
        var lcv = CreateLcv();
        lcv.DistributionMethod = LandedCostDistributionMethod.BasedOnQuantity;

        // Item 1: qty 10, Item 2: qty 5 → 66.67% / 33.33% split
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 500m);
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 5, 500m);
        lcv.AddCharge("Insurance", Guid.NewGuid(), 150m);

        lcv.Submit();

        lcv.Items[0].ApplicableCharges.ShouldBe(100m); // 10/15 * 150
        lcv.Items[1].ApplicableCharges.ShouldBe(50m);  // 5/15 * 150
    }

    [Fact]
    public void LCV_ErrorDiffusion_LastItemAbsorbsRounding()
    {
        var lcv = CreateLcv();
        lcv.DistributionMethod = LandedCostDistributionMethod.BasedOnAmount;

        // Three items that cause rounding: 100/3 = 33.33...
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 1, 100m);
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 1, 100m);
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 1, 100m);
        lcv.AddCharge("Customs", Guid.NewGuid(), 100m);

        lcv.Submit();

        // Total distributed must exactly equal total charges
        lcv.TotalDistributedAmount.ShouldBe(100m);
    }

    [Fact]
    public void LCV_Submit_StatusTransitions()
    {
        var lcv = CreateLcv();
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 1000m);
        lcv.AddCharge("Freight", Guid.NewGuid(), 50m);

        lcv.Status.ShouldBe(DocumentStatus.Draft);
        lcv.Submit();
        lcv.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void LCV_Cancel_AfterSubmit()
    {
        var lcv = CreateLcv();
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 1000m);
        lcv.AddCharge("Freight", Guid.NewGuid(), 50m);

        lcv.Submit();
        lcv.Cancel();
        lcv.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void LCV_SubmitWithNoItems_Throws()
    {
        var lcv = CreateLcv();
        lcv.AddCharge("Freight", Guid.NewGuid(), 50m);

        Should.Throw<Volo.Abp.BusinessException>(() => lcv.Submit());
    }

    [Fact]
    public void LCV_SubmitWithNoCharges_Throws()
    {
        var lcv = CreateLcv();
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 1000m);

        Should.Throw<Volo.Abp.BusinessException>(() => lcv.Submit());
    }
}
