using System;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for POS consolidation logic and Finance Book entity.
/// </summary>
public class PosConsolidationAndFinanceBookTests
{
    // === POS Consolidation Result Tests ===

    [Fact]
    public void ConsolidationResult_Defaults()
    {
        var result = new ConsolidationResult();
        Assert.Equal(0, result.GrandTotal);
        Assert.Equal(0, result.SourceInvoiceCount);
        Assert.Empty(result.Items);
        Assert.Empty(result.SourceInvoiceIds);
    }

    [Fact]
    public void ConsolidatedItem_Properties()
    {
        var item = new ConsolidatedItem
        {
            ItemId = Guid.NewGuid(),
            Description = "Widget A",
            Quantity = 5,
            UnitPrice = 10.50m,
            Amount = 52.50m
        };
        Assert.Equal(5, item.Quantity);
        Assert.Equal(10.50m, item.UnitPrice);
        Assert.Equal(52.50m, item.Amount);
    }

    [Fact]
    public void ConsolidationResult_MultipleItems_SumsCorrectly()
    {
        var result = new ConsolidationResult
        {
            GrandTotal = 100m,
            NetTotal = 94.34m,
            TaxAmount = 5.66m,
            SourceInvoiceCount = 3,
            Items = new()
            {
                new ConsolidatedItem { Quantity = 2, UnitPrice = 25m, Amount = 50m },
                new ConsolidatedItem { Quantity = 5, UnitPrice = 10m, Amount = 50m }
            }
        };

        Assert.Equal(100m, result.GrandTotal);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(100m, result.Items.Sum(i => i.Amount));
    }

    [Fact]
    public void ConsolidationResult_ReturnsProduceNegativeQuantity()
    {
        var result = new ConsolidationResult
        {
            GrandTotal = 50m,
            Items = new()
            {
                new ConsolidatedItem { Quantity = 10, UnitPrice = 10m, Amount = 100m },
                new ConsolidatedItem { Quantity = -5, UnitPrice = 10m, Amount = -50m }
            }
        };

        var netQty = result.Items.Sum(i => i.Quantity);
        Assert.Equal(5, netQty);
    }

    [Fact]
    public void ConsolidationResult_SourceInvoiceIds_Tracked()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        var result = new ConsolidationResult
        {
            SourceInvoiceIds = new() { id1, id2, id3 },
            SourceInvoiceCount = 3
        };

        Assert.Equal(3, result.SourceInvoiceIds.Count);
        Assert.Contains(id1, result.SourceInvoiceIds);
        Assert.Contains(id2, result.SourceInvoiceIds);
        Assert.Contains(id3, result.SourceInvoiceIds);
    }

    // === POS Closing Entry Tests ===

    [Fact]
    public void PosClosingEntry_Submit_RecalculatesTotals()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
        entry.AddInvoice(Guid.NewGuid(), "POS-001", 100m);
        entry.AddInvoice(Guid.NewGuid(), "POS-002", 250m);
        entry.AddInvoice(Guid.NewGuid(), "POS-003", 75m);

        entry.Submit();

        Assert.Equal(425m, entry.GrandTotal);
        Assert.Equal(PosClosingStatus.Submitted, entry.Status);
    }

    [Fact]
    public void PosClosingEntry_Variance_Calculation()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());

        // Cashier counted RM 5 short on cash and RM 2 over on card
        entry.AddPayment(Guid.NewGuid(), "Cash", 500m, 495m);
        entry.AddPayment(Guid.NewGuid(), "Credit Card", 300m, 302m);

        // Variance: (500-495) + (300-302) = 5 + (-2) = 3 short total
        Assert.Equal(3m, entry.TotalDifference);
    }

    [Fact]
    public void PosClosingEntry_ConsolidatedInvoiceId_DefaultsNull()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(entry.ConsolidatedSalesInvoiceId);
    }

    [Fact]
    public void PosClosingEntry_ConsolidatedInvoiceId_CanBeSet()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
        var siId = Guid.NewGuid();
        entry.ConsolidatedSalesInvoiceId = siId;
        Assert.Equal(siId, entry.ConsolidatedSalesInvoiceId);
    }

    [Fact]
    public void PosClosingEntry_PostingDate_AlwaysToday()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(DateTime.UtcNow.Date, entry.PostingDate);
    }

    // === Finance Book Tests ===

    [Fact]
    public void FinanceBook_Create_DefaultsNotDefault()
    {
        var book = new FinanceBook(Guid.NewGuid(), Guid.NewGuid(), "Tax Depreciation");
        Assert.Equal("Tax Depreciation", book.Name);
        Assert.False(book.IsDefault);
        Assert.Null(book.Description);
    }

    [Fact]
    public void FinanceBook_Create_WithDescription()
    {
        var book = new FinanceBook(Guid.NewGuid(), Guid.NewGuid(), "IFRS Book")
        {
            Description = "International Financial Reporting Standards book"
        };
        Assert.Equal("IFRS Book", book.Name);
        Assert.Equal("International Financial Reporting Standards book", book.Description);
    }

    [Fact]
    public void FinanceBook_SetDefault()
    {
        var book = new FinanceBook(Guid.NewGuid(), Guid.NewGuid(), "Management Book")
        {
            IsDefault = true
        };
        Assert.True(book.IsDefault);
    }

    [Fact]
    public void FinanceBook_RequiresName()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FinanceBook(Guid.NewGuid(), Guid.NewGuid(), null!));
    }

    [Fact]
    public void FinanceBook_CompanyScoped()
    {
        var companyId = Guid.NewGuid();
        var book = new FinanceBook(Guid.NewGuid(), companyId, "Book A");
        Assert.Equal(companyId, book.CompanyId);
    }

    [Fact]
    public void FinanceBook_TenantId_Set()
    {
        var tenantId = Guid.NewGuid();
        var book = new FinanceBook(Guid.NewGuid(), Guid.NewGuid(), "Book B", tenantId);
        Assert.Equal(tenantId, book.TenantId);
    }

    [Fact]
    public void DepreciationScheduleEntry_FinanceBookId_DefaultNull()
    {
        var entry = new DepreciationScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, 1000m, 1000m);
        Assert.Null(entry.FinanceBookId);
    }

    [Fact]
    public void DepreciationScheduleEntry_FinanceBookId_CanBeSet()
    {
        var bookId = Guid.NewGuid();
        var entry = new DepreciationScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, 1000m, 1000m)
        {
            FinanceBookId = bookId
        };
        Assert.Equal(bookId, entry.FinanceBookId);
    }

    // === Integration Concept Tests ===

    [Fact]
    public void PosClosing_MultipleInvoices_DifferentAmounts()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
        entry.AddInvoice(Guid.NewGuid(), "POS-001", 15.90m);
        entry.AddInvoice(Guid.NewGuid(), "POS-002", 48.50m);
        entry.AddInvoice(Guid.NewGuid(), "POS-003", 200.00m);
        entry.AddInvoice(Guid.NewGuid(), "POS-004", 12.60m);
        entry.AddInvoice(Guid.NewGuid(), "POS-005", 89.00m);

        entry.Submit();
        Assert.Equal(366.00m, entry.GrandTotal);
        Assert.Equal(5, entry.Invoices.Count);
    }

    [Fact]
    public void WeightedAverageRate_FromMergedItems()
    {
        // Simulates merge: 3 units at RM10 + 2 units at RM15 = 5 units total
        var item = new ConsolidatedItem
        {
            Quantity = 5,
            Amount = 60m // 30 + 30
        };
        item.UnitPrice = item.Quantity != 0 ? item.Amount / item.Quantity : 0;
        Assert.Equal(12m, item.UnitPrice); // weighted avg
    }

    [Fact]
    public void ConsolidationResult_ZeroItems_EmptyList()
    {
        var result = new ConsolidationResult
        {
            SourceInvoiceCount = 0,
            Items = new()
        };
        Assert.Empty(result.Items);
        Assert.Equal(0, result.GrandTotal);
    }
}
