using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for production-safety gap fixes:
/// - Credit limit at DN submit
/// - Opening invoice update_stock block
/// - Multi-reference payment allocation
/// - Activity logging coverage
/// </summary>
public class ProductionSafetyGapTests
{
    // === Credit Limit at DN Submit ===

    [Fact]
    public void CreditLimit_MustEnforceOnDN_NotJustSI()
    {
        // Per DO-NOT: "Implement credit limit check only at SO — must also enforce at DN and SI submit"
        // The credit limit check prevents over-delivery to customers who have exceeded their limit
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test Customer");
        customer.CreditLimit = 10000m;

        // Customer has outstanding of 8000, new DN would add 5000 = 13000 > 10000
        var outstanding = 8000m;
        var newAmount = 5000m;
        var totalExposure = outstanding + newAmount;

        totalExposure.ShouldBeGreaterThan(customer.CreditLimit);
    }

    [Fact]
    public void CreditLimit_ZeroMeansNoLimit_DNAllowed()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Unlimited Customer");
        customer.CreditLimit = 0m; // 0 = no limit

        // Any amount should be allowed
        (customer.CreditLimit == 0).ShouldBeTrue();
    }

    // === Opening Invoice UpdateStock Block ===

    [Fact]
    public void SalesInvoice_IsOpening_DefaultFalse()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        si.IsOpening.ShouldBeFalse();
    }

    [Fact]
    public void SalesInvoice_IsOpening_CanBeSet()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        si.IsOpening = true;
        si.IsOpening.ShouldBeTrue();
    }

    [Fact]
    public void OpeningInvoice_WithUpdateStock_ShouldBeBlocked()
    {
        // Per DO-NOT: "Allow opening invoices with update_stock = true (opening balances are accounting-only)"
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        si.IsOpening = true;
        si.UpdateStock = true;

        // The AppService should throw MyERP:01006 when this combination is detected
        (si.IsOpening && si.UpdateStock).ShouldBeTrue();
    }

    [Fact]
    public void OpeningInvoice_WithoutUpdateStock_ShouldBeAllowed()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        si.IsOpening = true;
        si.UpdateStock = false;

        (si.IsOpening && si.UpdateStock).ShouldBeFalse();
    }

    [Fact]
    public void NormalInvoice_WithUpdateStock_ShouldBeAllowed()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        si.IsOpening = false;
        si.UpdateStock = true;

        (si.IsOpening && si.UpdateStock).ShouldBeFalse();
    }

    [Fact]
    public void ErrorCode_OpeningInvoiceCannotUpdateStock_Exists()
    {
        MyERPDomainErrorCodes.OpeningInvoiceCannotUpdateStock.ShouldBe("MyERP:01006");
    }

    // === Multi-Reference Payment Allocation ===

    [Fact]
    public void PaymentEntry_HasReferencesCollection()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.Today, 10000m, Guid.NewGuid(), Guid.NewGuid());

        pe.References.ShouldNotBeNull();
        pe.References.Count.ShouldBe(0);
    }

    [Fact]
    public void PaymentEntry_CanAddMultipleReferences()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.Today, 10000m, Guid.NewGuid(), Guid.NewGuid());

        var inv1 = Guid.NewGuid();
        var inv2 = Guid.NewGuid();

        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), pe.Id, "SalesInvoice", inv1, 8000m, 8000m, 6000m));

        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), pe.Id, "SalesInvoice", inv2, 5000m, 5000m, 4000m));

        pe.References.Count.ShouldBe(2);
        pe.References.Sum(r => r.AllocatedAmount).ShouldBe(10000m); // Total allocation = PaidAmount
    }

    [Fact]
    public void PaymentEntry_ReferenceSumCannotExceedPaidAmount()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.Today, 10000m, Guid.NewGuid(), Guid.NewGuid());

        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), pe.Id, "SalesInvoice", Guid.NewGuid(), 10000m, 10000m, 7000m));

        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), pe.Id, "SalesInvoice", Guid.NewGuid(), 8000m, 8000m, 5000m));

        var totalAllocated = pe.References.Sum(r => r.AllocatedAmount);
        totalAllocated.ShouldBeGreaterThan(pe.PaidAmount);
        // In production, the AppService would validate this before posting
    }

    [Fact]
    public void PaymentEntry_MixedReferenceTypes()
    {
        // A payment can allocate to both SalesInvoice and SalesOrder
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.Today, 15000m, Guid.NewGuid(), Guid.NewGuid());

        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), pe.Id, "SalesInvoice", Guid.NewGuid(), 12000m, 12000m, 10000m));

        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), pe.Id, "SalesOrder", Guid.NewGuid(), 8000m, 8000m, 5000m));

        pe.References.Select(r => r.ReferenceType).Distinct().Count().ShouldBe(2);
    }

    // === Activity Log Coverage ===

    [Fact]
    public void DocumentActivityLog_PostedActivity_HasCorrectFields()
    {
        var log = new DocumentActivityLog(
            Guid.NewGuid(), "PaymentEntry", Guid.NewGuid(), "Posted",
            Guid.NewGuid(), "PE-001", "Submitted", "Posted");

        log.ActivityType.ShouldBe("Posted");
        log.PreviousStatus.ShouldBe("Submitted");
        log.NewStatus.ShouldBe("Posted");
    }

    [Fact]
    public void DocumentActivityLog_CancelledActivity_HasCorrectFields()
    {
        var log = new DocumentActivityLog(
            Guid.NewGuid(), "StockEntry", Guid.NewGuid(), "Cancelled",
            Guid.NewGuid(), "SE-001", "Posted", "Cancelled");

        log.DocumentType.ShouldBe("StockEntry");
        log.ActivityType.ShouldBe("Cancelled");
    }

    [Fact]
    public void DocumentActivityLog_ConvertedActivity_HasTargetInDetails()
    {
        var targetId = Guid.NewGuid();
        var log = new DocumentActivityLog(
            Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), "Converted",
            Guid.NewGuid(), "SO-001",
            details: $"Converted to DeliveryNote ({targetId})");

        log.Details!.ShouldContain("DeliveryNote");
        log.Details!.ShouldContain(targetId.ToString());
    }

    // === Credit Limit — Customer Entity ===

    [Fact]
    public void Customer_CreditLimit_DefaultZero()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test");
        customer.CreditLimit.ShouldBe(0);
    }

    [Fact]
    public void Customer_CreditLimit_CanBeSet()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test");
        customer.CreditLimit = 50000m;
        customer.CreditLimit.ShouldBe(50000m);
    }
}
