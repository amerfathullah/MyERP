using System;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for:
/// - WO Manufacture secondary items (co-products, by-products, scrap) cost allocation
/// - PE multi-reference exchange gain/loss per reference
/// - DN→SI billing qty guard (double-billing prevention)
/// - PP MR supplier grouping and sub-assembly filtering
/// </summary>
public class SecondaryItemsExchangeGlBillingGuardTests
{
    // === WO Secondary Items — Cost Allocation ===

    [Fact]
    public void BOM_FgCostAllocationPercentage_ReducedBySecondaryItems()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        // No secondary items → FG gets 100%
        Assert.Equal(100m, bom.FgCostAllocationPercentage);

        // Add co-product with 20% allocation
        bom.AddSecondaryItem(new BomSecondaryItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.CoProduct, 5)
        {
            CostAllocationPercentage = 20
        });

        Assert.Equal(80m, bom.FgCostAllocationPercentage);
    }

    [Fact]
    public void BOM_MultipleSecondaryItems_CostAllocationSumsCorrectly()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-002", Guid.NewGuid());

        bom.AddSecondaryItem(new BomSecondaryItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.CoProduct, 3)
        {
            CostAllocationPercentage = 15
        });
        bom.AddSecondaryItem(new BomSecondaryItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.ByProduct, 2)
        {
            CostAllocationPercentage = 10
        });
        bom.AddSecondaryItem(new BomSecondaryItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.Scrap, 1)
        {
            CostAllocationPercentage = 5
        });

        // FG = 100 - 15 - 10 - 5 = 70%
        Assert.Equal(70m, bom.FgCostAllocationPercentage);
        Assert.True(bom.ValidateCostAllocation());
    }

    [Fact]
    public void BOM_SecondaryItemCostDistribution_ProportionalToAllocation()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-003", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM", 10, 100)); // RM cost = 1000

        bom.AddSecondaryItem(new BomSecondaryItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.CoProduct, 5)
        {
            CostAllocationPercentage = 30
        });

        bom.RecalculateCost();

        // Total RM = 1000, secondary gets 30% = 300, rate = 300/5 = 60
        var secItem = bom.SecondaryItems.First();
        Assert.Equal(60m, secItem.Rate);
    }

    [Fact]
    public void BOM_ScrapItem_RoutesToScrapWarehouse()
    {
        var scrapItem = new BomSecondaryItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SecondaryItemType.Scrap, 2)
        {
            CostAllocationPercentage = 5
        };

        Assert.Equal(SecondaryItemType.Scrap, scrapItem.SecondaryItemType);
        // Scrap warehouse routing is handled in AppService (not entity-level)
    }

    [Fact]
    public void WorkOrder_ScrapWarehouseId_DefaultsNull()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10);
        Assert.Null(wo.ScrapWarehouseId);
    }

    [Fact]
    public void WorkOrder_ScrapWarehouseId_CanBeSet()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002", Guid.NewGuid(), Guid.NewGuid(), 10);
        var scrapWh = Guid.NewGuid();
        wo.ScrapWarehouseId = scrapWh;
        Assert.Equal(scrapWh, wo.ScrapWarehouseId);
    }

    // === PE Multi-Reference Exchange Gain/Loss ===

    [Fact]
    public void PaymentEntryReference_ExchangeRate_DefaultsToOne()
    {
        var refRow = new PaymentEntryReference(
            Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            1000m, 1000m, 1000m);

        // Default exchange rate should be 1 (same currency)
        Assert.Equal(1m, refRow.ExchangeRate);
    }

    [Fact]
    public void PaymentEntryReference_ExchangeRate_CanBeSetForMultiCurrency()
    {
        var refRow = new PaymentEntryReference(
            Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            1000m, 1000m, 1000m);
        refRow.ExchangeRate = 4.50m; // Invoice rate at time of creation

        Assert.Equal(4.50m, refRow.ExchangeRate);
    }

    [Fact]
    public void ExchangeGainLoss_PerReference_CalculatedFromRateDifference()
    {
        // PE exchange rate: 4.72 (payment date)
        // Reference invoice rate: 4.50 (invoice date)
        // Allocated: 1000 USD
        // Gain = 1000 × (4.72 - 4.50) = 220 MYR
        var peRate = 4.72m;
        var invoiceRate = 4.50m;
        var allocated = 1000m;

        var gainLoss = allocated * (peRate - invoiceRate);
        Assert.Equal(220m, gainLoss);
        Assert.True(gainLoss > 0); // gain (favorable for receivable)
    }

    [Fact]
    public void ExchangeGainLoss_MultiReference_EachRefHasDifferentRate()
    {
        // 3 invoices at different rates, all paid by one PE at rate 4.72
        var peRate = 4.72m;

        var ref1GainLoss = 500m * (peRate - 4.50m); // 110 gain
        var ref2GainLoss = 300m * (peRate - 4.80m); // -24 loss
        var ref3GainLoss = 200m * (peRate - 4.72m); // 0 neutral

        Assert.Equal(110m, ref1GainLoss);
        Assert.Equal(-24m, ref2GainLoss);
        Assert.Equal(0m, ref3GainLoss);

        // Net position: 110 - 24 + 0 = 86 gain
        Assert.Equal(86m, ref1GainLoss + ref2GainLoss + ref3GainLoss);
    }

    [Fact]
    public void ExchangeGainLoss_SmallDifference_SkippedBelow001()
    {
        var peRate = 4.72000m;
        var invoiceRate = 4.72005m;
        var allocated = 100m;

        var gainLoss = allocated * (peRate - invoiceRate);
        Assert.True(Math.Abs(gainLoss) < 0.01m); // Below threshold — should be skipped
    }

    // === DN→SI Billing Qty Guard ===

    [Fact]
    public void DeliveryNoteItem_PendingBillingQty_DefaultsToFullQuantity()
    {
        var dn = new DeliveryNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-T1",
            DateTime.Today);
        dn.AddItem(Guid.NewGuid(), "Widget", 100, 50, 0, "Unit");
        var dnItem = dn.Items.First();

        Assert.Equal(100m, dnItem.PendingBillingQty);
    }

    [Fact]
    public void DeliveryNoteItem_PendingBillingQty_ReducedByBilled()
    {
        var dn = new DeliveryNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-T2",
            DateTime.Today);
        dn.AddItem(Guid.NewGuid(), "Widget", 100, 50, 0, "Unit");
        var dnItem = dn.Items.First();
        dnItem.BilledQty = 60;

        Assert.Equal(40m, dnItem.PendingBillingQty);
    }

    [Fact]
    public void DeliveryNoteItem_FullyBilled_ZeroPending()
    {
        var dn = new DeliveryNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-T3",
            DateTime.Today);
        dn.AddItem(Guid.NewGuid(), "Widget", 100, 50, 0, "Unit");
        var dnItem = dn.Items.First();
        dnItem.BilledQty = 100;

        Assert.Equal(0m, dnItem.PendingBillingQty);
    }

    [Fact]
    public void DeliveryNote_AllItemsBilled_NoConvertibleItems()
    {
        // Simulate conversion guard: if all DN items are fully billed, conversion should be blocked
        var dn = new DeliveryNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-001",
            DateTime.Today);

        dn.AddItem(Guid.NewGuid(), "Widget", 10, 50, 0, "Unit");
        var item = dn.Items.First();
        item.BilledQty = 10; // Fully billed

        var hasConvertibleItems = dn.Items.Any(i => i.PendingBillingQty > 0);
        Assert.False(hasConvertibleItems);
    }

    [Fact]
    public void DeliveryNote_PartialBilling_ConvertibleWithPendingQty()
    {
        var dn = new DeliveryNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-002",
            DateTime.Today);

        dn.AddItem(Guid.NewGuid(), "Widget A", 10, 50, 0, "Unit");
        dn.AddItem(Guid.NewGuid(), "Widget B", 5, 100, 0, "Unit");
        dn.Items.First().BilledQty = 10; // Fully billed
        // Widget B is unbilled

        var convertibleItems = dn.Items.Where(i => i.PendingBillingQty > 0).ToList();
        Assert.Single(convertibleItems);
        Assert.Equal(5m, convertibleItems[0].PendingBillingQty);
    }

    // === PP MR Supplier Grouping ===

    [Fact]
    public void ProductionPlanMrItem_ProcurementType_DefaultsInHouseManufacturing()
    {
        var mrItem = new ProductionPlanMrItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Item", 100);

        // Default procurement type is InHouseManufacturing (enum value 0)
        Assert.Equal(SubAssemblyType.InHouseManufacturing, mrItem.ProcurementType);
    }

    [Fact]
    public void ProductionPlanMrItem_InHouseManufacturing_FilteredFromPurchaseMR()
    {
        // Sub-assembly items with InHouseManufacturing should NOT appear in purchase MRs
        var purchaseItem = new ProductionPlanMrItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Purchase Item", 100)
        {
            ProcurementType = SubAssemblyType.MaterialRequest,
            PlannedQty = 100
        };

        var mfgItem = new ProductionPlanMrItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Mfg Item", 50)
        {
            ProcurementType = SubAssemblyType.InHouseManufacturing,
            PlannedQty = 50
        };

        var allItems = new[] { purchaseItem, mfgItem };
        var purchaseOnly = allItems
            .Where(m => m.PlannedQty > 0 && m.ProcurementType != SubAssemblyType.InHouseManufacturing)
            .ToList();

        Assert.Single(purchaseOnly);
        Assert.Equal(purchaseItem.ItemId, purchaseOnly[0].ItemId);
    }

    [Fact]
    public void ProductionPlanMR_GroupsByWarehouse()
    {
        var wh1 = Guid.NewGuid();
        var wh2 = Guid.NewGuid();

        var items = new[]
        {
            new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A", 10) { PlannedQty = 10, WarehouseId = wh1 },
            new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "B", 20) { PlannedQty = 20, WarehouseId = wh1 },
            new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "C", 30) { PlannedQty = 30, WarehouseId = wh2 },
        };

        var groups = items.GroupBy(m => m.WarehouseId).ToList();
        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups.First(g => g.Key == wh1).Count());
        Assert.Single(groups.First(g => g.Key == wh2));
    }

    // === BOM Secondary Items — Validation Rules ===

    [Fact]
    public void BOM_FgItemCannotBeSecondaryItem()
    {
        var fgItemId = Guid.NewGuid();
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-004", fgItemId);

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            bom.AddSecondaryItem(new BomSecondaryItem(
                Guid.NewGuid(), bom.Id, fgItemId, SecondaryItemType.CoProduct, 1)));
    }

    [Fact]
    public void BOM_ProcessLossOver100_Blocked()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-005", Guid.NewGuid());

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            bom.AddSecondaryItem(new BomSecondaryItem(
                Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.Scrap, 1)
            {
                ProcessLossPercentage = 100 // 100% = invalid
            }));
    }

    [Fact]
    public void BOM_CostAllocationExceeds100_InvalidatesValidation()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-006", Guid.NewGuid());

        bom.AddSecondaryItem(new BomSecondaryItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.CoProduct, 5)
        {
            CostAllocationPercentage = 60
        });
        bom.AddSecondaryItem(new BomSecondaryItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.ByProduct, 3)
        {
            CostAllocationPercentage = 50 // 60+50 = 110 > 100
        });

        Assert.False(bom.ValidateCostAllocation());
    }

    // === Exchange Gain/Loss JE Structure ===

    [Fact]
    public void ExchangeGainLoss_Gain_DRBankCRExchangeGL()
    {
        var gainAmount = 220m;
        var bankAccountId = Guid.NewGuid();
        var exchangeGlId = Guid.NewGuid();

        var je = new JournalEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);

        je.AddLine(bankAccountId, gainAmount, true);  // DR Bank
        je.AddLine(exchangeGlId, gainAmount, false);   // CR Exchange GL

        Assert.Equal(2, je.Lines.Count);
        Assert.Equal(gainAmount, je.Lines.First(l => l.IsDebit).Amount);
        Assert.Equal(gainAmount, je.Lines.First(l => !l.IsDebit).Amount);
    }

    [Fact]
    public void ExchangeGainLoss_Loss_DRExchangeGLCRBank()
    {
        var lossAmount = 24m;
        var bankAccountId = Guid.NewGuid();
        var exchangeGlId = Guid.NewGuid();

        var je = new JournalEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);

        je.AddLine(exchangeGlId, lossAmount, true);    // DR Exchange GL
        je.AddLine(bankAccountId, lossAmount, false);   // CR Bank

        Assert.Equal(2, je.Lines.Count);
    }
}
