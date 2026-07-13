using System;
using MyERP.Accounting.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Accounting;

public class PaymentScheduleTests
{
    [Fact]
    public void PaymentScheduleEntry_Create_SetsFields()
    {
        var parentId = Guid.NewGuid();
        var entry = new PaymentScheduleEntry(
            Guid.NewGuid(), "SalesInvoice", parentId,
            new DateTime(2026, 2, 15), 50m, 5000m, "50% Advance");

        entry.ParentType.ShouldBe("SalesInvoice");
        entry.ParentId.ShouldBe(parentId);
        entry.DueDate.ShouldBe(new DateTime(2026, 2, 15));
        entry.InvoicePortion.ShouldBe(50m);
        entry.PaymentAmount.ShouldBe(5000m);
        entry.Description.ShouldBe("50% Advance");
    }

    [Fact]
    public void PaymentScheduleEntry_Outstanding_DefaultsToPaymentAmount()
    {
        var entry = new PaymentScheduleEntry(
            Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.Today, 100m, 10000m);

        entry.Outstanding.ShouldBe(10000m);
        entry.PaidAmount.ShouldBe(0m);
        entry.IsFullyPaid.ShouldBeFalse();
    }

    [Fact]
    public void PaymentScheduleEntry_RecordPayment_ReducesOutstanding()
    {
        var entry = new PaymentScheduleEntry(
            Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.Today, 100m, 10000m);

        var allocated = entry.RecordPayment(3000m);

        allocated.ShouldBe(3000m);
        entry.PaidAmount.ShouldBe(3000m);
        entry.Outstanding.ShouldBe(7000m);
        entry.IsFullyPaid.ShouldBeFalse();
    }

    [Fact]
    public void PaymentScheduleEntry_RecordPayment_FullPayment()
    {
        var entry = new PaymentScheduleEntry(
            Guid.NewGuid(), "PurchaseInvoice", Guid.NewGuid(),
            DateTime.Today, 100m, 5000m);

        entry.RecordPayment(5000m);

        entry.Outstanding.ShouldBe(0m);
        entry.IsFullyPaid.ShouldBeTrue();
    }

    [Fact]
    public void PaymentScheduleEntry_RecordPayment_OverpaymentCapped()
    {
        var entry = new PaymentScheduleEntry(
            Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.Today, 100m, 5000m);

        // Try to pay more than outstanding
        var allocated = entry.RecordPayment(8000m);

        allocated.ShouldBe(5000m); // Capped at outstanding
        entry.PaidAmount.ShouldBe(5000m);
        entry.Outstanding.ShouldBe(0m);
    }

    [Fact]
    public void PaymentScheduleEntry_RecordPayment_MultiplePayments()
    {
        var entry = new PaymentScheduleEntry(
            Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.Today, 100m, 10000m);

        entry.RecordPayment(3000m);
        entry.RecordPayment(4000m);
        entry.RecordPayment(3000m);

        entry.PaidAmount.ShouldBe(10000m);
        entry.IsFullyPaid.ShouldBeTrue();
    }

    [Fact]
    public void PaymentScheduleEntry_RecordPayment_ZeroOnFullyPaid()
    {
        var entry = new PaymentScheduleEntry(
            Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.Today, 100m, 1000m);

        entry.RecordPayment(1000m);
        var allocated = entry.RecordPayment(500m); // Already fully paid

        allocated.ShouldBe(0m); // Nothing left to allocate
    }

    [Fact]
    public void PaymentScheduleEntry_ForPurchaseInvoice()
    {
        var entry = new PaymentScheduleEntry(
            Guid.NewGuid(), "PurchaseInvoice", Guid.NewGuid(),
            new DateTime(2026, 3, 31), 30m, 3000m, "Progress Payment");

        entry.ParentType.ShouldBe("PurchaseInvoice");
        entry.InvoicePortion.ShouldBe(30m);
    }
}
