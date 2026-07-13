using System;
using System.Collections.Generic;
using MyERP.Accounting.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Tax.DomainServices;
using MyERP.Tax.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// End-to-end flow tests that verify complete business cycles
/// without database dependencies.
/// </summary>
public class EndToEndFlowTests
{
    private readonly TaxesAndTotalsService _taxService = new();

    [Fact]
    public void PurchaseToSalesCycle_WithMarginAndTax()
    {
        // Purchase 100 widgets at RM10 each
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.UtcNow);
        pi.AddItem(Guid.NewGuid(), "Widget", 100, 10, 0);
        pi.Submit();
        pi.GrandTotal.ShouldBe(1000m); // cost = RM1000

        // Sell 50 widgets at RM25 each (150% margin)
        var si = new SalesInvoice(Guid.NewGuid(), pi.CompanyId, Guid.NewGuid(), "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Widget", 50, 25, 0);
        si.Submit();
        si.NetTotal.ShouldBe(1250m);

        // Calculate SST 8% on selling price
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 50, Rate = 25, NetAmount = 1250 },
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SI", si.Id, 1, "SST 8%", "On Net Total", 8),
        };
        var totals = _taxService.Calculate(items, taxes);

        totals.GrandTotal.ShouldBe(1350m); // 1250 + 100 tax
        // Gross profit = 1250 - (50*10) = 750
        (si.NetTotal - 50 * 10).ShouldBe(750m);
    }

    [Fact]
    public void StockMovement_ReceiptAndIssue_BinTracking()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var bin = new Bin(Guid.NewGuid(), itemId, warehouseId);

        // Purchase Receipt: 200 units at RM50
        bin.ApplyStockMovement(200, 10000);
        bin.ActualQty.ShouldBe(200);
        bin.ValuationRate.ShouldBe(50);

        // Reserve 80 for customer orders
        bin.ReservedQty = 80;
        bin.ProjectedQty.ShouldBe(120); // 200 - 80

        // Deliver 80 units (stock out)
        bin.ApplyStockMovement(-80, -4000);
        bin.ReservedQty = 0; // reservation cleared after delivery
        bin.ActualQty.ShouldBe(120);
        bin.ProjectedQty.ShouldBe(120);

        // Order 50 more from supplier
        bin.OrderedQty = 50;
        bin.ProjectedQty.ShouldBe(170); // 120 + 50
    }

    [Fact]
    public void PaymentTerms_MultiInstallment_WithPayments()
    {
        // Create invoice for RM12,000
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-100", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Enterprise License", 1, 12000, 0);
        si.Submit();
        si.GrandTotal.ShouldBe(12000m);

        // Generate payment schedule: 40/30/30 split at 0/30/60 days
        var template = new PaymentTermsTemplate(Guid.NewGuid(), "40/30/30");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 40, 0, "Advance"));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 30, 30, "30 days"));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 30, 60, "60 days"));

        var schedule = template.GenerateSchedule(DateTime.UtcNow, 12000);
        schedule.Count.ShouldBe(3);
        schedule[0].PaymentAmount.ShouldBe(4800); // 40%
        schedule[1].PaymentAmount.ShouldBe(3600); // 30%
        schedule[2].PaymentAmount.ShouldBe(3600); // 30%

        // First payment received
        si.AmountPaid = 4800;
        si.OutstandingAmount.ShouldBe(7200);

        // Second payment
        si.AmountPaid = 8400;
        si.OutstandingAmount.ShouldBe(3600);

        // Final payment
        si.AmountPaid = 12000;
        si.OutstandingAmount.ShouldBe(0);
    }

    [Fact]
    public void ManufacturingCycle_BomToWorkOrder()
    {
        var companyId = Guid.NewGuid();

        // Create BOM: 1 Chair = 4 Legs + 1 Seat + 8 Screws
        var bom = new Manufacturing.Entities.BillOfMaterials(
            Guid.NewGuid(), companyId, "BOM-CHAIR-001", Guid.NewGuid());
        bom.Items.Add(new Manufacturing.Entities.BomItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Wooden Leg", 4, 15) { Uom = "Unit" });
        bom.Items.Add(new Manufacturing.Entities.BomItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Seat Panel", 1, 80) { Uom = "Unit" });
        bom.Items.Add(new Manufacturing.Entities.BomItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Screw Pack", 8, 0.50m) { Uom = "Unit" });
        bom.RecalculateCost();

        bom.TotalMaterialCost.ShouldBe(144m); // 60 + 80 + 4

        // Create Work Order for 10 chairs
        var wo = new Manufacturing.Entities.WorkOrder(
            Guid.NewGuid(), companyId, "WO-001", Guid.NewGuid(), bom.Id, 10);

        // Populate required items from BOM with multiplier
        var multiplier = 10m / bom.Quantity;
        foreach (var bi in bom.Items)
        {
            wo.RequiredItems.Add(new Manufacturing.Entities.WorkOrderItem(
                Guid.NewGuid(), wo.Id, bi.ItemId, bi.ItemName, bi.Quantity * multiplier));
        }

        wo.RequiredItems.Count.ShouldBe(3);
        wo.RequiredItems[0].RequiredQuantity.ShouldBe(40); // 4 * 10 legs
        wo.RequiredItems[1].RequiredQuantity.ShouldBe(10); // 1 * 10 seats
        wo.RequiredItems[2].RequiredQuantity.ShouldBe(80); // 8 * 10 screws

        // Submit and start production
        wo.Submit();
        wo.Start();
        wo.Status.ShouldBe(Manufacturing.WorkOrderStatus.InProcess);

        // Record partial production
        wo.RecordProduction(6);
        wo.ProducedQuantity.ShouldBe(6);
        wo.PercentComplete.ShouldBe(60);

        // Complete production
        wo.RecordProduction(4);
        wo.ProducedQuantity.ShouldBe(10);
        wo.Status.ShouldBe(Manufacturing.WorkOrderStatus.Completed);
    }

    [Fact]
    public void MultiCurrencyInvoice_WithExchangeRate()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-USD-001", DateTime.UtcNow);
        si.CurrencyCode = "USD";
        si.ExchangeRate = 4.72m; // 1 USD = 4.72 MYR

        si.AddItem(Guid.NewGuid(), "Consulting - July", 40, 150, 0, "Hour"); // USD 6000

        si.NetTotal.ShouldBe(6000m); // USD
        si.BaseNetTotal.ShouldBe(28320m); // MYR (6000 * 4.72)
        si.BaseGrandTotal.ShouldBe(28320m);

        // Payment in USD
        si.AmountPaid = 6000;
        si.OutstandingAmount.ShouldBe(0);
        // Base outstanding = BaseGrandTotal - (AmountPaid * ExchangeRate)
        si.BaseOutstandingAmount.ShouldBe(0);
    }

    [Fact]
    public void SubcontractingFlow_OrderToReceipt()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var sco = new SubcontractingOrder(
            Guid.NewGuid(), companyId, "SCO-001", DateTime.UtcNow, supplierId);

        // Order 500 units of finished goods at RM5 service charge per unit
        sco.AddItem(new SubcontractingOrderItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Assembled PCB", 500, 5));

        // Supply raw materials
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Blank PCB", 500));
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Capacitor 10uF", 2500));
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Resistor 10K", 5000));

        sco.NetTotal.ShouldBe(2500m); // 500 * 5
        sco.SuppliedItems.Count.ShouldBe(3);

        sco.Submit();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Open);

        // Receive 300 units
        var scr = new SubcontractingReceipt(
            Guid.NewGuid(), companyId, "SCR-001", DateTime.UtcNow, supplierId, sco.Id);
        scr.AddItem(new SubcontractingReceiptItem(
            Guid.NewGuid(), scr.Id, Guid.NewGuid(), "Assembled PCB", 300, 5));

        scr.NetTotal.ShouldBe(1500m);
        scr.Submit();
        scr.Status.ShouldBe(SubcontractingReceiptStatus.Submitted);
    }
}
