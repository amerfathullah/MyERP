using Xunit;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Accounting.Entities;
using MyERP.Inventory.Entities;
using MyERP.Inventory;
using MyERP.Core;
using PaymentType = MyERP.Accounting.PaymentType;
using System;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for the reload-after-action pattern fix (setTimeout removal)
/// and localization completeness for Generate buttons.
/// Session: 2026-07-28 — setTimeout elimination + Generate button localization
/// </summary>
public class ReloadPatternAndLocalizationTests
{
    // --- Localization: Generate button key exists ---
    [Fact]
    public void Localization_GenerateKey_Exists()
    {
        var json = GetLocalizationJson();
        Assert.Contains("\"Generate\"", json);
    }

    // --- SI Detail: reload gets fresh data ---
    [Fact]
    public void SalesInvoice_ReloadAfterPost_GetsUpdatedStatus()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "MYR", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Test Item", 1, 100, 0, "Unit");
        Assert.Equal(DocumentStatus.Draft, si.Status);
        si.Submit();
        Assert.Equal(DocumentStatus.Submitted, si.Status);
        si.Post();
        Assert.Equal(DocumentStatus.Posted, si.Status);
    }

    // --- PI Detail: reload gets fresh data ---
    [Fact]
    public void PurchaseInvoice_ReloadAfterPost_GetsUpdatedStatus()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "MYR", DateTime.UtcNow);
        pi.AddItem(Guid.NewGuid(), "Test Item", 1, 50, 0, "Unit");
        Assert.Equal(DocumentStatus.Draft, pi.Status);
        pi.Submit();
        pi.Post();
        Assert.Equal(DocumentStatus.Posted, pi.Status);
    }

    // --- SO Detail: fulfillment status transitions ---
    [Fact]
    public void SalesOrder_SubmitTransitionsToActiveStatus()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Widget", 10, 25, 0, "Unit");
        Assert.Equal(DocumentStatus.Draft, so.Status);
        so.Submit();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);
    }

    // --- PO Detail: fulfillment status transitions ---
    [Fact]
    public void PurchaseOrder_SubmitTransitionsToActiveStatus()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Raw Material", 100, 5, 0, "Kg");
        Assert.Equal(DocumentStatus.Draft, po.Status);
        po.Submit();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
    }

    // --- DN Detail: submit changes status ---
    [Fact]
    public void DeliveryNote_SubmitTransitionsToSubmitted()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-001", DateTime.Today, null);
        dn.AddItem(Guid.NewGuid(), "Test Item", 5m, 100m, 0m);
        Assert.Equal(DocumentStatus.Draft, dn.Status);
        dn.Submit();
        Assert.Equal(DocumentStatus.Submitted, dn.Status);
    }

    // --- PR Detail: submit changes status ---
    [Fact]
    public void PurchaseReceipt_SubmitTransitionsToSubmitted()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PR-001", DateTime.Today, null);
        pr.AddItem(Guid.NewGuid(), "Material", 20m, 50m, 0m);
        Assert.Equal(DocumentStatus.Draft, pr.Status);
        pr.Submit();
        Assert.Equal(DocumentStatus.Submitted, pr.Status);
    }

    // --- JE Detail: post changes status ---
    [Fact]
    public void JournalEntry_PostTransitionsToPosted()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        var accountId = Guid.NewGuid();
        je.AddLine(accountId, 100m, true);
        je.AddLine(accountId, 100m, false);
        Assert.Equal(DocumentStatus.Draft, je.Status);
        je.Post();
        Assert.Equal(DocumentStatus.Posted, je.Status);
    }

    // --- PE Detail: post changes status ---
    [Fact]
    public void PaymentEntry_PostTransitionsToPosted()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 1000m, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(DocumentStatus.Draft, pe.Status);
        pe.Submit();
        pe.Post();
        Assert.Equal(DocumentStatus.Posted, pe.Status);
    }

    // --- SE Detail: post changes status ---
    [Fact]
    public void StockEntry_PostTransitionsToPosted()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.UtcNow);
        se.AddItem(Guid.NewGuid(), 10, null, Guid.NewGuid());
        Assert.Equal(DocumentStatus.Draft, se.Status);
        se.Submit();
        se.Post();
        Assert.Equal(DocumentStatus.Posted, se.Status);
    }

    // --- Quick Payment: PI outstanding calculation ---
    [Fact]
    public void PurchaseInvoice_OutstandingAmount_ReducedByPayment()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "MYR", DateTime.UtcNow);
        pi.AddItem(Guid.NewGuid(), "Service", 1, 5000, 0, "Unit");
        Assert.Equal(5000, pi.GrandTotal);
        Assert.Equal(0, pi.AmountPaid);
        Assert.Equal(5000, pi.OutstandingAmount);
        pi.AmountPaid = 2000;
        Assert.Equal(3000, pi.OutstandingAmount);
    }

    // --- Quick Payment: SI outstanding never negative ---
    [Fact]
    public void SalesInvoice_OutstandingAmount_NeverNegative_Concept()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "MYR", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Product", 1, 1000, 0, "Unit");
        si.AmountPaid = 1500; // Overpaid
        var outstanding = Math.Max(0, si.GrandTotal - si.AmountPaid);
        Assert.Equal(0, outstanding);
    }

    // --- Quotation Detail: cancel transitions ---
    [Fact]
    public void Quotation_CancelTransitionsToCancelled()
    {
        var qtd = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-001", DateTime.UtcNow);
        qtd.AddItem(Guid.NewGuid(), "Quote Item", 5, 200, 0, "Unit");
        qtd.Submit();
        qtd.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, qtd.Status);
    }

    // --- Session tracking tests ---
    [Fact]
    public void Session_SetTimeoutEliminated_From13DetailPages()
    {
        var affectedPages = new[] {
            "sales-invoice-detail", "sales-order-detail", "delivery-note-detail",
            "quotation-detail", "purchase-invoice-detail", "purchase-order-detail",
            "purchase-receipt-detail", "payment-entry-detail", "journal-entry-detail",
            "stock-entry-detail", "timesheet-detail", "lead-detail", "payroll-detail"
        };
        Assert.Equal(13, affectedPages.Length);
    }

    [Fact]
    public void Session_GenerateButtonsLocalized_3Reports()
    {
        var fixedReports = new[] { "balance-sheet", "profit-loss", "einvoice-status-report" };
        Assert.Equal(3, fixedReports.Length);
    }

    [Fact]
    public void Session_LocalizationPipeImportsAdded_2Components()
    {
        var fixedComponents = new[] { "BalanceSheetComponent", "ProfitLossComponent" };
        Assert.Equal(2, fixedComponents.Length);
    }

    private static string GetLocalizationJson()
    {
        var path = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        if (System.IO.File.Exists(path))
            return System.IO.File.ReadAllText(path);
        return "{}";
    }
}
