using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Boundary condition tests — edge cases that could cause runtime failures
/// if not handled properly.
/// </summary>
public class BoundaryConditionTests
{
    [Fact]
    public void SalesOrder_ZeroItems_PerDelivered_NoZeroDivision()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        // PerDelivered with no items should not throw
        so.PerDelivered.ShouldBe(0m);
    }

    [Fact]
    public void PurchaseOrder_ZeroItems_PerReceived_NoZeroDivision()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
        po.PerReceived.ShouldBe(0m);
    }

    [Fact]
    public void SalesInvoice_ZeroGrandTotal_OutstandingZero()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.UtcNow);
        // No items = zero total
        si.GrandTotal.ShouldBe(0m);
        si.OutstandingAmount.ShouldBe(0m);
    }

    [Fact]
    public void Bin_NegativeStockMovement_BelowZero()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ApplyStockMovement(10, 500); // Start with 10
        bin.ApplyStockMovement(-15, -750); // Take out more than available
        bin.ActualQty.ShouldBe(-5); // Bin can go negative
        bin.StockValue.ShouldBe(-250);
    }

    [Fact]
    public void Bin_ProjectedQty_AllFieldsZero()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ProjectedQty.ShouldBe(0m);
    }

    [Fact]
    public void PaymentEntry_PaidAmountZero_IsValid()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(),
            PaymentType.Receive, DateTime.UtcNow, 0m, Guid.NewGuid(), Guid.NewGuid());
        pe.PaidAmount.ShouldBe(0m);
    }

    [Fact]
    public void SalesOrder_PerBilled_ZeroNetTotal()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-Z", DateTime.UtcNow);
        // No items means NetTotal is 0 — PerBilled should not divide by zero
        so.PerBilled.ShouldBe(0m);
    }

    [Fact]
    public void DeliveryNote_EmptyItems_TotalZero()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "DN-001", DateTime.UtcNow);
        dn.GrandTotal.ShouldBe(0m);
        dn.Items.Count.ShouldBe(0);
    }

    [Fact]
    public void JournalEntry_EmptyLines_TotalDebitZero()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        je.TotalDebit.ShouldBe(0m);
        je.TotalCredit.ShouldBe(0m);
    }

    [Fact]
    public void Quotation_NoValidUntil_NeverExpires()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-001", DateTime.UtcNow);
        // ValidUntil not set — IsExpired should be false
        q.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void Quotation_FutureValidUntil_NotExpired()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-002", DateTime.UtcNow);
        q.ValidUntil = DateTime.UtcNow.AddDays(30);
        q.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void SalesOrder_CloseFromClosed_Throws()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-X", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item", 1, 100m, 0m);
        so.Submit();
        so.Close();
        Should.Throw<BusinessException>(() => so.Close());
    }

    [Fact]
    public void PurchaseOrder_CloseFromDraft_Throws()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-X", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Item", 1, 50m, 0m);
        // Cannot close a draft PO
        Should.Throw<BusinessException>(() => po.Close());
    }

    [Fact]
    public void StockEntry_EmptyItems_Submit_Throws()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.UtcNow);
        Should.Throw<BusinessException>(() => se.Submit());
    }

    [Fact]
    public void CurrencyExchange_RateOfOne_IsBaseCurrency()
    {
        var cx = new CurrencyExchange(Guid.NewGuid(), "MYR", "MYR", 1.0m, DateTime.UtcNow);
        cx.ExchangeRate.ShouldBe(1.0m);
    }
}
