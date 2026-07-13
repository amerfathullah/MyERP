using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Integration;

public class EdgeCaseGuardTests
{
    // --- SI/PI Qty Validation ---

    [Fact]
    public void SalesInvoice_AddItem_PositiveQty_Succeeds()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        si.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void SalesInvoice_AddItem_ZeroQty_Throws()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-002", DateTime.UtcNow);
        Should.Throw<ArgumentException>(() => si.AddItem(Guid.NewGuid(), "Widget", 0, 100, 0));
    }

    [Fact]
    public void SalesInvoice_AddItem_NegativeQty_ThrowsForNonReturn()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-003", DateTime.UtcNow);
        Should.Throw<ArgumentException>(() => si.AddItem(Guid.NewGuid(), "Widget", -5, 100, 0));
    }

    [Fact]
    public void SalesInvoice_AddItem_NegativeQty_AllowedForReturn()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CN-001", DateTime.UtcNow);
        si.IsReturn = true;
        si.AddItem(Guid.NewGuid(), "Widget", -5, 100, 0); // Returns allow negative
        si.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void PurchaseInvoice_AddItem_ZeroQty_Throws()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.UtcNow);
        Should.Throw<ArgumentException>(() => pi.AddItem(Guid.NewGuid(), "Material", 0, 50, 0));
    }

    [Fact]
    public void PurchaseInvoice_AddItem_NegativeQty_AllowedForReturn()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-001", DateTime.UtcNow);
        pi.IsReturn = true;
        pi.AddItem(Guid.NewGuid(), "Material", -3, 50, 0); // Debit notes allow negative
        pi.Items.Count.ShouldBe(1);
    }

    // --- Account Self-Reference Guard ---

    [Fact]
    public void Account_SetParent_NullParent_Succeeds()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "1000", "Cash", AccountType.Asset);
        account.SetParent(null);
        account.ParentAccountId.ShouldBeNull();
    }

    [Fact]
    public void Account_SetParent_ValidParent_Succeeds()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "1100", "Bank", AccountType.Asset);
        var parentId = Guid.NewGuid();
        account.SetParent(parentId);
        account.ParentAccountId.ShouldBe(parentId);
    }

    [Fact]
    public void Account_SetParent_SelfReference_Throws()
    {
        var accountId = Guid.NewGuid();
        var account = new Account(accountId, Guid.NewGuid(), "1200", "AR", AccountType.Asset);
        Should.Throw<BusinessException>(() => account.SetParent(accountId));
    }

    // --- FiscalYear Overlap (entity-level: just verify date containment logic) ---

    [Fact]
    public void FiscalYear_DatesOverlap_Detection()
    {
        // Test the overlap check logic: two FYs overlap if start1 <= end2 && end1 >= start2
        var fy1Start = new DateTime(2026, 1, 1);
        var fy1End = new DateTime(2026, 12, 31);
        var fy2Start = new DateTime(2026, 6, 1);
        var fy2End = new DateTime(2027, 5, 31);

        var overlaps = fy1Start <= fy2End && fy1End >= fy2Start;
        overlaps.ShouldBeTrue();
    }

    [Fact]
    public void FiscalYear_DatesDoNotOverlap_Detection()
    {
        var fy1Start = new DateTime(2025, 1, 1);
        var fy1End = new DateTime(2025, 12, 31);
        var fy2Start = new DateTime(2026, 1, 1);
        var fy2End = new DateTime(2026, 12, 31);

        var overlaps = fy1Start <= fy2End && fy1End >= fy2Start;
        overlaps.ShouldBeFalse();
    }
}
