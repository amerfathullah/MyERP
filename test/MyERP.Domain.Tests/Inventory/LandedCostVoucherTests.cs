using System;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

public class LandedCostVoucherTests
{
    private static LandedCostVoucher CreateLCV()
        => new(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

    [Fact]
    public void Create_SetsDefaults()
    {
        var lcv = CreateLCV();
        lcv.Status.ShouldBe(Core.DocumentStatus.Draft);
        lcv.DistributionMethod.ShouldBe(LandedCostDistributionMethod.BasedOnAmount);
    }

    [Fact]
    public void DistributeCharges_ByAmount()
    {
        var lcv = CreateLCV();
        lcv.DistributionMethod = LandedCostDistributionMethod.BasedOnAmount;
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 1000m); // 1000 of 2000
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 5, 1000m);  // 1000 of 2000
        lcv.AddCharge("Freight", Guid.NewGuid(), 100m);

        lcv.DistributeCharges();

        lcv.Items[0].ApplicableCharges.ShouldBe(50m);
        lcv.Items[1].ApplicableCharges.ShouldBe(50m);
    }

    [Fact]
    public void DistributeCharges_ByQuantity()
    {
        var lcv = CreateLCV();
        lcv.DistributionMethod = LandedCostDistributionMethod.BasedOnQuantity;
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 30, 1000m); // 30 of 40
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 500m);  // 10 of 40
        lcv.AddCharge("Customs", Guid.NewGuid(), 200m);

        lcv.DistributeCharges();

        lcv.Items[0].ApplicableCharges.ShouldBe(150m); // 200 × 30/40
        lcv.Items[1].ApplicableCharges.ShouldBe(50m);  // 200 × 10/40
    }

    [Fact]
    public void Submit_WithItemsAndCharges_Succeeds()
    {
        var lcv = CreateLCV();
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 1000m);
        lcv.AddCharge("Freight", Guid.NewGuid(), 100m);
        lcv.Submit();
        lcv.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutItems_Throws()
    {
        var lcv = CreateLCV();
        lcv.AddCharge("Freight", Guid.NewGuid(), 100m);
        Should.Throw<BusinessException>(() => lcv.Submit())
            .Code.ShouldBe(MyERPDomainErrorCodes.LandedCostHasNoItems);
    }

    [Fact]
    public void Submit_WithoutCharges_Throws()
    {
        var lcv = CreateLCV();
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 1000m);
        Should.Throw<BusinessException>(() => lcv.Submit())
            .Code.ShouldBe(MyERPDomainErrorCodes.LandedCostHasNoCharges);
    }

    [Fact]
    public void Cancel_Submitted_Succeeds()
    {
        var lcv = CreateLCV();
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 1000m);
        lcv.AddCharge("Freight", Guid.NewGuid(), 100m);
        lcv.Submit();
        lcv.Cancel();
        lcv.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void AddCharge_RejectsZeroAmount()
    {
        var lcv = CreateLCV();
        Should.Throw<ArgumentException>(() => lcv.AddCharge("Freight", Guid.NewGuid(), 0));
    }

    [Fact]
    public void ErrorDiffusion_DistributableRoundingMismatch()
    {
        var lcv = CreateLCV();
        lcv.DistributionMethod = LandedCostDistributionMethod.BasedOnAmount;
        // 3 items with amounts that cause 33.33... distribution
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 100m);
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 100m);
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 100m);
        lcv.AddCharge("Insurance", Guid.NewGuid(), 100m);

        lcv.DistributeCharges();

        // Total must equal 100 exactly (error diffusion corrects the rounding)
        (lcv.Items[0].ApplicableCharges + lcv.Items[1].ApplicableCharges + lcv.Items[2].ApplicableCharges)
            .ShouldBe(100m);
    }

    [Fact]
    public void AddCharge_PreservesDimensions()
    {
        var lcv = CreateLCV();
        var ccId = Guid.NewGuid();
        var projId = Guid.NewGuid();
        lcv.AddCharge("Customs", Guid.NewGuid(), 250m, costCenterId: ccId, projectId: projId);

        lcv.Charges.Count.ShouldBe(1);
        lcv.Charges[0].CostCenterId.ShouldBe(ccId);
        lcv.Charges[0].ProjectId.ShouldBe(projId);
    }

    [Fact]
    public void DistributeCharges_RejectsOffsettingPurchaseAndReturnAmounts()
    {
        // Per ERPNext PR #59274 / commit 102842253b:
        // Offsetting purchase and return amounts summing to 0 must be rejected under BasedOnAmount.
        // Switching to BasedOnQuantity succeeds.
        var lcv = CreateLCV();
        lcv.DistributionMethod = LandedCostDistributionMethod.BasedOnAmount;
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 2, 100m);
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), -1, -100m);
        lcv.AddCharge("Customs", Guid.NewGuid(), 80m);

        var ex = Should.Throw<BusinessException>(() => lcv.DistributeCharges());
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Total Amount of all items is zero. Set 'Distribute Charges Based On' to Qty.");

        lcv.DistributionMethod = LandedCostDistributionMethod.BasedOnQuantity;
        lcv.DistributeCharges();

        lcv.Items[0].ApplicableCharges.ShouldBe(160m);
        lcv.Items[1].ApplicableCharges.ShouldBe(-80m);
        lcv.TotalDistributedAmount.ShouldBe(80m);
    }

    [Fact]
    public void DistributeCharges_RejectsFullyDiscountedPurchase()
    {
        // Per ERPNext PR #59274:
        // Item with 100% discount (0 amount) must be rejected under BasedOnAmount.
        var lcv = CreateLCV();
        lcv.DistributionMethod = LandedCostDistributionMethod.BasedOnAmount;
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 2, 0m);
        lcv.AddCharge("Freight", Guid.NewGuid(), 80m);

        var ex = Should.Throw<BusinessException>(() => lcv.DistributeCharges());
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Total Amount of all items is zero. Set 'Distribute Charges Based On' to Qty.");

        lcv.DistributionMethod = LandedCostDistributionMethod.BasedOnQuantity;
        lcv.DistributeCharges();

        lcv.Items[0].ApplicableCharges.ShouldBe(80m);
        lcv.TotalDistributedAmount.ShouldBe(80m);
    }

    [Fact]
    public void DistributeCharges_RejectsAmountsThatCancelToZeroAfterRounding()
    {
        // Per ERPNext PR #59274:
        // Items whose amounts cancel to zero within currency precision must be rejected.
        var lcv = CreateLCV();
        lcv.DistributionMethod = LandedCostDistributionMethod.BasedOnAmount;
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 1, 100.10m);
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 1, 200.20m);
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 1, -300.30m);
        lcv.AddCharge("Customs", Guid.NewGuid(), 80m);

        var ex = Should.Throw<BusinessException>(() => lcv.DistributeCharges());
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Total Amount of all items is zero. Set 'Distribute Charges Based On' to Qty.");
    }

    [Fact]
    public void DistributeCharges_RejectsZeroTotalQuantity_WhenBasedOnQuantity()
    {
        // Per ERPNext PR #59274:
        // Zero net quantity when distributing based on quantity must throw explicit error.
        var lcv = CreateLCV();
        lcv.DistributionMethod = LandedCostDistributionMethod.BasedOnQuantity;
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 500m);
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), -10, -500m);
        lcv.AddCharge("Freight", Guid.NewGuid(), 100m);

        var ex = Should.Throw<BusinessException>(() => lcv.DistributeCharges());
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Total Quantity of all items is zero. Set 'Distribute Charges Based On' to Amount.");
    }
}
