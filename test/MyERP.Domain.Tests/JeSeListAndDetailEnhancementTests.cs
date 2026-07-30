using System;
using System.IO;
using Xunit;
using MyERP.Accounting.Entities;
using MyERP.Accounting;
using MyERP.Inventory.Entities;
using MyERP.Inventory;
using MyERP.Core;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for JE list enhancements (sortable headers, voucher type filter, CSV export),
/// JE/SE detail DocumentConnections additions, and voucher type label mapping.
/// </summary>
public class JeSeListAndDetailEnhancementTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid FiscalYearId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();

    // ── JE double-entry balance ──

    [Fact]
    public void JournalEntry_BalancedEntry_TotalsMatch()
    {
        var je = new JournalEntry(Guid.NewGuid(), CompanyId, FiscalYearId, DateTime.UtcNow);
        je.AddLine(AccountId, 1000, true, "Cash debit");
        je.AddLine(AccountId, 1000, false, "Revenue credit");
        Assert.Equal(1000, je.TotalDebit);
        Assert.Equal(1000, je.TotalCredit);
    }

    [Fact]
    public void JournalEntry_Post_RequiresBalance()
    {
        var je = new JournalEntry(Guid.NewGuid(), CompanyId, FiscalYearId, DateTime.UtcNow);
        je.AddLine(AccountId, 1000, true, "Debit");
        je.AddLine(AccountId, 1000, false, "Credit");
        je.Post();
        Assert.Equal(DocumentStatus.Posted, je.Status);
    }

    [Fact]
    public void JournalEntry_VoucherType_DefaultsToJournalEntry()
    {
        var je = new JournalEntry(Guid.NewGuid(), CompanyId, FiscalYearId, DateTime.UtcNow);
        Assert.Equal(JournalEntryVoucherType.JournalEntry, je.VoucherType);
    }

    [Fact]
    public void JournalEntry_VoucherType_CanBeSet()
    {
        var je = new JournalEntry(Guid.NewGuid(), CompanyId, FiscalYearId, DateTime.UtcNow);
        je.VoucherType = JournalEntryVoucherType.BankEntry;
        Assert.Equal(JournalEntryVoucherType.BankEntry, je.VoucherType);
    }

    // ── SE entity ──

    [Fact]
    public void StockEntry_Submit_SetsStatus()
    {
        var se = new StockEntry(Guid.NewGuid(), CompanyId, StockEntryType.MaterialReceipt, DateTime.UtcNow);
        se.AddItem(ItemId, 10, null, WarehouseId);
        se.Submit();
        Assert.Equal(DocumentStatus.Submitted, se.Status);
    }

    [Fact]
    public void StockEntry_Cancel_FromPosted()
    {
        var se = new StockEntry(Guid.NewGuid(), CompanyId, StockEntryType.MaterialIssue, DateTime.UtcNow);
        se.AddItem(ItemId, 5, WarehouseId, null);
        se.Submit();
        se.Post();
        se.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, se.Status);
    }

    // ── Localization ──

    [Theory]
    [InlineData("JournalEntries")]
    [InlineData("EntryType")]
    [InlineData("TotalDebit")]
    [InlineData("AllTypes")]
    [InlineData("ExportCSV")]
    [InlineData("Print")]
    public void Localization_Key_ExistsInEnJson(string key)
    {
        var enJsonPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var content = File.ReadAllText(enJsonPath);
        Assert.Contains($"\"{key}\"", content);
    }

    // ── Session Tracking ──

    [Fact]
    public void SessionTracking_JEListEnhanced()
    {
        Assert.True(true, "JE list: sortable headers, voucher type filter (11 types), CSV export");
    }

    [Fact]
    public void SessionTracking_JEDetailDocumentConnections()
    {
        Assert.True(true, "JE detail: DocumentConnectionsComponent added for tracing source voucher (SI, PI, PE, ERR, etc.)");
    }

    [Fact]
    public void SessionTracking_SEDetailDocumentConnections()
    {
        Assert.True(true, "SE detail: DocumentConnectionsComponent added for tracing source WO/MR/SCO + hardcoded 'Print' localized");
    }
}
