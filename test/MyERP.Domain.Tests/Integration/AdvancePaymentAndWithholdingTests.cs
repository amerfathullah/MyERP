using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Tax.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Tests.Integration;

public class AdvancePaymentAndWithholdingTests
{
    [Fact]
    public void SalesOrder_AdvancePaid_DefaultZero()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        so.AdvancePaid.ShouldBe(0m);
    }

    [Fact]
    public void SalesOrder_PerAdvancePaid_Calculated()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        so.GrandTotal = 1000m;
        so.AdvancePaid = 300m;
        so.PerAdvancePaid.ShouldBe(30m); // 300/1000 × 100 = 30%
    }

    [Fact]
    public void SalesOrder_PerAdvancePaid_ZeroGrandTotal()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        so.GrandTotal = 0m;
        so.AdvancePaid = 0m;
        so.PerAdvancePaid.ShouldBe(0m); // No division by zero
    }

    [Fact]
    public void PurchaseOrder_AdvancePaid_DefaultZero()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
        po.AdvancePaid.ShouldBe(0m);
    }

    [Fact]
    public void TaxWithholdingEntry_Create_CalculatesWithheldAmount()
    {
        var entry = new TaxWithholdingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PurchaseInvoice", Guid.NewGuid(), Guid.NewGuid(),
            withholdingRate: 10m, taxableAmount: 50000m, DateTime.UtcNow);

        // 50000 × 10% = 5000
        entry.WithheldAmount.ShouldBe(5000m);
        entry.Status.ShouldBe(DocumentStatus.Draft);
    }

    [Fact]
    public void TaxWithholdingEntry_ApplyLDC_ReducesRate()
    {
        var entry = new TaxWithholdingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PurchaseInvoice", Guid.NewGuid(), Guid.NewGuid(),
            withholdingRate: 15m, taxableAmount: 100000m, DateTime.UtcNow);

        // Original: 100000 × 15% = 15000
        entry.WithheldAmount.ShouldBe(15000m);

        // Apply LDC with reduced rate of 5%
        entry.ApplyLDC(5m, "LDC-2026-001");

        // Reduced: 100000 × 5% = 5000
        entry.WithheldAmount.ShouldBe(5000m);
        entry.HasLDC.ShouldBeTrue();
        entry.LdcRate.ShouldBe(5m);
        entry.CertificateNumber.ShouldBe("LDC-2026-001");
    }

    [Fact]
    public void TaxWithholdingEntry_Submit_Succeeds()
    {
        var entry = new TaxWithholdingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PaymentEntry", Guid.NewGuid(), Guid.NewGuid(),
            withholdingRate: 10m, taxableAmount: 20000m, DateTime.UtcNow);
        entry.Submit();
        entry.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void TaxWithholdingEntry_Cancel_AfterSubmit()
    {
        var entry = new TaxWithholdingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PurchaseInvoice", Guid.NewGuid(), Guid.NewGuid(),
            withholdingRate: 10m, taxableAmount: 30000m, DateTime.UtcNow);
        entry.Submit();
        entry.Cancel();
        entry.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void TaxWithholdingEntry_DoubleSubmit_Throws()
    {
        var entry = new TaxWithholdingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PurchaseInvoice", Guid.NewGuid(), Guid.NewGuid(),
            withholdingRate: 10m, taxableAmount: 30000m, DateTime.UtcNow);
        entry.Submit();
        Should.Throw<BusinessException>(() => entry.Submit());
    }

    [Fact]
    public void DiscountPercentage_ToAmount_Conversion()
    {
        // Simulate: 5% discount on net total of RM 20,000
        var netTotal = 20000m;
        var discountPct = 5m;
        var discountAmt = Math.Round(netTotal * discountPct / 100m, 2);
        discountAmt.ShouldBe(1000m);
    }
}
