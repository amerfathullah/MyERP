using System;
using System.Linq;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for invoice advance payment tracking, rounding, write-off,
/// and DN/PR billing percentage tracking.
/// Per ERPNext: set_advances(), round_off_totals(), write_off, update_billed_amount.
/// </summary>
public class InvoiceAdvanceRoundingBillingTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    // ─── SI: Advance Payment Tracking ───

    [Fact]
    public void SI_TotalAdvance_DefaultsZero()
    {
        var si = CreateSI();
        si.TotalAdvance.ShouldBe(0);
    }

    [Fact]
    public void SI_SetTotalAdvance_ReducesOutstanding()
    {
        var si = CreateSI(100);
        si.SetTotalAdvance(30);
        si.TotalAdvance.ShouldBe(30);
        si.OutstandingAmount.ShouldBe(70); // 100 - 0 paid - 0 writeoff - 30 advance
    }

    [Fact]
    public void SI_AdvancePlusPayment_ReducesOutstanding()
    {
        var si = CreateSI(100);
        si.SetTotalAdvance(30);
        si.AmountPaid = 40;
        si.OutstandingAmount.ShouldBe(30); // 100 - 40 - 0 - 30
    }

    [Fact]
    public void SI_NegativeAdvance_Throws()
    {
        var si = CreateSI(100);
        Should.Throw<ArgumentException>(() => si.SetTotalAdvance(-10));
    }

    // ─── SI: Write-Off ───

    [Fact]
    public void SI_WriteOff_DefaultsZero()
    {
        var si = CreateSI();
        si.WriteOffAmount.ShouldBe(0);
        si.WriteOffAccountId.ShouldBeNull();
    }

    [Fact]
    public void SI_SetWriteOff_ReducesOutstanding()
    {
        var si = CreateSI(100);
        var accountId = Guid.NewGuid();
        var ccId = Guid.NewGuid();
        si.SetWriteOff(15, accountId, ccId);

        si.WriteOffAmount.ShouldBe(15);
        si.WriteOffAccountId.ShouldBe(accountId);
        si.WriteOffCostCenterId.ShouldBe(ccId);
        si.OutstandingAmount.ShouldBe(85); // 100 - 0 - 15 - 0
    }

    [Fact]
    public void SI_WriteOff_NegativeThrows()
    {
        var si = CreateSI(100);
        Should.Throw<ArgumentException>(() => si.SetWriteOff(-5));
    }

    [Fact]
    public void SI_WriteOff_ExceedsOutstandingThrows()
    {
        var si = CreateSI(100);
        Should.Throw<ArgumentException>(() => si.SetWriteOff(101));
    }

    [Fact]
    public void SI_AllThreeReductions_Outstanding()
    {
        var si = CreateSI(1000);
        si.AmountPaid = 400;
        si.SetWriteOff(50);
        si.SetTotalAdvance(200);
        si.OutstandingAmount.ShouldBe(350); // 1000 - 400 - 50 - 200
    }

    // ─── SI: Rounding ───

    [Fact]
    public void SI_ApplyRounding_RoundsToNearestWhole()
    {
        var si = CreateSI();
        si.AddItem(_itemId, "Widget", 3, 33.33m, 0);
        // GrandTotal = 99.99
        si.ApplyRounding();

        si.RoundedTotal.ShouldBe(100);
        si.RoundingAdjustment.ShouldBe(0.01m);
    }

    [Fact]
    public void SI_ApplyRounding_RoundsDown()
    {
        var si = CreateSI();
        si.AddItem(_itemId, "Widget", 1, 100.40m, 0);
        si.ApplyRounding();

        si.RoundedTotal.ShouldBe(100);
        si.RoundingAdjustment.ShouldBe(-0.40m);
    }

    [Fact]
    public void SI_ApplyRounding_Disabled_NoChange()
    {
        var si = CreateSI();
        si.AddItem(_itemId, "Widget", 3, 33.33m, 0);
        si.DisableRoundedTotal = true;
        si.ApplyRounding();

        si.RoundedTotal.ShouldBe(si.GrandTotal);
        si.RoundingAdjustment.ShouldBe(0);
    }

    [Fact]
    public void SI_ApplyRounding_BaseCurrency()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SI-R-001", DateTime.UtcNow);
        si.ExchangeRate = 4.72m;
        si.AddItem(_itemId, "Widget", 1, 100.50m, 0);
        si.ApplyRounding();

        si.BaseRoundedTotal.ShouldBe(Math.Round(si.BaseGrandTotal, 0, MidpointRounding.AwayFromZero));
    }

    // ─── PI: Advance + Write-Off + Rounding ───

    [Fact]
    public void PI_TotalAdvance_DefaultsZero()
    {
        var pi = CreatePI();
        pi.TotalAdvance.ShouldBe(0);
    }

    [Fact]
    public void PI_SetTotalAdvance_ReducesOutstanding()
    {
        var pi = CreatePI(500);
        pi.SetTotalAdvance(100);
        pi.OutstandingAmount.ShouldBe(400);
    }

    [Fact]
    public void PI_WriteOff_ReducesOutstanding()
    {
        var pi = CreatePI(500);
        pi.SetWriteOff(25);
        pi.OutstandingAmount.ShouldBe(475);
    }

    [Fact]
    public void PI_ApplyRounding_Works()
    {
        var pi = CreatePI();
        pi.AddItem(_itemId, "Part", 7, 14.29m, 0);
        // GrandTotal = 100.03
        pi.ApplyRounding();
        pi.RoundedTotal.ShouldBe(100);
        pi.RoundingAdjustment.ShouldBe(-0.03m);
    }

    // ─── DN Item: BilledQty Tracking ───

    [Fact]
    public void DNItem_BilledQty_DefaultsZero()
    {
        var dn = CreateDN();
        dn.AddItem(_itemId, "Widget", 10, 50, 0);
        dn.Items.First().BilledQty.ShouldBe(0);
    }

    [Fact]
    public void DNItem_BilledQty_IncrementReducesPending()
    {
        var dn = CreateDN();
        dn.AddItem(_itemId, "Widget", 10, 50, 0);
        var item = dn.Items.First();
        item.BilledQty = 4;

        item.PendingBillingQty.ShouldBe(6);
    }

    [Fact]
    public void DNItem_BilledQty_FullyBilled()
    {
        var dn = CreateDN();
        dn.AddItem(_itemId, "Widget", 10, 50, 0);
        var item = dn.Items.First();
        item.BilledQty = 10;

        item.PendingBillingQty.ShouldBe(0);
    }

    // ─── DN: PerBilled (MIN% formula) ───

    [Fact]
    public void DN_PerBilled_DefaultsZero()
    {
        var dn = CreateDN();
        dn.AddItem(_itemId, "Widget A", 10, 50, 0);
        dn.AddItem(_itemId, "Widget B", 5, 100, 0);

        dn.PerBilled.ShouldBe(0);
    }

    [Fact]
    public void DN_PerBilled_PartialBilling_UsesMinFormula()
    {
        var dn = CreateDN();
        dn.AddItem(_itemId, "Widget A", 10, 50, 0);
        dn.AddItem(_itemId, "Widget B", 5, 100, 0);

        // Bill 100% of A but 0% of B
        dn.Items.First().BilledQty = 10;

        // MIN(100%, 0%) = 0%
        dn.PerBilled.ShouldBe(0);
    }

    [Fact]
    public void DN_PerBilled_FullyBilled_100()
    {
        var dn = CreateDN();
        dn.AddItem(_itemId, "Widget A", 10, 50, 0);
        dn.AddItem(_itemId, "Widget B", 5, 100, 0);

        dn.Items.First().BilledQty = 10;
        dn.Items.Last().BilledQty = 5;

        dn.PerBilled.ShouldBe(100);
    }

    [Fact]
    public void DN_PerBilled_NoItems_ReturnsZero()
    {
        var dn = CreateDN();
        dn.PerBilled.ShouldBe(0);
    }

    // ─── PR Item: BilledQty Tracking ───

    [Fact]
    public void PRItem_BilledQty_DefaultsZero()
    {
        var pr = CreatePR();
        pr.AddItem(_itemId, "Part", 20, 25, 0);
        pr.Items.First().BilledQty.ShouldBe(0);
    }

    [Fact]
    public void PRItem_BilledQty_PendingCalculation()
    {
        var pr = CreatePR();
        pr.AddItem(_itemId, "Part", 20, 25, 0);
        var item = pr.Items.First();
        item.BilledQty = 15;

        item.PendingBillingQty.ShouldBe(5);
    }

    // ─── PR: PerBilled (MIN% formula) ───

    [Fact]
    public void PR_PerBilled_DefaultsZero()
    {
        var pr = CreatePR();
        pr.AddItem(_itemId, "Part A", 10, 25, 0);
        pr.PerBilled.ShouldBe(0);
    }

    [Fact]
    public void PR_PerBilled_PartialBilling()
    {
        var pr = CreatePR();
        pr.AddItem(_itemId, "Part A", 10, 25, 0);
        pr.AddItem(_itemId, "Part B", 20, 10, 0);

        pr.Items.First().BilledQty = 10; // 100%
        pr.Items.Last().BilledQty = 10;  // 50%

        pr.PerBilled.ShouldBe(50); // MIN(100%, 50%)
    }

    [Fact]
    public void PR_PerBilled_FullyBilled()
    {
        var pr = CreatePR();
        pr.AddItem(_itemId, "Part A", 10, 25, 0);
        pr.Items.First().BilledQty = 10;

        pr.PerBilled.ShouldBe(100);
    }

    // ─── SI Item: DeliveryNoteItemId ───

    [Fact]
    public void SIItem_DeliveryNoteItemId_DefaultsNull()
    {
        var si = CreateSI(100);
        si.Items.First().DeliveryNoteItemId.ShouldBeNull();
    }

    [Fact]
    public void SIItem_DeliveryNoteItemId_CanBeSet()
    {
        var si = CreateSI(100);
        var dnItemId = Guid.NewGuid();
        si.Items.First().DeliveryNoteItemId = dnItemId;
        si.Items.First().DeliveryNoteItemId.ShouldBe(dnItemId);
    }

    // ─── OutstandingAmount formula completeness ───

    [Fact]
    public void SI_OutstandingAmount_CombinedFormula()
    {
        var si = CreateSI(1000);
        si.AmountPaid = 300;
        si.SetWriteOff(50);
        si.SetTotalAdvance(200);

        // Outstanding = Grand - Paid - WriteOff - Advance
        si.OutstandingAmount.ShouldBe(450m);
    }

    [Fact]
    public void PI_OutstandingAmount_CombinedFormula()
    {
        var pi = CreatePI(2000);
        pi.AmountPaid = 800;
        pi.SetWriteOff(100);
        pi.SetTotalAdvance(500);

        pi.OutstandingAmount.ShouldBe(600m);
    }

    // ─── Helpers ───

    private SalesInvoice CreateSI(decimal amount = 0)
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SI-TEST-001", DateTime.UtcNow);
        if (amount > 0)
            si.AddItem(_itemId, "Item", 1, amount, 0);
        return si;
    }

    private PurchaseInvoice CreatePI(decimal amount = 0)
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PI-TEST-001", DateTime.UtcNow);
        if (amount > 0)
            pi.AddItem(_itemId, "Item", 1, amount, 0);
        return pi;
    }

    private DeliveryNote CreateDN()
    {
        return new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-TEST-001", DateTime.UtcNow);
    }

    private PurchaseReceipt CreatePR()
    {
        return new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-TEST-001", DateTime.UtcNow);
    }
}
