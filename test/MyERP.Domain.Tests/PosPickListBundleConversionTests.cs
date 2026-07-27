using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for POS Opening/Closing workflows, Pick List operations,
/// Product Bundle management, document conversions (SO→MR, JE reversal),
/// and MR item SO linkage.
/// </summary>
public class PosPickListBundleConversionTests
{
    // === POS Opening Entry ===

    [Fact]
    public void PosOpening_DefaultStatus_IsOpen()
    {
        var entry = new PosOpeningEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(PosOpeningStatus.Open, entry.Status);
    }

    [Fact]
    public void PosOpening_AddBalance_IncreasesTotal()
    {
        var entry = new PosOpeningEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddOpeningBalance(Guid.NewGuid(), "Cash", 500m);
        entry.AddOpeningBalance(Guid.NewGuid(), "Credit Card", 200m);
        Assert.Equal(700m, entry.TotalOpeningAmount);
        Assert.Equal(2, entry.Payments.Count);
    }

    [Fact]
    public void PosOpening_Close_SetsClosedStatus()
    {
        var entry = new PosOpeningEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddOpeningBalance(Guid.NewGuid(), "Cash", 100m);
        var closingId = Guid.NewGuid();
        entry.Close(closingId);
        Assert.Equal(PosOpeningStatus.Closed, entry.Status);
        Assert.Equal(closingId, entry.PosClosingEntryId);
    }

    [Fact]
    public void PosOpening_Cancel_RequiresClosedStatus()
    {
        var entry = new PosOpeningEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.Close(Guid.NewGuid());
        entry.Cancel();
        Assert.Equal(PosOpeningStatus.Cancelled, entry.Status);
    }

    // === POS Closing Entry ===

    [Fact]
    public void PosClosing_DefaultStatus_IsDraft()
    {
        var entry = new PosClosingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(PosClosingStatus.Draft, entry.Status);
    }

    [Fact]
    public void PosClosing_AddPayment_TracksVariance()
    {
        var entry = new PosClosingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddPayment(Guid.NewGuid(), "Cash", 1000m, 980m);
        Assert.Single(entry.Payments);
        Assert.Equal(20m, entry.Payments.First().Difference);
    }

    [Fact]
    public void PosClosing_Submit_CalculatesGrandTotal()
    {
        var entry = new PosClosingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddPayment(Guid.NewGuid(), "Cash", 500m, 500m);
        entry.AddPayment(Guid.NewGuid(), "Card", 300m, 310m);
        entry.AddInvoice(Guid.NewGuid(), "INV-001", 800m);
        entry.Submit();
        Assert.Equal(PosClosingStatus.Submitted, entry.Status);
        Assert.Equal(800m, entry.GrandTotal);
    }

    [Fact]
    public void PosClosing_TotalDifference_SumsPaymentVariances()
    {
        var entry = new PosClosingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddPayment(Guid.NewGuid(), "Cash", 1000m, 990m); // -10
        entry.AddPayment(Guid.NewGuid(), "Card", 500m, 510m);  // +10
        Assert.Equal(0m, entry.TotalDifference);
    }

    // === Pick List ===

    [Fact]
    public void PickList_DefaultStatus_IsDraft()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        Assert.Equal(DocumentStatus.Draft, pl.Status);
    }

    [Fact]
    public void PickList_AddItem_SetsQuantity()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10m);
        Assert.Single(pl.Items);
        Assert.Equal(10m, pl.Items.First().Qty);
    }

    [Fact]
    public void PickList_Submit_RequiresItems()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        Assert.Throws<BusinessException>(() => pl.Submit());
    }

    [Fact]
    public void PickList_RecordTransfer_TracksPending()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10m);
        pl.Submit();
        var item = pl.Items.First();
        item.RecordTransfer(4m);
        Assert.Equal(4m, item.TransferredQty);
        Assert.Equal(6m, item.PendingQty);
    }

    [Fact]
    public void PickList_FullTransfer_IsFullyTransferred()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 5m);
        pl.Submit();
        pl.Items.First().RecordTransfer(5m);
        Assert.True(pl.IsFullyTransferred);
    }

    [Fact]
    public void PickList_PartialTransfer_IsPartiallyTransferred()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10m);
        pl.Submit();
        pl.Items.First().RecordTransfer(3m);
        Assert.True(pl.IsPartiallyTransferred);
        Assert.False(pl.IsFullyTransferred);
    }

    // === Product Bundle ===

    [Fact]
    public void ProductBundle_DefaultIsActive_True()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        Assert.True(bundle.IsActive);
    }

    [Fact]
    public void ProductBundle_AddItem_IncreasesCount()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.AddItem(Guid.NewGuid(), 5m, "Widget");
        Assert.Single(bundle.Items);
    }

    [Fact]
    public void ProductBundle_Deactivate_SetsInactive()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.Deactivate();
        Assert.False(bundle.IsActive);
    }

    [Fact]
    public void ProductBundle_Activate_SetsActive()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.Deactivate();
        bundle.Activate();
        Assert.True(bundle.IsActive);
    }

    [Fact]
    public void ProductBundle_Valuation_SumsComponentValues()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        var partA = Guid.NewGuid();
        var partB = Guid.NewGuid();
        bundle.AddItem(partA, 2m, "Part A");
        bundle.AddItem(partB, 3m, "Part B");
        var valuation = bundle.CalculateValuation(id =>
            id == partA ? 50m : id == partB ? 30m : 0m);
        Assert.Equal(190m, valuation); // 2×50 + 3×30
    }

    // === JE Reversal Fields ===

    [Fact]
    public void JournalEntry_ReversalOfId_DefaultsNull()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow);
        Assert.Null(je.ReversalOfId);
    }

    [Fact]
    public void JournalEntry_ReversalType_CanBeSet()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow);
        je.VoucherType = JournalEntryVoucherType.Reversal;
        Assert.Equal(JournalEntryVoucherType.Reversal, je.VoucherType);
    }

    [Fact]
    public void JournalEntry_Reversal_SwapsDebitCredit()
    {
        var original = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow);
        var acctA = Guid.NewGuid();
        var acctB = Guid.NewGuid();
        original.AddLine(acctA, 100m, true, "Expense");
        original.AddLine(acctB, 100m, false, "Bank");
        original.Post();

        var reversal = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow);
        reversal.VoucherType = JournalEntryVoucherType.Reversal;
        reversal.ReversalOfId = original.Id;
        foreach (var line in original.Lines)
        {
            reversal.AddLine(line.AccountId, line.Amount, !line.IsDebit, $"Reversal: {line.Description}");
        }
        reversal.Post();

        var reversalLines = reversal.Lines.ToList();
        Assert.Equal(2, reversalLines.Count);
        Assert.False(reversalLines[0].IsDebit);
        Assert.True(reversalLines[1].IsDebit);
    }

    // === Material Request Item SO Linkage ===

    [Fact]
    public void MaterialRequestItem_SalesOrderId_DefaultsNull()
    {
        var item = new MaterialRequestItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test Item", 10m, "Unit");
        Assert.Null(item.SalesOrderId);
    }

    [Fact]
    public void MaterialRequestItem_SalesOrderId_CanBeSet()
    {
        var item = new MaterialRequestItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test Item", 10m, "Unit");
        var soId = Guid.NewGuid();
        item.SalesOrderId = soId;
        Assert.Equal(soId, item.SalesOrderId);
    }

    // === Document Conversion Concepts ===

    [Fact]
    public void SalesOrder_PendingDeliveryQty_DeterminesConvertibleQty()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-TEST", DateTime.UtcNow.AddDays(7));
        so.AddItem(Guid.NewGuid(), "Widget", 100m, 50m, 0m);
        var item = so.Items.First();
        Assert.Equal(100m, item.PendingDeliveryQty);
    }

    [Fact]
    public void PosClosing_Cancel_FromSubmitted()
    {
        var entry = new PosClosingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddInvoice(Guid.NewGuid(), "INV-001", 100m);
        entry.Submit();
        entry.Cancel();
        Assert.Equal(PosClosingStatus.Cancelled, entry.Status);
    }

    [Fact]
    public void PickList_Cancel_BlockedWhenTransferred()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10m);
        pl.Submit();
        pl.Items.First().RecordTransfer(5m);
        Assert.Throws<BusinessException>(() => pl.Cancel());
    }
}
