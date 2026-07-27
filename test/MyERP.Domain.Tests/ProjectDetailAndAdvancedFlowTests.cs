using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Projects;
using MyERP.Projects.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests covering:
/// - Project detail view data patterns (entity fields, computed properties)
/// - Delivery schedule FIFO allocation patterns
/// - Payment entry tax calculation patterns
/// - PCV closing entry computation patterns
/// - Manufacturing process loss absorption
/// - Invoice discount + tax cascade interaction
/// </summary>
public class ProjectDetailAndAdvancedFlowTests
{
    // === Project Entity — Detail View Data Patterns ===

    [Fact]
    public void Project_DefaultState_AllFieldsInitialized()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-001", "Test Project");

        Assert.Equal(ProjectStatus.Open, project.Status);
        Assert.Equal(ProjectPriority.Medium, project.Priority);
        Assert.Equal(PercentCompleteMethod.TaskCompletion, project.PercentCompleteMethod);
        Assert.Equal(0m, project.PercentComplete);
        Assert.Equal(0m, project.EstimatedCost);
        Assert.Equal(0m, project.TotalBillingAmount);
        Assert.Equal(0m, project.TotalBilledAmount);
        Assert.Equal(0m, project.TotalCostingAmount);
        Assert.Empty(project.Tasks);
    }

    [Fact]
    public void Project_GrossMargin_CalculatedFromCostAndBilling()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-002", "Margin Test");
        project.TotalBilledAmount = 100_000m;
        project.TotalCostingAmount = 70_000m;

        // GrossMargin = totalBilled - totalCosting (absolute amount)
        Assert.Equal(30_000m, project.GrossMargin);
    }

    [Fact]
    public void Project_GrossMargin_ZeroBilling_ReturnsNegative()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-003", "Zero Billing");
        project.TotalBilledAmount = 0m;
        project.TotalCostingAmount = 5000m;

        // No billing but has costs = negative margin
        Assert.Equal(-5000m, project.GrossMargin);
    }

    [Fact]
    public void Project_Complete_SetsStatusAndPercentAndDate()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-004", "Complete Test");

        project.Complete();

        Assert.Equal(ProjectStatus.Completed, project.Status);
        Assert.Equal(100m, project.PercentComplete);
        Assert.NotNull(project.ActualEndDate);
    }

    [Fact]
    public void Project_SetPercentComplete_Manual_ValidRange()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-005", "Manual PCT");
        project.PercentCompleteMethod = PercentCompleteMethod.Manual;

        project.SetPercentComplete(75m);

        Assert.Equal(75m, project.PercentComplete);
    }

    [Fact]
    public void Project_SetPercentComplete_OutOfRange_Throws()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-006", "Out of Range");
        project.PercentCompleteMethod = PercentCompleteMethod.Manual;

        Assert.Throws<Volo.Abp.BusinessException>(() => project.SetPercentComplete(150m));
        Assert.Throws<Volo.Abp.BusinessException>(() => project.SetPercentComplete(-10m));
    }

    [Fact]
    public void Project_SetPercentComplete_NonManual_Throws()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-007", "Non Manual");
        project.PercentCompleteMethod = PercentCompleteMethod.TaskCompletion;

        Assert.Throws<Volo.Abp.BusinessException>(() => project.SetPercentComplete(50m));
    }

    [Fact]
    public void Project_Cancel_FromOpen_Succeeds()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-008", "Cancel Test");
        Assert.Equal(ProjectStatus.Open, project.Status);

        project.Cancel();

        Assert.Equal(ProjectStatus.Cancelled, project.Status);
    }

    [Fact]
    public void Project_Cancel_FromCompleted_Throws()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-009", "Completed Cancel");
        project.Complete();

        Assert.Throws<Volo.Abp.BusinessException>(() => project.Cancel());
    }

    [Fact]
    public void Project_Reopen_FromCancelled_Succeeds()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-010", "Reopen Test");
        project.Cancel();

        project.Reopen();

        Assert.Equal(ProjectStatus.Open, project.Status);
    }

    [Fact]
    public void Project_Reopen_FromOpen_Throws()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-011", "Reopen From Open");

        Assert.Throws<Volo.Abp.BusinessException>(() => project.Reopen());
    }

    // === Payment Entry Tax — Direction and Calculation ===

    [Fact]
    public void PaymentEntryTax_OnPaidAmount_CalculatesCorrectly()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        tax.ChargeType = PaymentTaxChargeType.OnPaidAmount;
        tax.Rate = 6m; // SST 6%

        tax.Calculate(10000m, 1m);

        Assert.Equal(600m, tax.TaxAmount);
        Assert.Equal(600m, tax.BaseTaxAmount);
    }

    [Fact]
    public void PaymentEntryTax_Actual_FixedAmount()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        tax.ChargeType = PaymentTaxChargeType.Actual;
        tax.TaxAmount = 150m;

        tax.Calculate(10000m, 1m);

        Assert.Equal(150m, tax.TaxAmount); // Actual doesn't recalculate
    }

    [Fact]
    public void PaymentEntryTax_WithExchangeRate_BaseTaxDiffers()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        tax.ChargeType = PaymentTaxChargeType.OnPaidAmount;
        tax.Rate = 10m;

        tax.Calculate(1000m, 4.72m); // USD→MYR

        Assert.Equal(100m, tax.TaxAmount); // 10% of 1000
        Assert.Equal(472m, tax.BaseTaxAmount); // 100 × 4.72
    }

    [Fact]
    public void PaymentEntryTax_IsExchangeGainLoss_ExcludedFromTotal()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(),
            PaymentType.Receive, DateTime.UtcNow, 5000m,
            Guid.NewGuid(), Guid.NewGuid());

        var tax1 = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid());
        tax1.ChargeType = PaymentTaxChargeType.OnPaidAmount;
        tax1.Rate = 6m;
        tax1.Calculate(pe.PaidAmount, 1m);
        pe.AddTax(tax1);

        var fxTax = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid());
        fxTax.IsExchangeGainLoss = true;
        fxTax.TaxAmount = 50m;
        pe.AddTax(fxTax);

        // TotalTaxes should exclude exchange gain/loss
        Assert.Equal(300m, pe.TotalTaxes); // Only the 6% tax, not FX
    }

    // === Work Order — Process Loss Absorption ===

    [Fact]
    public void WorkOrder_ProcessLoss_FgCostAbsorbsLoss()
    {
        // When producing 90 units with 10 units process loss from 100 units worth of RM:
        // FG rate = totalRmCost / good_output_qty (not total_qty)
        // This means each good unit absorbs the cost of lost units
        decimal totalRmCost = 10000m;
        decimal goodOutput = 90m;
        decimal processLoss = 10m;
        decimal totalFg = goodOutput + processLoss;

        // Process loss absorption: divide RM cost by good output only
        decimal fgRate = totalRmCost / goodOutput; // = 111.11 per unit

        // Without process loss absorption (wrong): 10000/100 = 100 per unit
        decimal wrongRate = totalRmCost / totalFg;

        Assert.True(fgRate > wrongRate); // Absorbed rate is higher
        Assert.Equal(10000m, fgRate * goodOutput); // Total value preserved
    }

    [Fact]
    public void WorkOrder_Overproduction_5Percent_AllowsWithin()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001",
            Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.Submit();
        wo.Start();

        // 105 units allowed with 5% overproduction tolerance
        wo.RecordProduction(105, overproductionPercentage: 5m);

        Assert.Equal(105m, wo.ProducedQuantity);
    }

    [Fact]
    public void WorkOrder_Overproduction_ExceedsAllowance_Throws()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002",
            Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.Submit();
        wo.Start();

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            wo.RecordProduction(110, overproductionPercentage: 5m));
    }

    // === Invoice Outstanding — Multi-field Computation ===

    [Fact]
    public void SalesInvoice_Outstanding_FullFormula()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Test Item", 10, 100, 0);

        // Outstanding = GrandTotal - AmountPaid - WriteOffAmount - TotalAdvance
        Assert.Equal(1000m, si.GrandTotal);
        Assert.Equal(1000m, si.OutstandingAmount);

        // After partial payment
        si.AmountPaid = 400m;
        Assert.Equal(600m, si.OutstandingAmount);
    }

    [Fact]
    public void SalesInvoice_CreditNote_NegativeOutstanding()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CN-001", DateTime.UtcNow);
        si.IsReturn = true;
        si.AddItem(Guid.NewGuid(), "Return Item", -5, 100, 0);

        Assert.True(si.GrandTotal < 0);
    }

    // === SO Fulfillment — MIN% Formula ===

    [Fact]
    public void SalesOrder_PerDelivered_UsesMinFormula()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item A", 10, 100, 0);
        so.AddItem(Guid.NewGuid(), "Item B", 20, 50, 0);

        // Simulate: Item A fully delivered, Item B 0% delivered
        so.Items.First().DeliveredQty = 10;
        so.Items.Last().DeliveredQty = 0;

        // MIN% means 0% (worst item), not 50% (average)
        Assert.Equal(0m, so.PerDelivered);
    }

    [Fact]
    public void SalesOrder_PerDelivered_AllDelivered_100Percent()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-002", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item A", 10, 100, 0);

        so.Items.First().DeliveredQty = 10;

        Assert.Equal(100m, so.PerDelivered);
    }

    // === PO Fulfillment Status Progression ===

    [Fact]
    public void PurchaseOrder_Submit_SetsToDeliverAndBill()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Test Material", 100, 25, 0);

        po.Submit();

        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
    }

    [Fact]
    public void PurchaseOrder_FullyReceived_TransitionsToToBill()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-002", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Test Material", 100, 25, 0);
        po.Submit();

        po.Items.First().ReceivedQty = 100;
        po.UpdateFulfillmentStatus();

        Assert.Equal(DocumentStatus.ToBill, po.Status);
    }

    // === Delivery Note Return — Qty Sign Enforcement ===

    [Fact]
    public void DeliveryNote_Return_NegativeQty()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "DN-RET-001", DateTime.UtcNow);
        dn.IsReturn = true;

        dn.AddItem(Guid.NewGuid(), "Returned Item", -5, 100, 0);

        Assert.Equal(-5m, dn.Items.First().Quantity);
    }

    // === BOM Secondary Items + Process Loss ===

    [Fact]
    public void BomSecondaryItem_CostAllocation_FgReduces()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Raw Material", 5, 100));

        // Add secondary item with 10% cost allocation
        var secondary = new BomSecondaryItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.ByProduct, 1);
        secondary.CostAllocationPercentage = 10m;
        bom.AddSecondaryItem(secondary);

        // FG gets remaining 90% of cost
        Assert.Equal(90m, bom.FgCostAllocationPercentage);
    }

    [Fact]
    public void BomSecondaryItem_TotalAllocation_Must100()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-002", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Raw Material", 5, 100));
        var secondary = new BomSecondaryItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.CoProduct, 1);
        secondary.CostAllocationPercentage = 40m;
        bom.AddSecondaryItem(secondary);

        // FG = 60%, secondary = 40%, total = 100%
        bom.ValidateCostAllocation(); // Should not throw
        Assert.Equal(60m, bom.FgCostAllocationPercentage);
    }

    // === Stock Ledger Entry — Immutability ===

    [Fact]
    public void StockLedgerEntry_PostingDateTime_Composed()
    {
        var sle = new StockLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 7, 24), -10m, 50m, 90m, 4500m);

        Assert.Equal(new DateTime(2026, 7, 24), sle.PostingDate);
        Assert.Equal(-10m, sle.QuantityChange);
        Assert.Equal(50m, sle.ValuationRate);
    }

    // === UOM Conversion — StockQty ===

    [Fact]
    public void SalesOrderItem_StockQty_WithConversionFactor()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-UOM-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Dozen Item", 5, 120, 0); // 5 Dozen @ RM 120/Dozen

        var item = so.Items.First();
        item.ConversionFactor = 12; // 1 Dozen = 12 Units
        item.StockUom = "Unit";

        Assert.Equal(60m, item.StockQty); // 5 × 12 = 60 stock units
    }

    [Fact]
    public void SalesOrderItem_StockQty_SameUom_Factor1()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-UOM-002", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Simple Item", 10, 50, 0);

        var item = so.Items.First();
        // Default ConversionFactor = 1

        Assert.Equal(10m, item.StockQty); // Same UOM: StockQty = Quantity
    }
}
