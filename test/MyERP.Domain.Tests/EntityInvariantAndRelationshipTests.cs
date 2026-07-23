using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.HumanResources.Entities;
using MyERP.Manufacturing.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for entity invariants, lifecycle guards, and cross-entity relationships
/// added during the final migration hardening phase.
/// </summary>
public class EntityInvariantAndRelationshipTests
{
    private static readonly Guid TestTenant = Guid.NewGuid();
    private static readonly Guid TestCompany = Guid.NewGuid();

    // ──── PutawayRule ────

    [Fact]
    public void PutawayRule_UnlimitedCapacity_ReturnsMaxValue()
    {
        var rule = new PutawayRule(Guid.NewGuid(), TestCompany, Guid.NewGuid(), TestTenant)
        {
            StockCapacity = 0
        };
        Assert.Equal(decimal.MaxValue, rule.GetAvailableCapacity(100));
    }

    [Fact]
    public void PutawayRule_FiniteCapacity_SubtractsBalance()
    {
        var rule = new PutawayRule(Guid.NewGuid(), TestCompany, Guid.NewGuid(), TestTenant)
        {
            StockCapacity = 500
        };
        Assert.Equal(200, rule.GetAvailableCapacity(300));
    }

    [Fact]
    public void PutawayRule_OverCapacity_ReturnsZero()
    {
        var rule = new PutawayRule(Guid.NewGuid(), TestCompany, Guid.NewGuid(), TestTenant)
        {
            StockCapacity = 100
        };
        Assert.Equal(0, rule.GetAvailableCapacity(150));
    }

    [Fact]
    public void PutawayRule_DefaultPriority_IsOne()
    {
        var rule = new PutawayRule(Guid.NewGuid(), TestCompany, Guid.NewGuid());
        Assert.Equal(1, rule.Priority);
    }

    [Fact]
    public void PutawayRule_DefaultEnabled()
    {
        var rule = new PutawayRule(Guid.NewGuid(), TestCompany, Guid.NewGuid());
        Assert.True(rule.IsEnabled);
    }

    // ──── InstallationNote ────

    [Fact]
    public void InstallationNote_Create_DefaultsDraft()
    {
        var note = new InstallationNote(Guid.NewGuid(), TestCompany, "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(DocumentStatus.Draft, note.Status);
        Assert.Empty(note.Items);
    }

    [Fact]
    public void InstallationNote_AddItem_IncreasesCount()
    {
        var note = new InstallationNote(Guid.NewGuid(), TestCompany, "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        note.AddItem(Guid.NewGuid(), 5);
        Assert.Single(note.Items);
        Assert.Equal(5, note.Items[0].Qty);
    }

    [Fact]
    public void InstallationNote_AddItem_ZeroQty_Throws()
    {
        var note = new InstallationNote(Guid.NewGuid(), TestCompany, "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() => note.AddItem(Guid.NewGuid(), 0));
    }

    [Fact]
    public void InstallationNote_ValidateDate_BeforeDN_Throws()
    {
        var note = new InstallationNote(Guid.NewGuid(), TestCompany, "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 1, 1));
        var dnDate = new DateTime(2026, 6, 1);
        Assert.Throws<Volo.Abp.BusinessException>(() => note.ValidateInstallationDate(dnDate));
    }

    [Fact]
    public void InstallationNote_ValidateDate_SameDay_Succeeds()
    {
        var date = new DateTime(2026, 6, 1);
        var note = new InstallationNote(Guid.NewGuid(), TestCompany, "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), date);
        // Should not throw
        note.ValidateInstallationDate(date);
    }

    // ──── Uom ────

    [Fact]
    public void Uom_WholeNumber_IntegerPasses()
    {
        var uom = new Uom(Guid.NewGuid(), "Box") { MustBeWholeNumber = true };
        uom.ValidateWholeNumber(5m); // Should not throw
    }

    [Fact]
    public void Uom_WholeNumber_FractionalThrows()
    {
        var uom = new Uom(Guid.NewGuid(), "Box") { MustBeWholeNumber = true };
        Assert.Throws<Volo.Abp.BusinessException>(() => uom.ValidateWholeNumber(5.5m));
    }

    [Fact]
    public void Uom_NotWholeNumber_FractionalAllowed()
    {
        var uom = new Uom(Guid.NewGuid(), "Litre") { MustBeWholeNumber = false };
        uom.ValidateWholeNumber(3.75m); // Should not throw
    }

    [Fact]
    public void Uom_WholeNumber_Tolerance()
    {
        var uom = new Uom(Guid.NewGuid(), "Unit") { MustBeWholeNumber = true };
        // 0.0000001 tolerance — tiny remainder passes
        uom.ValidateWholeNumber(5.00000005m); // Should not throw (within tolerance)
    }

    [Fact]
    public void Uom_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Uom(Guid.NewGuid(), ""));
    }

    // ──── Address ────

    [Fact]
    public void Address_DefaultFields()
    {
        var addr = new Address(Guid.NewGuid(), "Office", "Customer", Guid.NewGuid(), "123 Main St", "MYS");
        Assert.Equal("Customer", addr.PartyType);
        Assert.False(addr.IsPrimaryAddress);
        Assert.False(addr.IsShippingAddress);
    }

    [Fact]
    public void Address_PrimaryFlag_CanBeSet()
    {
        var addr = new Address(Guid.NewGuid(), "Office", "Customer", Guid.NewGuid(), "123 Main St", "MYS")
        {
            IsPrimaryAddress = true
        };
        Assert.True(addr.IsPrimaryAddress);
    }

    // ──── Contact ────

    [Fact]
    public void Contact_DefaultFields()
    {
        var contact = new Contact(Guid.NewGuid(), "John", "Customer", Guid.NewGuid());
        Assert.Equal("Customer", contact.PartyType);
        Assert.False(contact.IsPrimaryContact);
    }

    [Fact]
    public void Contact_PrimaryFlag_CanBeSet()
    {
        var contact = new Contact(Guid.NewGuid(), "John", "Customer", Guid.NewGuid())
        {
            IsPrimaryContact = true
        };
        Assert.True(contact.IsPrimaryContact);
    }

    // ──── ModeOfPayment ────

    [Fact]
    public void ModeOfPayment_DefaultType()
    {
        var mop = new ModeOfPayment(Guid.NewGuid(), "Cash", "Cash");
        Assert.Equal("Cash", mop.Name);
    }

    // ──── CostCenter ────

    [Fact]
    public void CostCenter_GroupVsLeaf()
    {
        var group = new CostCenter(Guid.NewGuid(), TestCompany, "Root")
        {
            IsGroup = true
        };
        var leaf = new CostCenter(Guid.NewGuid(), TestCompany, "Sales")
        {
            IsGroup = false,
            ParentId = group.Id
        };
        Assert.True(group.IsGroup);
        Assert.False(leaf.IsGroup);
        Assert.Equal(group.Id, leaf.ParentId);
    }

    // ──── ItemDefault ────

    [Fact]
    public void ItemDefault_AllFieldsNullable()
    {
        var def = new ItemDefault(Guid.NewGuid(), Guid.NewGuid(), TestCompany);
        Assert.Null(def.DefaultWarehouseId);
        Assert.Null(def.IncomeAccountId);
        Assert.Null(def.ExpenseAccountId);
        Assert.Null(def.BuyingCostCenterId);
        Assert.Null(def.SellingCostCenterId);
        Assert.Null(def.DefaultSupplierId);
    }

    [Fact]
    public void ItemDefault_CanSetAllFields()
    {
        var wh = Guid.NewGuid();
        var income = Guid.NewGuid();
        var expense = Guid.NewGuid();
        var def = new ItemDefault(Guid.NewGuid(), Guid.NewGuid(), TestCompany)
        {
            DefaultWarehouseId = wh,
            IncomeAccountId = income,
            ExpenseAccountId = expense
        };
        Assert.Equal(wh, def.DefaultWarehouseId);
        Assert.Equal(income, def.IncomeAccountId);
        Assert.Equal(expense, def.ExpenseAccountId);
    }

    // ──── AccountingPeriod Per-DocType ────

    [Fact]
    public void AccountingPeriod_PerDocType_Closed()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), TestCompany, "Q1-2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31));
        period.CloseDocumentType("SalesInvoice");
        Assert.True(period.IsClosedForDocumentType("SalesInvoice"));
        Assert.False(period.IsClosedForDocumentType("PurchaseInvoice"));
    }

    [Fact]
    public void AccountingPeriod_PerDocType_CaseInsensitive()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), TestCompany, "Q1-2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31));
        period.CloseDocumentType("SalesInvoice");
        Assert.True(period.IsClosedForDocumentType("salesinvoice"));
    }

    [Fact]
    public void AccountingPeriod_Reopen_SpecificType()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), TestCompany, "Q1-2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31));
        period.CloseDocumentType("SalesInvoice");
        period.CloseDocumentType("PurchaseInvoice");
        period.ReopenDocumentType("SalesInvoice");
        Assert.False(period.IsClosedForDocumentType("SalesInvoice"));
        Assert.True(period.IsClosedForDocumentType("PurchaseInvoice"));
    }

    // ──── PaymentScheduleEntry ────

    [Fact]
    public void PaymentScheduleEntry_RecordPayment_ReducesOutstanding()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100m, 1000m);
        var allocated = entry.RecordPayment(600m);
        Assert.Equal(600m, allocated);
        Assert.Equal(600m, entry.PaidAmount);
    }

    [Fact]
    public void PaymentScheduleEntry_RecordPayment_CapsAtOutstanding()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100m, 1000m);
        entry.RecordPayment(800m);
        var allocated = entry.RecordPayment(500m);
        Assert.Equal(200m, allocated); // Capped at outstanding (1000 - 800)
        Assert.Equal(1000m, entry.PaidAmount);
    }

    [Fact]
    public void PaymentScheduleEntry_FullyPaid_Outstanding_Zero()
    {
        var entry = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100m, 1000m);
        entry.RecordPayment(1000m);
        Assert.Equal(0m, entry.PaymentAmount - entry.PaidAmount);
    }

    // ──── SalesOrder Close/Reopen ────

    [Fact]
    public void SalesOrder_Close_FromActive_Succeeds()
    {
        var so = CreateSalesOrder();
        so.Submit();
        so.UpdateFulfillmentStatus();
        so.Close();
        Assert.Equal(DocumentStatus.Closed, so.Status);
    }

    [Fact]
    public void SalesOrder_Reopen_FromClosed_Succeeds()
    {
        var so = CreateSalesOrder();
        so.Submit();
        so.UpdateFulfillmentStatus();
        so.Close();
        so.Reopen();
        Assert.NotEqual(DocumentStatus.Closed, so.Status);
    }

    [Fact]
    public void SalesOrder_Close_FromDraft_Throws()
    {
        var so = CreateSalesOrder();
        Assert.Throws<Volo.Abp.BusinessException>(() => so.Close());
    }

    // ──── PurchaseOrder Close/Reopen ────

    [Fact]
    public void PurchaseOrder_Close_FromActive_Succeeds()
    {
        var po = CreatePurchaseOrder();
        po.Submit();
        po.UpdateFulfillmentStatus();
        po.Close();
        Assert.Equal(DocumentStatus.Closed, po.Status);
    }

    [Fact]
    public void PurchaseOrder_Reopen_RecalculatesStatus()
    {
        var po = CreatePurchaseOrder();
        po.Submit();
        po.UpdateFulfillmentStatus();
        po.Close();
        po.Reopen();
        Assert.NotEqual(DocumentStatus.Closed, po.Status);
    }

    // ──── LeaveAllocation ────

    [Fact]
    public void LeaveAllocation_DeductLeave_ReducesBalance()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), TestCompany, Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        alloc.DeductLeave(3);
        Assert.Equal(3m, alloc.LeavesUsed);
        Assert.Equal(9m, alloc.Balance);
    }

    [Fact]
    public void LeaveAllocation_RestoreLeave_IncreasesBalance()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), TestCompany, Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        alloc.DeductLeave(5);
        alloc.RestoreLeave(3);
        Assert.Equal(2m, alloc.LeavesUsed);
        Assert.Equal(10m, alloc.Balance);
    }

    [Fact]
    public void LeaveAllocation_Restore_NeverNegativeUsed()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), TestCompany, Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        alloc.DeductLeave(2);
        alloc.RestoreLeave(5); // More than used
        Assert.Equal(0m, alloc.LeavesUsed);
    }

    [Fact]
    public void LeaveAllocation_CarryForward_AddsToBalance()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), TestCompany, Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12)
        {
            CarryForwardDays = 5
        };
        Assert.Equal(17m, alloc.Balance); // 12 + 5
    }

    [Fact]
    public void LeaveAllocation_ExpiredCarryForward_Excluded()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), TestCompany, Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12)
        {
            CarryForwardDays = 5,
            CarryForwardExpiryDate = DateTime.UtcNow.AddDays(-1) // expired
        };
        Assert.Equal(12m, alloc.Balance); // CF excluded
    }

    // ──── BomOperation ────

    [Fact]
    public void BomOperation_CalculateCost()
    {
        var bomId = Guid.NewGuid();
        var opId = Guid.NewGuid();
        var op = new BomOperation(Guid.NewGuid(), bomId, opId, 10, 60m);
        op.CalculateCost(100m); // 100 RM/hour
        Assert.Equal(100m, op.OperatingCost); // 60min / 60 × 100
    }

    [Fact]
    public void BomOperation_CalculateCost_HalfHour()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 20, 30m);
        op.CalculateCost(200m); // 200 RM/hour
        Assert.Equal(100m, op.OperatingCost); // 30/60 × 200
    }

    [Fact]
    public void BomOperation_BatchSize_JobCardCount()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 60m)
        {
            BatchSize = 25
        };
        Assert.Equal(4, op.GetJobCardCount(100)); // 100/25 = 4
        Assert.Equal(5, op.GetJobCardCount(110)); // ceil(110/25) = 5
    }

    [Fact]
    public void BomOperation_ZeroBatchSize_SingleJobCard()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 60m)
        {
            BatchSize = 0
        };
        Assert.Equal(1, op.GetJobCardCount(999)); // 0 = single JC
    }

    // ──── Helpers ────

    private static SalesOrder CreateSalesOrder()
    {
        var so = new SalesOrder(Guid.NewGuid(), TestCompany, Guid.NewGuid(),
            "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Test Item", 10, 100m, 0m);
        return so;
    }

    private static PurchaseOrder CreatePurchaseOrder()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), TestCompany, Guid.NewGuid(),
            "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Test Material", 10, 50m, 0m);
        return po;
    }
}
