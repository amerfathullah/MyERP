using System;
using MyERP.Accounting.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for July 24 2026 continued session:
/// 1. Early Payment Discount on PaymentScheduleEntry
/// 2. Invoice IsOverdue computed property (SI + PI)
/// 3. BOM Cost level-wise processing
/// 4. PaymentScheduleEntry recording and lifecycle
/// </summary>
public class PaymentReminderBomCostDiscountTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    // ── Invoice IsOverdue — Sales Invoice ──────────────────────────

    [Fact]
    public void SalesInvoice_IsOverdue_WhenPostedAndPastDue()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, Guid.NewGuid(),
            "SI-001", DateTime.UtcNow.AddDays(-45));
        si.DueDate = DateTime.UtcNow.AddDays(-15);
        si.AddItem(Guid.NewGuid(), "Test", 1, 1000, 60);
        si.Submit();
        si.Post();

        Assert.True(si.IsOverdue);
    }

    [Fact]
    public void SalesInvoice_NotOverdue_WhenDueDateInFuture()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, Guid.NewGuid(),
            "SI-002", DateTime.UtcNow);
        si.DueDate = DateTime.UtcNow.AddDays(30);
        si.AddItem(Guid.NewGuid(), "Test", 1, 1000, 60);
        si.Submit();
        si.Post();

        Assert.False(si.IsOverdue);
    }

    [Fact]
    public void SalesInvoice_NotOverdue_WhenFullyPaid()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, Guid.NewGuid(),
            "SI-003", DateTime.UtcNow.AddDays(-45));
        si.DueDate = DateTime.UtcNow.AddDays(-15);
        si.AddItem(Guid.NewGuid(), "Test", 1, 1000, 60);
        si.Submit();
        si.Post();
        si.AmountPaid = si.GrandTotal;

        Assert.False(si.IsOverdue);
    }

    [Fact]
    public void SalesInvoice_NotOverdue_WhenDraft()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, Guid.NewGuid(),
            "SI-004", DateTime.UtcNow.AddDays(-45));
        si.DueDate = DateTime.UtcNow.AddDays(-15);
        si.AddItem(Guid.NewGuid(), "Test", 1, 1000, 60);

        Assert.False(si.IsOverdue);
    }

    [Fact]
    public void SalesInvoice_NotOverdue_WhenReturn()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, Guid.NewGuid(),
            "SI-005", DateTime.UtcNow.AddDays(-45));
        si.IsReturn = true;
        si.DueDate = DateTime.UtcNow.AddDays(-15);
        si.AddItem(Guid.NewGuid(), "Test", -1, 1000, 60);
        si.Submit();
        si.Post();

        Assert.False(si.IsOverdue);
    }

    [Fact]
    public void SalesInvoice_NotOverdue_WhenNoDueDate()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, Guid.NewGuid(),
            "SI-006", DateTime.UtcNow.AddDays(-45));
        si.AddItem(Guid.NewGuid(), "Test", 1, 1000, 60);
        si.Submit();
        si.Post();

        Assert.False(si.IsOverdue);
    }

    // ── Invoice IsOverdue — Purchase Invoice ───────────────────────

    [Fact]
    public void PurchaseInvoice_IsOverdue_WhenPostedAndPastDue()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, Guid.NewGuid(),
            "PI-001", DateTime.UtcNow.AddDays(-45));
        pi.DueDate = DateTime.UtcNow.AddDays(-15);
        pi.AddItem(Guid.NewGuid(), "Test", 1, 1000, 60);
        pi.Submit();
        pi.Post();

        Assert.True(pi.IsOverdue);
    }

    [Fact]
    public void PurchaseInvoice_NotOverdue_WhenFullyPaid()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, Guid.NewGuid(),
            "PI-002", DateTime.UtcNow.AddDays(-45));
        pi.DueDate = DateTime.UtcNow.AddDays(-15);
        pi.AddItem(Guid.NewGuid(), "Test", 1, 1000, 60);
        pi.Submit();
        pi.Post();
        pi.AmountPaid = pi.GrandTotal;

        Assert.False(pi.IsOverdue);
    }

    // ── Early Payment Discount ─────────────────────────────────────

    [Fact]
    public void PaymentScheduleEntry_EarlyDiscount_DefaultsEmpty()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100, 1000);

        Assert.Null(entry.DiscountType);
        Assert.Equal(0, entry.DiscountPercentage);
        Assert.Null(entry.DiscountValidTill);
        Assert.Equal(0, entry.DiscountedAmount);
    }

    [Fact]
    public void PaymentScheduleEntry_EarlyDiscount_Percentage_Available()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100, 1000);
        entry.DiscountType = "Percentage";
        entry.DiscountPercentage = 2;
        entry.DiscountValidTill = DateTime.UtcNow.AddDays(10);

        Assert.True(entry.IsDiscountAvailable(DateTime.UtcNow));
    }

    [Fact]
    public void PaymentScheduleEntry_EarlyDiscount_Expired()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100, 1000);
        entry.DiscountType = "Percentage";
        entry.DiscountPercentage = 2;
        entry.DiscountValidTill = DateTime.UtcNow.AddDays(-1);

        Assert.False(entry.IsDiscountAvailable(DateTime.UtcNow));
    }

    [Fact]
    public void PaymentScheduleEntry_EarlyDiscount_PercentageCalc()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100, 1000);
        entry.DiscountType = "Percentage";
        entry.DiscountPercentage = 2;
        entry.DiscountValidTill = DateTime.UtcNow.AddDays(10);

        Assert.Equal(980m, entry.GetPayableAmount(DateTime.UtcNow));
    }

    [Fact]
    public void PaymentScheduleEntry_EarlyDiscount_ExpiredFallsBackToFull()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100, 1000);
        entry.DiscountType = "Percentage";
        entry.DiscountPercentage = 2;
        entry.DiscountValidTill = DateTime.UtcNow.AddDays(-5);

        Assert.Equal(1000m, entry.GetPayableAmount(DateTime.UtcNow));
    }

    [Fact]
    public void PaymentScheduleEntry_EarlyDiscount_FixedAmount()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100, 1000);
        entry.DiscountType = "Amount";
        entry.DiscountPercentage = 50;
        entry.DiscountValidTill = DateTime.UtcNow.AddDays(10);

        Assert.Equal(950m, entry.GetPayableAmount(DateTime.UtcNow));
    }

    [Fact]
    public void PaymentScheduleEntry_EarlyDiscount_PreCalculated()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100, 1000);
        entry.DiscountType = "Percentage";
        entry.DiscountPercentage = 2;
        entry.DiscountValidTill = DateTime.UtcNow.AddDays(10);
        entry.DiscountedAmount = 980;

        Assert.Equal(980m, entry.GetPayableAmount(DateTime.UtcNow));
    }

    [Fact]
    public void PaymentScheduleEntry_EarlyDiscount_PartialPayment()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100, 1000);
        entry.DiscountType = "Percentage";
        entry.DiscountPercentage = 2;
        entry.DiscountValidTill = DateTime.UtcNow.AddDays(10);
        entry.DiscountedAmount = 980;

        entry.RecordPayment(500);
        Assert.Equal(480m, entry.GetPayableAmount(DateTime.UtcNow));
    }

    [Fact]
    public void PaymentScheduleEntry_EarlyDiscount_FullyPaid_NotAvailable()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100, 1000);
        entry.DiscountType = "Percentage";
        entry.DiscountPercentage = 2;
        entry.DiscountValidTill = DateTime.UtcNow.AddDays(10);

        entry.RecordPayment(1000);
        Assert.False(entry.IsDiscountAvailable(DateTime.UtcNow));
    }

    [Fact]
    public void PaymentScheduleEntry_EarlyDiscount_NoExpiry_AlwaysAvailable()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100, 1000);
        entry.DiscountType = "Percentage";
        entry.DiscountPercentage = 5;

        Assert.True(entry.IsDiscountAvailable(DateTime.UtcNow));
        Assert.Equal(950m, entry.GetPayableAmount(DateTime.UtcNow));
    }

    // ── BOM Cost level-wise processing ─────────────────────────────

    [Fact]
    public void BOM_RecalculateCost_UpdatesTotalFromItems()
    {
        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials(bomId, CompanyId, "BOM-001", Guid.NewGuid());
        bom.Quantity = 1;
        bom.Items.Add(new BomItem(Guid.NewGuid(), bomId, Guid.NewGuid(), "Material A", 5, 10)); // 50
        bom.Items.Add(new BomItem(Guid.NewGuid(), bomId, Guid.NewGuid(), "Material B", 3, 20)); // 60
        bom.RecalculateCost();

        Assert.Equal(110m, bom.TotalCost);
    }

    [Fact]
    public void BOM_SubBomRate_CalculatedFromChildCost()
    {
        var childBomId = Guid.NewGuid();
        var childBom = new BillOfMaterials(childBomId, CompanyId, "BOM-CHILD", Guid.NewGuid());
        childBom.Quantity = 10;
        childBom.Items.Add(new BomItem(Guid.NewGuid(), childBomId, Guid.NewGuid(), "Raw Material", 20, 25)); // 500
        childBom.RecalculateCost();

        var subBomRate = childBom.Quantity > 0 ? childBom.TotalCost / childBom.Quantity : 0;
        Assert.Equal(50m, subBomRate);
    }

    [Fact]
    public void BOM_LevelWise_LeafFirst_ThenParent()
    {
        var leafBomId = Guid.NewGuid();
        var leafBom = new BillOfMaterials(leafBomId, CompanyId, "BOM-LEAF", Guid.NewGuid());
        leafBom.Quantity = 1;
        leafBom.Items.Add(new BomItem(Guid.NewGuid(), leafBomId, Guid.NewGuid(), "Raw A", 2, 100)); // 200
        leafBom.RecalculateCost();

        var parentBomId = Guid.NewGuid();
        var parentBom = new BillOfMaterials(parentBomId, CompanyId, "BOM-PARENT", Guid.NewGuid());
        parentBom.Quantity = 1;
        parentBom.Items.Add(new BomItem(Guid.NewGuid(), parentBomId, Guid.NewGuid(), "Sub-Assembly", 5, leafBom.TotalCost / leafBom.Quantity)); // 5 × 200
        parentBom.RecalculateCost();

        Assert.Equal(1000m, parentBom.TotalCost);
    }

    [Fact]
    public void BOM_ZeroQuantity_NoDivisionByZero()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), CompanyId, "BOM-ZERO", Guid.NewGuid());
        bom.Quantity = 0;
        bom.RecalculateCost();

        var rate = bom.Quantity > 0 ? bom.TotalCost / bom.Quantity : 0;
        Assert.Equal(0m, rate);
    }

    // ── PaymentScheduleEntry Lifecycle ──────────────────────────────

    [Fact]
    public void PaymentScheduleEntry_Recording_ReducesOutstanding()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 50, 500);

        var allocated = entry.RecordPayment(200);

        Assert.Equal(200m, allocated);
        Assert.Equal(300m, entry.Outstanding);
        Assert.False(entry.IsFullyPaid);
    }

    [Fact]
    public void PaymentScheduleEntry_FullPayment_MarksComplete()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 50, 500);
        entry.RecordPayment(500);

        Assert.True(entry.IsFullyPaid);
    }

    [Fact]
    public void PaymentScheduleEntry_OverPayment_Capped()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100, 1000);

        var allocated = entry.RecordPayment(1500);
        Assert.Equal(1000m, allocated);
        Assert.True(entry.IsFullyPaid);
    }

    [Fact]
    public void PaymentScheduleEntry_ProgressivePayments()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100, 1000);

        entry.RecordPayment(300);
        Assert.Equal(700m, entry.Outstanding);

        entry.RecordPayment(500);
        Assert.Equal(200m, entry.Outstanding);

        entry.RecordPayment(200);
        Assert.True(entry.IsFullyPaid);
    }
}
