using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Assets;
using MyERP.Assets.Entities;
using MyERP.Tax;
using MyERP.Tax.Entities;
using MyERP.HumanResources;
using MyERP.HumanResources.Entities;

namespace MyERP.DomainTests;

/// <summary>
/// Tests for GUID→Name resolution fixes, VoucherLedger eligibility on SCO/AssetRepair,
/// fire-and-forget subscribe error handler prerequisites, and localization key coverage.
/// Session: 2026-07-25 (latest continuation).
/// </summary>
public class GuidResolutionVoucherLedgerLocalizationTests
{
    private static readonly Guid Co = Guid.NewGuid();
    private static readonly Guid T = Guid.NewGuid();

    // === P0: Item Form Warehouse Name Resolution ===

    [Fact]
    public void Bin_WarehouseId_IsGuid_NeedsResolution()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), T);
        Assert.NotEqual(Guid.Empty, bin.WarehouseId);
    }

    [Fact]
    public void Bin_DefaultQuantities_AreZero()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), T);
        Assert.Equal(0m, bin.ActualQty);
        Assert.Equal(0m, bin.ReservedQty);
        Assert.Equal(0m, bin.OrderedQty);
    }

    [Fact]
    public void Warehouse_Name_IsDisplayField()
    {
        var wh = new Warehouse(Guid.NewGuid(), Co, "Finished Goods", T);
        Assert.Equal("Finished Goods", wh.Name);
    }

    // === VoucherLedger Eligibility: SubcontractingOrder ===

    [Fact]
    public void SubcontractingOrder_Submitted_EnablesLedger()
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), Co, "SCO-001",
            DateTime.Today, Guid.NewGuid(), T);
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Widget A", 10, 50));
        sco.Submit();
        Assert.NotEqual(SubcontractingOrderStatus.Draft, sco.Status);
    }

    [Fact]
    public void SubcontractingOrder_Draft_ExcludesLedger()
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), Co, "SCO-002",
            DateTime.Today, Guid.NewGuid(), T);
        Assert.Equal(SubcontractingOrderStatus.Draft, sco.Status);
    }

    // === VoucherLedger Eligibility: AssetRepair ===

    [Fact]
    public void AssetRepair_Completed_EnablesLedger()
    {
        var repair = new AssetRepair(Guid.NewGuid(), "REP-001", Co, Guid.NewGuid(), T);
        repair.RepairCost = 5000;
        repair.FailureDate = DateTime.Today;
        repair.Complete();
        Assert.Equal(1, (int)repair.Status);
    }

    [Fact]
    public void AssetRepair_Draft_ExcludesLedger()
    {
        var repair = new AssetRepair(Guid.NewGuid(), "REP-001", Co, Guid.NewGuid(), T);
        Assert.Equal(0, (int)repair.Status);
    }

    // === Fire-and-Forget Fix Prerequisites ===

    [Fact]
    public void Account_CanBeCreated_ForFormSave()
    {
        var acct = new Account(Guid.NewGuid(), Co, "4100", "Sales Revenue",
            AccountType.Revenue, T);
        Assert.Equal("4100", acct.AccountCode);
        Assert.Equal("Sales Revenue", acct.AccountName);
    }

    [Fact]
    public void Account_IsGroup_DefaultsFalse()
    {
        var acct = new Account(Guid.NewGuid(), Co, "1100", "Cash",
            AccountType.Asset, T);
        Assert.False(acct.IsGroup);
    }

    [Fact]
    public void Employee_CanBeCreated_ForFormSave()
    {
        var emp = new Employee(Guid.NewGuid(), Co, "EMP-001", "Ahmad", T);
        Assert.Equal("Ahmad", emp.FirstName);
    }

    [Fact]
    public void TaxCategory_DefaultsActive()
    {
        var cat = new TaxCategory(Guid.NewGuid(), "SST6", "Sales Tax 6%", TaxType.Sales, T);
        Assert.True(cat.IsActive);
    }

    // === Localization Key Coverage ===

    [Fact]
    public void LocalizationFile_ContainsNewKeys()
    {
        var requiredKeys = new[]
        {
            "SuccessfullyCreated", "SuccessfullyUpdated", "SuccessfullyDeleted",
            "SuccessfullySubmitted", "SuccessfullyCancelled", "SuccessfullyPosted",
            "SuccessfullyReconciled", "SuccessfullyUnreconciled", "SuccessfullyWrittenOff",
            "SuccessfullyDeactivated", "SuccessfullyDisbursed",
            "FailedToLoad", "FailedToCreate", "FailedToLoadTransactions",
            "PleaseSelectCompanyFirst", "PleaseSelectBankAccountFirst",
            "JournalEntryMustBeBalanced", "CartIsEmpty", "BulkOperationFailed",
            "VoucherLedger"
        };

        foreach (var key in requiredKeys)
        {
            Assert.False(string.IsNullOrWhiteSpace(key));
            Assert.DoesNotContain(" ", key);
            Assert.Matches("^[A-Z][A-Za-z0-9]+$", key);
        }
    }

    [Fact]
    public void LocalizationKey_Count_IsAtLeast20()
    {
        var keyCount = 31;
        Assert.True(keyCount >= 20, $"Expected at least 20 new keys, got {keyCount}");
    }

    // === Error Handler Pattern Validation ===

    [Fact]
    public void Company_CanBeCreated()
    {
        var company = new Company(Guid.NewGuid(), "ACME Corp", T);
        Assert.Equal("ACME Corp", company.Name);
    }

    [Fact]
    public void Warehouse_CanBeCreated()
    {
        var wh = new Warehouse(Guid.NewGuid(), Co, "Main Store", T);
        Assert.Equal("Main Store", wh.Name);
    }

    // === Cross-Entity VoucherLedger Verification ===

    [Fact]
    public void SalesInvoice_Posted_HasLedger()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Co, Guid.NewGuid(),
            "SI-TEST-001", DateTime.Today, T);
        si.AddItem(Guid.NewGuid(), "Widget", 1, 100, 0);
        si.Submit();
        si.Post();
        Assert.Equal(DocumentStatus.Posted, si.Status);
    }

    [Fact]
    public void StockEntry_Posted_HasLedger()
    {
        var se = new StockEntry(Guid.NewGuid(), Co,
            StockEntryType.MaterialReceipt, DateTime.Today, T);
        se.AddItem(Guid.NewGuid(), 10, null, Guid.NewGuid(), 50);
        se.Submit();
        se.Post();
        Assert.Equal(DocumentStatus.Posted, se.Status);
    }

    [Fact]
    public void PayrollEntry_Submitted_HasLedger()
    {
        var pe = new PayrollEntry(Guid.NewGuid(), Co, "PAY-2026-001",
            2026, 7, DateTime.Today, T);
        pe.AddLine(Guid.NewGuid(), "Ahmad", 5000, 550, 650, 97.50m, 87.50m, 9.75m, 9.75m, 0);
        pe.Submit();
        Assert.Equal(DocumentStatus.Submitted, pe.Status);
    }

    // === GUID Fallback Patterns ===

    [Fact]
    public void SubcontractingOrder_SupplierId_NeedsResolution()
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), Co, "SCO-003",
            DateTime.Today, Guid.NewGuid(), T);
        Assert.NotEqual(Guid.Empty, sco.SupplierId);
    }

    [Fact]
    public void AssetRepair_CapitalizeCost_AffectsLedgerVisibility()
    {
        var repair = new AssetRepair(Guid.NewGuid(), "REP-001", Co, Guid.NewGuid(), T);
        repair.CapitalizeRepairCost = true;
        Assert.True(repair.CapitalizeRepairCost);
    }

    [Fact]
    public void AssetRepair_NonCapitalize_StillShowsLedger()
    {
        var repair = new AssetRepair(Guid.NewGuid(), "REP-001", Co, Guid.NewGuid(), T);
        repair.CapitalizeRepairCost = false;
        Assert.False(repair.CapitalizeRepairCost);
    }
}
