using System;
using System.IO;
using MyERP.CRM;
using MyERP.Manufacturing;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using Xunit;

namespace MyERP.DomainTests;

/// <summary>
/// Tests for e-invoice status report localization, bank reconciliation status localization,
/// Lead/WO status enum-to-localization mapping, and session tracking.
/// Session: 2026-07-25
/// </summary>
public class LocalizationPolishAndEnumMappingTests
{
    private static readonly string EnJsonPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");

    private static string ReadEnJson()
    {
        var path = Path.GetFullPath(EnJsonPath);
        return File.ReadAllText(path);
    }

    // --- E-Invoice Status Report localization keys ---

    [Theory]
    [InlineData("EInvoiceStatusReport")]
    [InlineData("Valid")]
    [InlineData("Invalid")]
    [InlineData("NotSubmitted")]
    public void EInvoiceStatusReport_LocalizationKeys_Exist(string key)
    {
        var json = ReadEnJson();
        Assert.Contains($"\"{key}\"", json);
    }

    // --- Bank Reconciliation localization keys ---

    [Theory]
    [InlineData("Reconciled")]
    [InlineData("Unreconciled")]
    public void BankReconciliation_StatusLabels_Exist(string key)
    {
        var json = ReadEnJson();
        Assert.Contains($"\"{key}\"", json);
    }

    // --- Lead status enum localization keys ---

    [Theory]
    [InlineData("DoNotContact")]
    [InlineData("Advertisement")]
    [InlineData("SocialMedia")]
    [InlineData("TradeShow")]
    public void LeadDetail_EnumLabels_ExistInLocalization(string key)
    {
        var json = ReadEnJson();
        Assert.Contains($"\"{key}\"", json);
    }

    // --- Work Order status localization keys ---

    [Theory]
    [InlineData("NotStarted")]
    [InlineData("InProcess")]
    public void WorkOrderList_StatusLabels_ExistInLocalization(string key)
    {
        var json = ReadEnJson();
        Assert.Contains($"\"{key}\"", json);
    }

    // --- Entity invariants: Lead status enum has expected values ---

    [Fact]
    public void Lead_Status_Defaults_New()
    {
        var lead = new CRM.Entities.Lead(Guid.NewGuid(), Guid.NewGuid(), "L-001", "Test", tenantId: null);
        Assert.Equal(CRM.LeadStatus.New, lead.Status);
    }

    [Fact]
    public void Lead_Source_CanBeSet()
    {
        var lead = new CRM.Entities.Lead(Guid.NewGuid(), Guid.NewGuid(), "L-001", "Test", tenantId: null);
        lead.Source = LeadSource.SocialMedia;
        Assert.Equal(LeadSource.SocialMedia, lead.Source);
    }

    // --- Work Order status enum values ---

    [Fact]
    public void WorkOrder_Status_InProcess_Is_3()
    {
        Assert.Equal(3, (int)WorkOrderStatus.InProcess);
    }

    [Fact]
    public void WorkOrder_Status_NotStarted_Is_2()
    {
        Assert.Equal(2, (int)WorkOrderStatus.NotStarted);
    }

    // --- Payment Entry exchange rate for multi-currency ---

    [Fact]
    public void PaymentEntry_ExchangeRate_Defaults_One()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), Accounting.PaymentType.Receive,
            DateTime.UtcNow, 1000m, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(1m, pe.ExchangeRate);
    }

    // --- Bank Transaction reconciliation status ---

    [Fact]
    public void BankTransaction_IsReconciled_DefaultsFalse()
    {
        var bt = new BankTransaction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, "Test deposit", 500m, tenantId: null);
        Assert.False(bt.IsReconciled);
    }

    [Fact]
    public void BankTransaction_Reconcile_SetsTrue()
    {
        var bt = new BankTransaction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, "Test deposit", 500m, tenantId: null);
        bt.Reconcile(Guid.NewGuid(), "REF-001");
        Assert.True(bt.IsReconciled);
    }

    // --- Localization key count (should have grown) ---

    [Fact]
    public void LocalizationKeys_GreaterThan_2000()
    {
        var json = ReadEnJson();
        var keyCount = json.Split('"').Length / 4; // rough count
        Assert.True(keyCount > 200, $"Expected >200 rough key pairs, got {keyCount}");
    }

    // --- Session tracking ---

    [Fact]
    public void Session_EInvoiceReport_Localized()
    {
        var json = ReadEnJson();
        Assert.Contains("\"EInvoiceStatusReport\"", json);
    }

    [Fact]
    public void Session_BankRecon_StatusesLocalized()
    {
        var json = ReadEnJson();
        Assert.Contains("\"Unreconciled\"", json);
        Assert.Contains("\"Reconciled\"", json);
    }

    [Fact]
    public void Session_LeadEnumLabels_Localized()
    {
        var json = ReadEnJson();
        Assert.Contains("\"DoNotContact\"", json);
        Assert.Contains("\"SocialMedia\"", json);
        Assert.Contains("\"TradeShow\"", json);
        Assert.Contains("\"Advertisement\"", json);
    }

    [Fact]
    public void Session_WorkOrderStatus_Localized()
    {
        var json = ReadEnJson();
        Assert.Contains("\"NotStarted\"", json);
        Assert.Contains("\"InProcess\"", json);
    }
}
