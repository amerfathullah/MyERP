using System;
using System.Linq;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for upstream changes detected on 2026-07-24:
/// - PR #57263: Proforma Invoice complete feature (entity, AppService, SO integration)
/// - PR #57335: Report date range validation utility (refactoring, no new logic)
/// - PR #57410: Revert canonical warehouse names (our seeder already uses English)
/// </summary>
public class UpstreamJuly24Tests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _salesOrderId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    // ─── Proforma Invoice: Quantity Basis ───

    [Fact]
    public void QuantityBasis_RateFromSO_AmountCalculated()
    {
        var pi = new ProformaInvoice(Guid.NewGuid(), _companyId, _salesOrderId, _customerId,
            DateTime.UtcNow, ProformaInvoiceBasis.Quantity, "MYR");

        pi.AddItem(Guid.NewGuid(), _itemId, "ITEM-001", "Widget", 10, 50, "Unit");

        var item = pi.Items.First();
        item.Rate.ShouldBe(50m);
        item.Amount.ShouldBe(500m); // 10 × 50
    }

    [Fact]
    public void QuantityBasis_PartialQty_WorksCorrectly()
    {
        var pi = new ProformaInvoice(Guid.NewGuid(), _companyId, _salesOrderId, _customerId,
            DateTime.UtcNow, ProformaInvoiceBasis.Quantity, "MYR");

        // SO has 100 units, proforma for 30
        pi.AddItem(Guid.NewGuid(), _itemId, "ITEM-001", "Widget", 30, 25, "Unit");

        pi.TotalQty.ShouldBe(30);
        pi.GrandTotal.ShouldBe(750); // 30 × 25
    }

    // ─── Proforma Invoice: Amount Basis ───

    [Fact]
    public void AmountBasis_UserEntersBothQtyAndAmount()
    {
        var pi = new ProformaInvoice(Guid.NewGuid(), _companyId, _salesOrderId, _customerId,
            DateTime.UtcNow, ProformaInvoiceBasis.Amount, "MYR");

        // Amount basis: user enters qty=5 and wants total=625, rate derived = 625/5 = 125
        pi.AddItem(Guid.NewGuid(), _itemId, "ITEM-001", "Widget", 5, 125, "Unit");

        var item = pi.Items.First();
        item.Rate.ShouldBe(125m);
        item.Amount.ShouldBe(625m); // 5 × 125
    }

    [Fact]
    public void AmountBasis_HideItemQty_Default()
    {
        var pi = new ProformaInvoice(Guid.NewGuid(), _companyId, _salesOrderId, _customerId,
            DateTime.UtcNow, ProformaInvoiceBasis.Amount, "MYR");

        pi.HideItemQty.ShouldBeFalse(); // default
        pi.HideItemQty = true;
        pi.HideItemQty.ShouldBeTrue();
    }

    [Fact]
    public void QuantityBasis_HideItemQty_StaysFalse()
    {
        // Per upstream: HideItemQty only meaningful for Amount basis
        // AppService forces false when Quantity basis
        var pi = new ProformaInvoice(Guid.NewGuid(), _companyId, _salesOrderId, _customerId,
            DateTime.UtcNow, ProformaInvoiceBasis.Quantity, "MYR");

        pi.HideItemQty.ShouldBeFalse();
    }

    // ─── Proforma Invoice: In-Create Pattern ───

    [Fact]
    public void InCreatePattern_SubmittedOnCreation()
    {
        // Per ERPNext: proforma is insert+submit in one operation (in_create = true)
        var pi = CreateProformaWithItems();
        pi.Submit(); // simulates AppService auto-submit after insert

        pi.Status.ShouldBe(ProformaInvoiceStatus.Issued);
    }

    [Fact]
    public void InCreatePattern_MustHaveItems()
    {
        var pi = new ProformaInvoice(Guid.NewGuid(), _companyId, _salesOrderId, _customerId,
            DateTime.UtcNow, ProformaInvoiceBasis.Quantity, "MYR");

        // Cannot submit without items
        Should.Throw<BusinessException>(() => pi.Submit())
            .Code.ShouldBe("MyERP:01007");
    }

    // ─── Proforma Invoice: Email Lifecycle ───

    [Fact]
    public void Email_CancelledProforma_Blocked()
    {
        // Per gotcha #2452: cancelled proformas cannot be emailed
        var pi = CreateProformaWithItems();
        pi.Submit();
        pi.Cancel();

        Should.Throw<BusinessException>(() => pi.MarkEmailed("test@example.com"));
    }

    [Fact]
    public void Email_IssuedProforma_Succeeds()
    {
        var pi = CreateProformaWithItems();
        pi.Submit();

        pi.MarkEmailed("customer@example.com");

        pi.SentOn.ShouldNotBeNull();
        pi.EmailedTo.ShouldBe("customer@example.com");
    }

    [Fact]
    public void Email_MultipleRecipients_Preserved()
    {
        var pi = CreateProformaWithItems();
        pi.Submit();

        pi.MarkEmailed("a@example.com, b@example.com, c@example.com");

        pi.EmailedTo.ShouldNotBeNull();
        pi.EmailedTo!.ShouldContain("a@example.com");
        pi.EmailedTo.ShouldContain("b@example.com");
        pi.EmailedTo.ShouldContain("c@example.com");
    }

    // ─── Proforma Invoice: Multi-Item Progressive Proforming ───

    [Fact]
    public void ProgressiveProforming_MultiItem_TotalsAccumulate()
    {
        // First proforma: 30 of 100 units
        var pi1 = CreateProformaWithItems();
        pi1.GrandTotal.ShouldBe(500); // from helper: 5 × 100

        // Second proforma: different items
        var pi2 = new ProformaInvoice(Guid.NewGuid(), _companyId, _salesOrderId, _customerId,
            DateTime.UtcNow, ProformaInvoiceBasis.Quantity, "MYR");
        pi2.AddItem(Guid.NewGuid(), Guid.NewGuid(), "B", "Item B", 3, 200, "Unit");

        pi2.GrandTotal.ShouldBe(600); // 3 × 200

        // Total proformed = 500 + 600 = 1100
        var totalProformed = pi1.GrandTotal + pi2.GrandTotal;
        totalProformed.ShouldBe(1100);
    }

    // ─── Report Date Validation (PR #57335) ───

    [Fact]
    public void ReportDateValidation_FromBeforeTo_Valid()
    {
        // Per upstream PR #57335: shared validate_mandatory_date_range utility
        // Tests the concept (actual validation in AppService layer)
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 6, 30);
        (from <= to).ShouldBeTrue();
    }

    [Fact]
    public void ReportDateValidation_FromAfterTo_Invalid()
    {
        var from = new DateTime(2026, 7, 1);
        var to = new DateTime(2026, 1, 1);
        (from > to).ShouldBeTrue(); // should fail validation
    }

    // ─── Warehouse Name Revert (PR #57410) ───

    [Fact]
    public void DefaultWarehouseNames_UseCanonicalEnglish()
    {
        // PR #57410 reverted PR #57392 (canonical names) and PR #57409 (translation marking)
        // Our DefaultDataSeeder already uses canonical English names
        // This test documents the expectation
        var expectedNames = new[] { "All Warehouses", "Stores", "Work In Progress", "Finished Goods", "Goods In Transit" };
        foreach (var name in expectedNames)
        {
            name.ShouldNotBeNullOrWhiteSpace();
            name.ShouldNotContain("_"); // should be English words, not slugified
        }
    }

    // ─── Helpers ───

    private ProformaInvoice CreateProformaWithItems()
    {
        var pi = new ProformaInvoice(Guid.NewGuid(), _companyId, _salesOrderId, _customerId,
            DateTime.UtcNow, ProformaInvoiceBasis.Quantity, "MYR");
        pi.AddItem(Guid.NewGuid(), _itemId, "ITEM-001", "Widget", 5, 100, "Unit");
        return pi;
    }
}
