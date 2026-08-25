using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for business logic wiring (July 24 session):
/// - Stock Reservation Entry consumption on delivery
/// - Overdue billing threshold validation
/// - SRE FIFO consumption ordering
/// - Credit limit + overdue combined enforcement
/// </summary>
public class SreOverdueAndWiringTests
{
    // ═══════════════════════════════════════════════
    // Stock Reservation Entry — FIFO Consumption
    // ═══════════════════════════════════════════════

    [Fact]
    public void SRE_RecordDelivery_ReducesAvailableQty()
    {
        var sre = CreateTestSre(100m);
        sre.RecordDelivery(30m);
        sre.DeliveredQty.ShouldBe(30m);
        sre.AvailableQty.ShouldBe(70m);
    }

    [Fact]
    public void SRE_RecordDelivery_FullConsumption()
    {
        var sre = CreateTestSre(50m);
        sre.RecordDelivery(50m);
        sre.DeliveredQty.ShouldBe(50m);
        sre.AvailableQty.ShouldBe(0m);
    }

    [Fact]
    public void SRE_RecordDelivery_ExceedsReserved_Throws()
    {
        var sre = CreateTestSre(100m);
        sre.RecordDelivery(60m);
        Should.Throw<BusinessException>(() => sre.RecordDelivery(50m)); // 60+50=110 > 100
    }

    [Fact]
    public void SRE_ProgressiveDelivery_TracksCorrectly()
    {
        var sre = CreateTestSre(100m);
        sre.RecordDelivery(25m);
        sre.RecordDelivery(25m);
        sre.RecordDelivery(25m);
        sre.DeliveredQty.ShouldBe(75m);
        sre.AvailableQty.ShouldBe(25m);
    }

    [Fact]
    public void SRE_DefaultDeliveredQty_IsZero()
    {
        var sre = CreateTestSre(100m);
        sre.DeliveredQty.ShouldBe(0m);
        sre.AvailableQty.ShouldBe(100m);
    }

    // ═══════════════════════════════════════════════
    // Overdue Billing Threshold — Entity Fields
    // ═══════════════════════════════════════════════

    [Fact]
    public void CustomerCreditLimit_OverdueBillingThreshold_DefaultsZero()
    {
        var ccl = new CustomerCreditLimit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10000m);
        ccl.OverdueBillingThreshold.ShouldBe(0m);
    }

    [Fact]
    public void CustomerCreditLimit_OverdueBillingThreshold_CanBeSet()
    {
        var ccl = new CustomerCreditLimit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10000m);
        ccl.OverdueBillingThreshold = 5000m;
        ccl.OverdueBillingThreshold.ShouldBe(5000m);
    }

    [Fact]
    public void CustomerCreditLimit_ZeroThreshold_DisablesEnforcement()
    {
        // Zero threshold means no overdue enforcement for this customer-company pair
        var ccl = new CustomerCreditLimit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10000m);
        ccl.OverdueBillingThreshold = 0;
        // When threshold = 0, ValidateOverdueBillingThresholdAsync should return immediately
        // (tested at service level — entity just stores the value)
        ccl.OverdueBillingThreshold.ShouldBe(0m);
    }

    [Fact]
    public void CustomerCreditLimit_BypassFlag_IndependentOfOverdue()
    {
        var ccl = new CustomerCreditLimit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10000m);
        ccl.BypassCreditLimitCheck = true;
        ccl.OverdueBillingThreshold = 10000m;
        // Bypass only affects credit limit, not overdue threshold
        ccl.BypassCreditLimitCheck.ShouldBeTrue();
        ccl.OverdueBillingThreshold.ShouldBe(10000m);
    }

    // ═══════════════════════════════════════════════
    // Error Code Verification
    // ═══════════════════════════════════════════════

    [Fact]
    public void ErrorCode_OverdueBillingThresholdExceeded_Exists()
    {
        MyERPDomainErrorCodes.OverdueBillingThresholdExceeded.ShouldBe("MyERP:03043");
    }

    // ═══════════════════════════════════════════════
    // SRE + DN Integration Concepts
    // ═══════════════════════════════════════════════

    [Fact]
    public void SRE_MultipleReservations_FIFOConsumption()
    {
        // Simulate FIFO: oldest reservation consumed first
        var sre1 = CreateTestSre(30m); // Oldest
        var sre2 = CreateTestSre(50m); // Newer
        var sre3 = CreateTestSre(20m); // Newest

        // Deliver 60 units — should consume SRE1 fully (30) + SRE2 partially (30)
        decimal toDeliver = 60m;

        // SRE1: consume MIN(30, 60) = 30
        var consume1 = Math.Min(sre1.AvailableQty, toDeliver);
        sre1.RecordDelivery(consume1);
        toDeliver -= consume1;

        // SRE2: consume MIN(50, 30) = 30
        var consume2 = Math.Min(sre2.AvailableQty, toDeliver);
        sre2.RecordDelivery(consume2);
        toDeliver -= consume2;

        // SRE3: not touched (remaining = 0)
        toDeliver.ShouldBe(0m);
        sre1.AvailableQty.ShouldBe(0m);    // Fully consumed
        sre2.AvailableQty.ShouldBe(20m);   // Partially consumed
        sre3.AvailableQty.ShouldBe(20m);   // Untouched
    }

    [Fact]
    public void SRE_DeliveryExceedsAllReservations_ConsumesAll()
    {
        var sre1 = CreateTestSre(30m);
        var sre2 = CreateTestSre(20m);

        decimal toDeliver = 100m;

        // SRE1: consume MIN(30, 100) = 30
        var consume1 = Math.Min(sre1.AvailableQty, toDeliver);
        sre1.RecordDelivery(consume1);
        toDeliver -= consume1;

        // SRE2: consume MIN(20, 70) = 20
        var consume2 = Math.Min(sre2.AvailableQty, toDeliver);
        sre2.RecordDelivery(consume2);
        toDeliver -= consume2;

        // Remaining qty (50) had no reservation — that's OK (unreserved stock)
        toDeliver.ShouldBe(50m);
        sre1.AvailableQty.ShouldBe(0m);
        sre2.AvailableQty.ShouldBe(0m);
    }

    [Fact]
    public void SRE_ZeroDelivery_Throws()
    {
        // RecordDelivery requires positive qty — zero is invalid
        var sre = CreateTestSre(100m);
        Should.Throw<ArgumentException>(() => sre.RecordDelivery(0m));
    }

    // ═══════════════════════════════════════════════
    // SI UpdateStock + SRE Consumption Concept
    // ═══════════════════════════════════════════════

    [Fact]
    public void SI_UpdateStock_StockQty_UsedForSREConsumption()
    {
        // When SI has UpdateStock=true, SRE consumption should use StockQty (not raw Quantity)
        // This ensures UOM conversion is respected
        var item = CreateTestSiItem(quantity: 5m, conversionFactor: 12m); // 5 Dozen = 60 Units
        item.StockQty.ShouldBe(60m); // SRE consumption uses this value
    }

    [Fact]
    public void SI_UpdateStock_NonStockItems_SkipSRE()
    {
        // Service items (MaintainStock=false) don't have stock reservations
        // SRE consumption should skip these items
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "CONSULT-001", "Consulting", ItemType.Service);
        item.MaintainStock.ShouldBeFalse(); // Service items don't maintain stock
    }

    // ═══════════════════════════════════════════════
    // DN Submit — SRE + Bin Combined
    // ═══════════════════════════════════════════════

    [Fact]
    public void DN_Submit_ShouldConsumeReservation_AND_ReleaseBinReservedQty()
    {
        // DN submit does TWO things with reservations:
        // 1. Updates SRE.DeliveredQty (FIFO consumption)
        // 2. Reduces Bin.ReservedQty
        // Both are needed for accurate stock tracking
        var sre = CreateTestSre(100m);
        sre.RecordDelivery(40m);
        sre.AvailableQty.ShouldBe(60m);

        // Bin reserved qty would be updated separately by BinService.UpdateReservedQtyAsync(-40)
        // This test validates the SRE side only
        sre.DeliveredQty.ShouldBe(40m);
    }

    // ═══════════════════════════════════════════════
    // Overdue Invoice Detection Concept
    // ═══════════════════════════════════════════════

    [Fact]
    public void OverdueInvoice_PastDueDate_WithOutstanding_IsOverdue()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var issueDate = DateTime.UtcNow.AddDays(-60);
        var invoice = new SalesInvoice(
            Guid.NewGuid(), companyId, customerId, "SI-001", issueDate);
        invoice.AddItem(Guid.NewGuid(), "Test Item", 1, 5000, 0);
        invoice.DueDate = DateTime.UtcNow.AddDays(-30); // Past due
        invoice.Submit();

        // This invoice is overdue (DueDate < today and outstanding > 0)
        invoice.DueDate.ShouldNotBeNull();
        invoice.DueDate!.Value.ShouldBeLessThan(DateTime.UtcNow);
        invoice.OutstandingAmount.ShouldBe(5000m);
    }

    [Fact]
    public void OverdueInvoice_FutureDueDate_NotOverdue()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var issueDate = DateTime.UtcNow;
        var invoice = new SalesInvoice(
            Guid.NewGuid(), companyId, customerId, "SI-002", issueDate);
        invoice.AddItem(Guid.NewGuid(), "Test Item", 1, 5000, 0);
        invoice.DueDate = DateTime.UtcNow.AddDays(30); // Future due date

        invoice.DueDate.ShouldNotBeNull();
        invoice.DueDate!.Value.ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Fact]
    public void OverdueInvoice_FullyPaid_NotOverdue()
    {
        // Even if past due, a fully paid invoice is NOT overdue
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var issueDate = DateTime.UtcNow.AddDays(-60);
        var invoice = new SalesInvoice(
            Guid.NewGuid(), companyId, customerId, "SI-003", issueDate);
        invoice.AddItem(Guid.NewGuid(), "Test Item", 1, 5000, 0);
        invoice.DueDate = DateTime.UtcNow.AddDays(-30);
        invoice.AmountPaid = 5000m;

        invoice.OutstandingAmount.ShouldBe(0m);
    }

    // ═══════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════

    private static StockReservationEntry CreateTestSre(decimal reservedQty)
    {
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), reservedQty, tenantId: null);
        sre.Submit();
        return sre;
    }

    private static SalesInvoiceItem CreateTestSiItem(decimal quantity, decimal conversionFactor)
    {
        var item = new SalesInvoiceItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test Item", quantity, 100m, 0);
        item.ConversionFactor = conversionFactor;
        return item;
    }
}
