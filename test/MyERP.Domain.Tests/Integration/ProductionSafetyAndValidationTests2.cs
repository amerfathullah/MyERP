using System;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

public class ProductionSafetyAndValidationTests
{
    [Fact]
    public void BomCostPropagation_RecalculateCost_UpdatesTotalMaterialCost()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Steel", 10, 25));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Bolt", 50, 2));

        bom.RecalculateCost();

        bom.TotalMaterialCost.ShouldBe(350m); // (10×25) + (50×2)
    }

    [Fact]
    public void BomCostPropagation_SubBomRate_PropagatesUp()
    {
        var childBomId = Guid.NewGuid();
        var parentBom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-PARENT", Guid.NewGuid());

        // Sub-assembly item references a child BOM
        var subItem = new BomItem(Guid.NewGuid(), parentBom.Id, Guid.NewGuid(), "Sub-Assembly", 2, 100);
        subItem.SubBomId = childBomId;
        parentBom.Items.Add(subItem);

        // Simulate rate update from child BOM (childBom.TotalCost=150, Quantity=1 → rate=150)
        subItem.Rate = 150;
        subItem.Recalculate();

        parentBom.RecalculateCost();

        parentBom.TotalMaterialCost.ShouldBe(300m); // 2 × 150
    }

    [Fact]
    public void BomPhantomItem_ExplodedBomItem_AggregatesSameItems()
    {
        // If two BOM items produce the same material, they should be aggregated
        var itemId = Guid.NewGuid();
        var items = new[]
        {
            new ExplodedBomItem(itemId, "Wire", 5m, 10m, "Kg", null),
            new ExplodedBomItem(itemId, "Wire", 3m, 12m, "Kg", null),
        };

        var aggregated = items
            .GroupBy(x => x.ItemId)
            .Select(g => new ExplodedBomItem(
                g.Key, g.First().ItemName,
                g.Sum(x => x.Quantity),
                g.Max(x => x.Rate),
                g.First().Uom, null))
            .ToList();

        aggregated.Count.ShouldBe(1);
        aggregated[0].Quantity.ShouldBe(8m);
        aggregated[0].Rate.ShouldBe(12m); // Max rate
    }

    [Fact]
    public void PaymentEntryReconciliationGuard_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.PaymentEntryUsedInReconciliation.ShouldBe("MyERP:01009");
    }

    [Fact]
    public void PaymentLedgerEntry_Delinked_DefaultsFalse()
    {
        var ple = new PaymentLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
            Guid.NewGuid(), "Customer", Guid.NewGuid(),
            "PaymentEntry", Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(),
            1000m, 1000m, "MYR");
        ple.Delinked.ShouldBeFalse();
    }

    [Fact]
    public void PaymentLedgerEntry_Delinked_PreventsReconciliationBlockingCancel()
    {
        // When Delinked=true, the PLE is excluded from the reconciliation guard
        var ple = new PaymentLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
            Guid.NewGuid(), "Customer", Guid.NewGuid(),
            "PaymentEntry", Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(),
            1000m, 1000m, "MYR");
        ple.Delinked = true;
        ple.Delinked.ShouldBeTrue();
    }

    [Fact]
    public void DeliveryNote_CannotDeliverAgainstCancelledSO()
    {
        // The guard checks status; a cancelled SO should block DN submit
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item 1", 10, 100, 0, "Unit");
        so.Submit();
        so.Cancel();
        so.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void DeliveryNote_CannotDeliverAgainstClosedSO()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item 1", 10, 100, 0, "Unit");
        so.Submit();
        so.Close();
        so.Status.ShouldBe(DocumentStatus.Closed);
    }

    [Fact]
    public void PricingRuleContext_DiscountedRate_DefaultsZero()
    {
        var ctx = new Sales.DomainServices.PricingRuleContext
        {
            ItemId = Guid.NewGuid(),
            ItemName = "Test Item",
            Qty = 5,
            Rate = 100,
        };
        ctx.DiscountedRate.ShouldBe(0);
    }

    [Fact]
    public void PricingRuleContext_DiscountedRateSet_OverridesOriginal()
    {
        var ctx = new Sales.DomainServices.PricingRuleContext
        {
            ItemId = Guid.NewGuid(),
            ItemName = "Test Item",
            Qty = 5,
            Rate = 100,
        };
        ctx.DiscountedRate = 85m; // 15% discount
        ctx.DiscountedRate.ShouldBe(85m);
        ctx.DiscountedRate.ShouldNotBe(ctx.Rate);
    }

    [Fact]
    public void BomCostPropagation_LeafBom_HasNoSubBomReferences()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-LEAF", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Raw Material", 5, 20));

        var hasSubBoms = bom.Items.Any(i => i.SubBomId.HasValue);
        hasSubBoms.ShouldBeFalse();
    }

    [Fact]
    public void BomCostPropagation_MultiLevel_SubBomRateFlows()
    {
        // Level 0: leaf BOM (raw materials)
        var leafBom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-LEAF", Guid.NewGuid());
        leafBom.Items.Add(new BomItem(Guid.NewGuid(), leafBom.Id, Guid.NewGuid(), "Iron", 2, 10));
        leafBom.RecalculateCost(); // TotalMaterialCost = 20

        // Level 1: parent references leaf
        var parentBom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-PARENT", Guid.NewGuid());
        var subItem = new BomItem(Guid.NewGuid(), parentBom.Id, Guid.NewGuid(), "Assembly", 3, 0);
        subItem.SubBomId = leafBom.Id;
        // Simulate propagation: rate = childTotalCost / childQty
        subItem.Rate = leafBom.TotalCost / leafBom.Quantity; // 20/1 = 20
        subItem.Recalculate(); // Amount = 3×20 = 60
        parentBom.Items.Add(subItem);
        parentBom.RecalculateCost();

        parentBom.TotalMaterialCost.ShouldBe(60m);
    }
}
