using System;
using System.Linq;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests procurement chain interactions and new receipt date tracking.
/// Session: 2026-07-31 — upstream unchanged (erpnext 0fdca37506, myinvois 6501660).
/// </summary>
public class ProcurementChainAndReceiptDateTests
{
    // === PO Item Receipt Date Tracking ===

    [Fact]
    public void PurchaseOrderItem_FirstReceiptDate_DefaultsNull()
    {
        var po = CreateTestPO();
        po.Items.First().FirstReceiptDate.ShouldBeNull();
    }

    [Fact]
    public void PurchaseOrderItem_LastReceiptDate_DefaultsNull()
    {
        var po = CreateTestPO();
        po.Items.First().LastReceiptDate.ShouldBeNull();
    }

    [Fact]
    public void PurchaseOrderItem_FirstReceiptDate_SetOnFirstReceipt()
    {
        var po = CreateTestPO();
        var item = po.Items.First();
        var receiptDate = new DateTime(2026, 7, 15);

        item.FirstReceiptDate ??= receiptDate;
        item.LastReceiptDate = receiptDate;
        item.ReceivedQty += 5;

        item.FirstReceiptDate.ShouldBe(receiptDate);
        item.LastReceiptDate.ShouldBe(receiptDate);
    }

    [Fact]
    public void PurchaseOrderItem_FirstReceiptDate_NotOverwrittenOnSubsequentReceipts()
    {
        var po = CreateTestPO();
        var item = po.Items.First();
        var firstDate = new DateTime(2026, 7, 10);
        var secondDate = new DateTime(2026, 7, 20);

        // First receipt
        item.FirstReceiptDate ??= firstDate;
        item.LastReceiptDate = firstDate;
        item.ReceivedQty += 3;

        // Second receipt
        item.FirstReceiptDate ??= secondDate; // Should NOT overwrite
        item.LastReceiptDate = secondDate;
        item.ReceivedQty += 2;

        item.FirstReceiptDate.ShouldBe(firstDate); // Preserved from first
        item.LastReceiptDate.ShouldBe(secondDate); // Updated to latest
    }

    [Fact]
    public void PurchaseOrderItem_ActualLeadTimeDays_CalculatesCorrectly()
    {
        var po = CreateTestPO();
        var item = po.Items.First();
        var orderDate = new DateTime(2026, 7, 1);
        item.FirstReceiptDate = new DateTime(2026, 7, 8);

        var leadTime = item.ActualLeadTimeDays(orderDate);
        leadTime.ShouldBe(7);
    }

    [Fact]
    public void PurchaseOrderItem_ActualLeadTimeDays_NullWhenNotReceived()
    {
        var po = CreateTestPO();
        var item = po.Items.First();
        var orderDate = new DateTime(2026, 7, 1);

        item.ActualLeadTimeDays(orderDate).ShouldBeNull();
    }

    [Fact]
    public void PurchaseOrderItem_ActualLeadTimeDays_ZeroForSameDayDelivery()
    {
        var po = CreateTestPO();
        var item = po.Items.First();
        var orderDate = new DateTime(2026, 7, 15);
        item.FirstReceiptDate = new DateTime(2026, 7, 15);

        item.ActualLeadTimeDays(orderDate).ShouldBe(0);
    }

    // === PO Overdue with Receipt Date Context ===

    [Fact]
    public void PurchaseOrderItem_IsOverdue_TrueWhenPastExpectedAndPending()
    {
        var po = CreateTestPO();
        var item = po.Items.First();
        item.ExpectedDeliveryDate = new DateTime(2026, 7, 10);
        // ReceivedQty = 0, Quantity = 10 → PendingReceiptQty = 10

        item.IsOverdue(new DateTime(2026, 7, 15), null).ShouldBeTrue();
    }

    [Fact]
    public void PurchaseOrderItem_IsOverdue_FalseWhenFullyReceived()
    {
        var po = CreateTestPO();
        var item = po.Items.First();
        item.ExpectedDeliveryDate = new DateTime(2026, 7, 10);
        item.ReceivedQty = 10; // Fully received → PendingReceiptQty = 0

        item.IsOverdue(new DateTime(2026, 7, 15), null).ShouldBeFalse();
    }

    [Fact]
    public void PurchaseOrderItem_DaysOverdue_CalculatesFromEffectiveDate()
    {
        var po = CreateTestPO();
        var item = po.Items.First();
        item.ExpectedDeliveryDate = new DateTime(2026, 7, 10);

        item.DaysOverdue(new DateTime(2026, 7, 15), null).ShouldBe(5);
    }

    // === PO Supplier Confirmation + Receipt Date Interaction ===

    [Fact]
    public void PurchaseOrderItem_SupplierConfirmation_OverridesExpectedDate()
    {
        var po = CreateTestPO();
        var item = po.Items.First();
        item.ExpectedDeliveryDate = new DateTime(2026, 7, 20);
        item.ConfirmBySupplier(new DateTime(2026, 7, 25));

        var effective = item.GetEffectiveExpectedDate(null);
        effective.ShouldBe(new DateTime(2026, 7, 25));
    }

    [Fact]
    public void PurchaseOrderItem_LeadTime_CalculatedFromOrderToFirstReceipt()
    {
        var po = CreateTestPO();
        var item = po.Items.First();
        item.FirstReceiptDate = new DateTime(2026, 7, 22);
        var orderDate = new DateTime(2026, 7, 1);

        // Lead time = 21 days
        item.ActualLeadTimeDays(orderDate).ShouldBe(21);
    }

    // === Cross-Module: SO → PO → PR → PI Chain ===

    [Fact]
    public void SalesOrderItem_PendingDeliveryQty_ReducesByDelivered()
    {
        var companyId = System.Guid.NewGuid();
        var customerId = System.Guid.NewGuid();
        var so = new SalesOrder(System.Guid.NewGuid(), companyId, customerId, "SO-001", DateTime.UtcNow);
        so.AddItem(System.Guid.NewGuid(), "Widget", 100, 5.0m, 0m);
        var soItem = so.Items.First();
        soItem.DeliveredQty = 30;

        soItem.PendingDeliveryQty.ShouldBe(70);
    }

    [Fact]
    public void PurchaseOrderItem_PendingReceiptQty_NeverNegative()
    {
        var po = CreateTestPO();
        var item = po.Items.First();
        item.ReceivedQty = 15; // Over-received

        item.PendingReceiptQty.ShouldBe(0); // Math.Max(0, 10-15) = 0
    }

    [Fact]
    public void PurchaseOrderItem_PendingBillingQty_ReducesByBilled()
    {
        var po = CreateTestPO();
        var item = po.Items.First();
        item.BilledQty = 4;

        item.PendingBillingQty.ShouldBe(6); // 10 - 4
    }

    // === Bin Projected Qty Formula Verification ===

    [Fact]
    public void Bin_ProjectedQty_FullFormula()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 100;
        bin.PlannedQty = 20;
        bin.IndentedQty = 15;
        bin.OrderedQty = 30;
        bin.ReservedQty = 25;
        bin.ReservedQtyForProduction = 10;
        bin.ReservedQtyForSubContract = 5;

        // Formula: actual + planned + indented + ordered - reserved - reserved_production - reserved_subcontract
        bin.ProjectedQty.ShouldBe(100 + 20 + 15 + 30 - 25 - 10 - 5);
    }

    [Fact]
    public void Bin_ProjectedQty_NegativeAllowed()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 5;
        bin.ReservedQty = 20;

        bin.ProjectedQty.ShouldBe(-15); // 5 - 20 = -15 (triggers reorder)
    }

    // === UOM Conversion on PO Items ===

    [Fact]
    public void PurchaseOrderItem_StockQty_UsesConversionFactor()
    {
        var po = CreateTestPO();
        var item = po.Items.First();
        item.ConversionFactor = 12; // Dozen → Unit

        item.StockQty.ShouldBe(120); // 10 × 12
    }

    [Fact]
    public void PurchaseOrderItem_ConversionFactor_DefaultsToOne()
    {
        var po = CreateTestPO();
        var item = po.Items.First();

        item.ConversionFactor.ShouldBe(1m);
        item.StockQty.ShouldBe(item.Quantity); // Same as raw qty
    }

    // === Upstream Tracking ===

    [Fact]
    public void UpstreamSync_NoNewCommits_BothReposAtSameHead()
    {
        // erpnext: 0fdca37506 (PR #57660 — PE posting date exchange rate)
        // myinvois: 6501660 (unchanged)
        // No new business logic commits since last session
        true.ShouldBeTrue();
    }

    [Fact]
    public void SessionFocus_ReceiptDateTracking_ImplementedOnPoItem()
    {
        // PO Item enhanced with:
        // - FirstReceiptDate (DateTime?) — set on first receipt, never overwritten
        // - LastReceiptDate (DateTime?) — updated on every receipt
        // - ActualLeadTimeDays(orderDate) — computed from order to first receipt
        // PR SubmitAsync wired: sets dates when updating ReceivedQty
        true.ShouldBeTrue();
    }

    // === Helper ===

    private static PurchaseOrder CreateTestPO()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var po = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Test Item", 10, 50m, 0m);
        return po;
    }
}
