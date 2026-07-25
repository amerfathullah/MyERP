using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for voucher-level ledger query DTOs and entity relationships.
/// Per ERPNext: every submitted document has "Stock Ledger" + "Accounting Ledger" view buttons.
/// </summary>
public class VoucherLedgerTests
{
    [Fact]
    public void VoucherLedgerDto_DefaultsToEmptyEntries()
    {
        var dto = new VoucherLedgerDto
        {
            VoucherType = "SalesInvoice",
            VoucherId = Guid.NewGuid()
        };

        Assert.Empty(dto.Entries);
        Assert.Equal(0, dto.TotalDebit);
        Assert.Equal(0, dto.TotalCredit);
        Assert.True(dto.IsBalanced);
    }

    [Fact]
    public void VoucherLedgerDto_IsBalanced_WhenDebitEqualsCredit()
    {
        var dto = new VoucherLedgerDto
        {
            VoucherType = "SalesInvoice",
            VoucherId = Guid.NewGuid(),
            TotalDebit = 1060m,
            TotalCredit = 1060m,
        };

        Assert.True(dto.IsBalanced);
    }

    [Fact]
    public void VoucherLedgerDto_IsNotBalanced_WhenDifferenceExceedsTolerance()
    {
        var dto = new VoucherLedgerDto
        {
            VoucherType = "SalesInvoice",
            VoucherId = Guid.NewGuid(),
            TotalDebit = 1060m,
            TotalCredit = 1000m,
        };

        Assert.False(dto.IsBalanced);
    }

    [Fact]
    public void VoucherLedgerDto_IsBalanced_WithinOneCentTolerance()
    {
        var dto = new VoucherLedgerDto
        {
            VoucherType = "JournalEntry",
            VoucherId = Guid.NewGuid(),
            TotalDebit = 100.005m,
            TotalCredit = 100.00m, // 0.005 < 0.01 tolerance
        };

        Assert.True(dto.IsBalanced);
    }

    [Fact]
    public void VoucherLedgerEntryDto_HasAllExpectedFields()
    {
        var entry = new VoucherLedgerEntryDto
        {
            PostingDate = new DateTime(2026, 7, 23),
            AccountCode = "1130",
            AccountName = "Accounts Receivable",
            DebitAmount = 1060m,
            CreditAmount = 0,
            CostCenterName = "Main",
            Description = "Revenue from sales",
            FinanceBook = "Default"
        };

        Assert.Equal("1130", entry.AccountCode);
        Assert.Equal(1060m, entry.DebitAmount);
        Assert.Equal("Default", entry.FinanceBook);
    }

    [Fact]
    public void VoucherStockLedgerDto_DefaultsToEmptyEntries()
    {
        var dto = new VoucherStockLedgerDto
        {
            VoucherType = "DeliveryNote",
            VoucherId = Guid.NewGuid()
        };

        Assert.Empty(dto.Entries);
        Assert.Equal(0, dto.TotalQtyIn);
        Assert.Equal(0, dto.TotalQtyOut);
        Assert.Equal(0, dto.TotalValueDifference);
    }

    [Fact]
    public void VoucherStockLedgerEntryDto_HasAllExpectedFields()
    {
        var entry = new VoucherStockLedgerEntryDto
        {
            PostingDate = new DateTime(2026, 7, 23),
            ItemCode = "ITEM-001",
            ItemName = "Widget A",
            WarehouseName = "Finished Goods",
            QuantityChange = -5m,
            ValuationRate = 10m,
            StockValueDifference = -50m,
            BalanceQuantity = 95m,
            BalanceValue = 950m,
        };

        Assert.Equal("ITEM-001", entry.ItemCode);
        Assert.Equal(-5m, entry.QuantityChange);
        Assert.Equal(95m, entry.BalanceQuantity);
    }

    [Fact]
    public void VoucherStockLedgerDto_TracksInOutTotals()
    {
        var dto = new VoucherStockLedgerDto
        {
            VoucherType = "StockEntry",
            VoucherId = Guid.NewGuid(),
            TotalQtyIn = 100m,
            TotalQtyOut = 25m,
            TotalValueDifference = 750m,
        };

        Assert.Equal(100m, dto.TotalQtyIn);
        Assert.Equal(25m, dto.TotalQtyOut);
    }

    [Fact]
    public void JournalEntry_ReferenceFields_LinkToSourceDocument()
    {
        var companyId = Guid.NewGuid();
        var fyId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var je = new JournalEntry(Guid.NewGuid(), companyId, fyId, DateTime.UtcNow);
        je.ReferenceType = "SalesInvoice";
        je.ReferenceId = sourceId;

        Assert.Equal("SalesInvoice", je.ReferenceType);
        Assert.Equal(sourceId, je.ReferenceId);
    }

    [Fact]
    public void StockLedgerEntry_VoucherFields_TrackSourceDocument()
    {
        var sle = new StockLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 7, 23), 10m, 15m, 110m, 1650m);
        sle.VoucherType = "DeliveryNote";
        sle.VoucherId = Guid.NewGuid();

        Assert.Equal("DeliveryNote", sle.VoucherType);
        Assert.NotEqual(Guid.Empty, sle.VoucherId!.Value);
    }

    [Fact]
    public void VoucherLedgerDto_SalesInvoice_TypicalPattern()
    {
        // A typical SI posting creates 3 GL lines: DR Receivable, CR Revenue, CR Tax
        var dto = new VoucherLedgerDto
        {
            VoucherType = "SalesInvoice",
            VoucherId = Guid.NewGuid(),
            VoucherNumber = "SI-2026-00042",
            Entries = new List<VoucherLedgerEntryDto>
            {
                new() { AccountCode = "1130", AccountName = "AR", DebitAmount = 1060, CreditAmount = 0 },
                new() { AccountCode = "4100", AccountName = "Revenue", DebitAmount = 0, CreditAmount = 1000 },
                new() { AccountCode = "2200", AccountName = "SST Payable", DebitAmount = 0, CreditAmount = 60 },
            },
            TotalDebit = 1060,
            TotalCredit = 1060,
        };

        Assert.Equal(3, dto.Entries.Count);
        Assert.True(dto.IsBalanced);
        Assert.Equal("SI-2026-00042", dto.VoucherNumber);
    }

    [Fact]
    public void VoucherStockLedgerDto_DeliveryNote_TypicalPattern()
    {
        // A typical DN posting creates 1 SLE per item (stock-out)
        var dto = new VoucherStockLedgerDto
        {
            VoucherType = "DeliveryNote",
            VoucherId = Guid.NewGuid(),
            Entries = new List<VoucherStockLedgerEntryDto>
            {
                new() { ItemCode = "ITEM-001", ItemName = "Widget A", WarehouseName = "FG",
                         QuantityChange = -10, ValuationRate = 15, BalanceQuantity = 90 },
                new() { ItemCode = "ITEM-002", ItemName = "Gadget B", WarehouseName = "FG",
                         QuantityChange = -5, ValuationRate = 25, BalanceQuantity = 45 },
            },
            TotalQtyIn = 0,
            TotalQtyOut = 15,
            TotalValueDifference = -275,
        };

        Assert.Equal(2, dto.Entries.Count);
        Assert.Equal(0, dto.TotalQtyIn);
        Assert.Equal(15, dto.TotalQtyOut);
    }

    [Fact]
    public void VoucherLedgerDto_WorkOrder_ManufacturePattern()
    {
        // WO production creates: DR Inventory (FG), CR WIP/Expense (RM consumed)
        var dto = new VoucherLedgerDto
        {
            VoucherType = "WorkOrder",
            VoucherId = Guid.NewGuid(),
            Entries = new List<VoucherLedgerEntryDto>
            {
                new() { AccountCode = "1140", AccountName = "Inventory", DebitAmount = 500, CreditAmount = 0, Description = "FG receipt" },
                new() { AccountCode = "5100", AccountName = "COGS", DebitAmount = 0, CreditAmount = 500, Description = "RM consumed" },
            },
            TotalDebit = 500,
            TotalCredit = 500,
        };

        Assert.True(dto.IsBalanced);
        Assert.Equal(2, dto.Entries.Count);
    }

    [Fact]
    public void VoucherStockLedgerDto_PurchaseReceipt_TypicalPattern()
    {
        // A typical PR creates 1 SLE per item (stock-in)
        var dto = new VoucherStockLedgerDto
        {
            VoucherType = "PurchaseReceipt",
            VoucherId = Guid.NewGuid(),
            Entries = new List<VoucherStockLedgerEntryDto>
            {
                new() { ItemCode = "RM-001", ItemName = "Steel Sheet", WarehouseName = "Stores",
                         QuantityChange = 100, ValuationRate = 8.50m, BalanceQuantity = 350 },
                new() { ItemCode = "RM-002", ItemName = "Copper Wire", WarehouseName = "Stores",
                         QuantityChange = 50, ValuationRate = 22m, BalanceQuantity = 200 },
            },
            TotalQtyIn = 150,
            TotalQtyOut = 0,
            TotalValueDifference = 1950m,
        };

        Assert.Equal(2, dto.Entries.Count);
        Assert.Equal(150, dto.TotalQtyIn);
        Assert.Equal(0, dto.TotalQtyOut);
        Assert.Equal(1950m, dto.TotalValueDifference);
    }

    [Fact]
    public void VoucherLedgerDto_PurchaseReceipt_PerpetualInventoryGL()
    {
        // PR perpetual inventory: DR Stock, CR SRBNB (Stock Received But Not Billed)
        var dto = new VoucherLedgerDto
        {
            VoucherType = "PurchaseReceipt",
            VoucherId = Guid.NewGuid(),
            VoucherNumber = "PR-2026-00015",
            Entries = new List<VoucherLedgerEntryDto>
            {
                new() { AccountCode = "1140", AccountName = "Inventory", DebitAmount = 1950, CreditAmount = 0 },
                new() { AccountCode = "2150", AccountName = "Stock Received But Not Billed", DebitAmount = 0, CreditAmount = 1950 },
            },
            TotalDebit = 1950,
            TotalCredit = 1950,
        };

        Assert.True(dto.IsBalanced);
        Assert.Equal("PurchaseReceipt", dto.VoucherType);
    }

    [Fact]
    public void VoucherLedgerDto_PaymentEntry_ReceivePattern()
    {
        // Customer payment: DR Bank, CR Receivable
        var dto = new VoucherLedgerDto
        {
            VoucherType = "PaymentEntry",
            VoucherId = Guid.NewGuid(),
            VoucherNumber = "PE-2026-00088",
            Entries = new List<VoucherLedgerEntryDto>
            {
                new() { AccountCode = "1120", AccountName = "Bank", DebitAmount = 5000, CreditAmount = 0 },
                new() { AccountCode = "1130", AccountName = "Accounts Receivable", DebitAmount = 0, CreditAmount = 5000 },
            },
            TotalDebit = 5000,
            TotalCredit = 5000,
        };

        Assert.True(dto.IsBalanced);
        Assert.Equal("PE-2026-00088", dto.VoucherNumber);
    }

    [Fact]
    public void VoucherLedgerDto_PaymentEntry_PayWithTaxPattern()
    {
        // Supplier payment with tax: DR Payable, DR Tax, CR Bank
        var dto = new VoucherLedgerDto
        {
            VoucherType = "PaymentEntry",
            VoucherId = Guid.NewGuid(),
            Entries = new List<VoucherLedgerEntryDto>
            {
                new() { AccountCode = "2110", AccountName = "Accounts Payable", DebitAmount = 10000, CreditAmount = 0 },
                new() { AccountCode = "2210", AccountName = "WHT Payable", DebitAmount = 0, CreditAmount = 200 },
                new() { AccountCode = "1120", AccountName = "Bank", DebitAmount = 0, CreditAmount = 9800 },
            },
            TotalDebit = 10000,
            TotalCredit = 10000,
        };

        Assert.True(dto.IsBalanced);
        Assert.Equal(3, dto.Entries.Count);
    }

    [Fact]
    public void VoucherLedgerDto_JournalEntry_DirectPost()
    {
        // JE posts directly — IS the GL entry (no intermediary)
        var dto = new VoucherLedgerDto
        {
            VoucherType = "JournalEntry",
            VoucherId = Guid.NewGuid(),
            VoucherNumber = "JE-2026-00003",
            Entries = new List<VoucherLedgerEntryDto>
            {
                new() { AccountCode = "5500", AccountName = "Depreciation Expense", DebitAmount = 1200, CreditAmount = 0 },
                new() { AccountCode = "1220", AccountName = "Accumulated Depreciation", DebitAmount = 0, CreditAmount = 1200 },
            },
            TotalDebit = 1200,
            TotalCredit = 1200,
        };

        Assert.True(dto.IsBalanced);
        Assert.Equal("JournalEntry", dto.VoucherType);
    }

    [Fact]
    public void VoucherLedgerDto_DeliveryNote_PerpetualInventoryGL()
    {
        // DN perpetual inventory: DR COGS, CR Stock
        var dto = new VoucherLedgerDto
        {
            VoucherType = "DeliveryNote",
            VoucherId = Guid.NewGuid(),
            VoucherNumber = "DN-2026-00022",
            Entries = new List<VoucherLedgerEntryDto>
            {
                new() { AccountCode = "5100", AccountName = "Cost of Goods Sold", DebitAmount = 275, CreditAmount = 0 },
                new() { AccountCode = "1140", AccountName = "Inventory", DebitAmount = 0, CreditAmount = 275 },
            },
            TotalDebit = 275,
            TotalCredit = 275,
        };

        Assert.True(dto.IsBalanced);
        Assert.Equal("DN-2026-00022", dto.VoucherNumber);
    }

    [Fact]
    public void VoucherStockLedgerDto_StockEntry_TransferPattern()
    {
        // Material Transfer: source warehouse decreases, target increases (net zero)
        var dto = new VoucherStockLedgerDto
        {
            VoucherType = "StockEntry",
            VoucherId = Guid.NewGuid(),
            Entries = new List<VoucherStockLedgerEntryDto>
            {
                new() { ItemCode = "ITEM-001", ItemName = "Widget A", WarehouseName = "Stores",
                         QuantityChange = -20, ValuationRate = 15, BalanceQuantity = 80 },
                new() { ItemCode = "ITEM-001", ItemName = "Widget A", WarehouseName = "WIP",
                         QuantityChange = 20, ValuationRate = 15, BalanceQuantity = 20 },
            },
            TotalQtyIn = 20,
            TotalQtyOut = 20,
            TotalValueDifference = 0,
        };

        Assert.Equal(2, dto.Entries.Count);
        Assert.Equal(20, dto.TotalQtyIn);
        Assert.Equal(20, dto.TotalQtyOut);
        Assert.Equal(0, dto.TotalValueDifference);
    }
}
