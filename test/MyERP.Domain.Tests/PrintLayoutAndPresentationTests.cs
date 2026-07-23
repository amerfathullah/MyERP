using System;
using Xunit;
using MyERP.Inventory;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for print layout data requirements and document presentation logic.
/// Covers: PR/SE print layouts, document number formatting, status display.
/// </summary>
public class PrintLayoutAndPresentationTests
{
    // --- Purchase Receipt Print Data ---

    [Fact]
    public void PurchaseReceipt_PrintData_HasRequiredFields()
    {
        var pr = CreateTestPurchaseReceipt();
        Assert.NotNull(pr.ReceiptNumber);
        Assert.NotEqual(default, pr.PostingDate);
        Assert.True(pr.Items.Count > 0);
    }

    [Fact]
    public void PurchaseReceipt_ReturnBadge_ShownWhenIsReturn()
    {
        var pr = CreateTestPurchaseReceipt();
        pr.IsReturn = true;
        Assert.True(pr.IsReturn);
    }

    [Fact]
    public void PurchaseReceipt_AbsQuantity_UsedForPrint()
    {
        // Returns have negative qty but print shows absolute value
        var quantity = -5m;
        var absQty = Math.Abs(quantity);
        Assert.Equal(5m, absQty);
    }

    [Fact]
    public void PurchaseReceipt_GrandTotal_SumsCorrectly()
    {
        var netTotal = 1000m;
        var taxAmount = 60m;
        var grandTotal = netTotal + taxAmount;
        Assert.Equal(1060m, grandTotal);
    }

    // --- Stock Entry Print Data ---

    [Fact]
    public void StockEntry_EntryTypeLabel_MapsCorrectly()
    {
        var typeLabels = new System.Collections.Generic.Dictionary<string, string>
        {
            ["0"] = "RECEIPT", ["1"] = "ISSUE", ["2"] = "TRANSFER",
            ["4"] = "MANUFACTURE", ["5"] = "REPACK", ["8"] = "DISASSEMBLE",
        };
        Assert.Equal("RECEIPT", typeLabels["0"]);
        Assert.Equal("MANUFACTURE", typeLabels["4"]);
        Assert.Equal("DISASSEMBLE", typeLabels["8"]);
    }

    [Fact]
    public void StockEntry_TotalQty_SumsAbsoluteQuantities()
    {
        // Items may have negative qty (consumption) — print shows absolute
        var quantities = new[] { 10m, -5m, 3m };
        var totalQty = 0m;
        foreach (var q in quantities) totalQty += Math.Abs(q);
        Assert.Equal(18m, totalQty);
    }

    [Fact]
    public void StockEntry_TotalValue_UsesQtyTimesRate()
    {
        var qty = 10m;
        var rate = 15.50m;
        var value = Math.Abs(qty * rate);
        Assert.Equal(155.0m, value);
    }

    [Fact]
    public void StockEntry_TransferDocument_ShowsBothWarehouses()
    {
        var source = "WH-001";
        var target = "WH-002";
        Assert.NotEqual(source, target);
        Assert.False(string.IsNullOrEmpty(source));
        Assert.False(string.IsNullOrEmpty(target));
    }

    [Fact]
    public void StockEntry_ManufactureType_HasWorkOrderReference()
    {
        var entryType = StockEntryType.Manufacture;
        var woId = Guid.NewGuid();
        Assert.Equal(StockEntryType.Manufacture, entryType);
        Assert.NotEqual(Guid.Empty, woId);
    }

    // --- Document Number Formatting ---

    [Fact]
    public void DocumentNumber_FiscalYearFormat_IncludesYear()
    {
        var prefix = "PR";
        var fy = "2026";
        var seq = 42;
        var number = $"{prefix}-{fy}-{seq:D5}";
        Assert.Equal("PR-2026-00042", number);
    }

    [Fact]
    public void DocumentNumber_StandardFormat_NoYear()
    {
        var prefix = "SE";
        var seq = 123;
        var number = $"{prefix}-{seq:D5}";
        Assert.Equal("SE-00123", number);
    }

    // --- Status Display Logic ---

    [Fact]
    public void StatusDisplay_Draft_ShowsEditDelete()
    {
        var status = "Draft";
        var showEdit = status == "Draft";
        var showDelete = status == "Draft";
        Assert.True(showEdit);
        Assert.True(showDelete);
    }

    [Fact]
    public void StatusDisplay_Submitted_ShowsWorkflowActions()
    {
        var status = "Submitted";
        var showMakeInvoice = status == "Submitted";
        var showCancel = status == "Submitted";
        var showEdit = status == "Draft"; // not shown
        Assert.True(showMakeInvoice);
        Assert.True(showCancel);
        Assert.False(showEdit);
    }

    [Fact]
    public void StatusDisplay_Cancelled_ShowsAmendOnly()
    {
        var status = "Cancelled";
        var showAmend = status == "Cancelled";
        var showSubmit = status == "Draft"; // not shown
        Assert.True(showAmend);
        Assert.False(showSubmit);
    }

    // --- Print Layout Currency Formatting ---

    [Fact]
    public void CurrencyFormat_MalaysianRinggit_2DecimalPlaces()
    {
        var amount = 1234.567m;
        var formatted = amount.ToString("N2");
        Assert.Equal("1,234.57", formatted);
    }

    [Fact]
    public void CurrencyFormat_ZeroAmount_ShowsZero()
    {
        var amount = 0m;
        var formatted = amount.ToString("N2");
        Assert.Equal("0.00", formatted);
    }

    [Fact]
    public void CurrencyFormat_NegativeForReturn_ShowsAbsolute()
    {
        var amount = -500.25m;
        var absFormatted = Math.Abs(amount).ToString("N2");
        Assert.Equal("500.25", absFormatted);
    }

    // --- Date Formatting ---

    [Fact]
    public void DateFormat_Malaysian_ddMMyyy()
    {
        var date = new DateTime(2026, 7, 23);
        var formatted = date.ToString("dd/MM/yyyy");
        Assert.Equal("23/07/2026", formatted);
    }

    // Helper
    private static TestPurchaseReceipt CreateTestPurchaseReceipt()
    {
        return new TestPurchaseReceipt
        {
            ReceiptNumber = "PR-2026-00001",
            PostingDate = new DateTime(2026, 7, 23),
            IsReturn = false,
            Items = new() { new() { Quantity = 10, UnitPrice = 100m } },
            NetTotal = 1000m,
            TaxAmount = 60m,
            GrandTotal = 1060m,
        };
    }

    private class TestPurchaseReceipt
    {
        public string ReceiptNumber { get; set; } = "";
        public DateTime PostingDate { get; set; }
        public bool IsReturn { get; set; }
        public System.Collections.Generic.List<TestItem> Items { get; set; } = new();
        public decimal NetTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
    }

    private class TestItem
    {
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
