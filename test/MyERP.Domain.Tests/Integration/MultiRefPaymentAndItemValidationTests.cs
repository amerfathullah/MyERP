using System;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Integration;

public class MultiRefPaymentAndItemValidationTests
{
    [Fact]
    public void PaymentEntryReference_Create_SetsFields()
    {
        var peId = Guid.NewGuid();
        var refId = Guid.NewGuid();
        var reference = new PaymentEntryReference(
            Guid.NewGuid(), peId, "SalesInvoice", refId,
            totalAmount: 10000m, outstandingAmount: 7000m,
            allocatedAmount: 5000m, referenceNumber: "SI-001");

        reference.PaymentEntryId.ShouldBe(peId);
        reference.ReferenceType.ShouldBe("SalesInvoice");
        reference.ReferenceId.ShouldBe(refId);
        reference.TotalAmount.ShouldBe(10000m);
        reference.OutstandingAmount.ShouldBe(7000m);
        reference.AllocatedAmount.ShouldBe(5000m);
        reference.ReferenceNumber.ShouldBe("SI-001");
    }

    [Fact]
    public void PaymentEntryReference_MultipleAllocations_Concept()
    {
        // One PE of RM 8000 split across 2 invoices
        var peId = Guid.NewGuid();
        var ref1 = new PaymentEntryReference(
            Guid.NewGuid(), peId, "SalesInvoice", Guid.NewGuid(),
            10000m, 10000m, 5000m, "SI-001");
        var ref2 = new PaymentEntryReference(
            Guid.NewGuid(), peId, "SalesInvoice", Guid.NewGuid(),
            6000m, 6000m, 3000m, "SI-002");

        var totalAllocated = ref1.AllocatedAmount + ref2.AllocatedAmount;
        totalAllocated.ShouldBe(8000m); // Total matches PE PaidAmount
    }

    [Fact]
    public void PaymentEntryReference_ExchangeRate_Default()
    {
        var reference = new PaymentEntryReference(
            Guid.NewGuid(), Guid.NewGuid(), "PurchaseInvoice", Guid.NewGuid(),
            5000m, 5000m, 5000m);
        reference.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void Item_IsActive_DefaultTrue()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        item.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Item_CanBeDeactivated()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        item.IsActive = false;
        item.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void ItemInactive_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.ItemInactive.ShouldBe("MyERP:05013");
    }

    [Fact]
    public void PaymentEntryReference_ForPurchaseOrder_Advance()
    {
        var reference = new PaymentEntryReference(
            Guid.NewGuid(), Guid.NewGuid(), "PurchaseOrder", Guid.NewGuid(),
            totalAmount: 20000m, outstandingAmount: 20000m,
            allocatedAmount: 5000m, referenceNumber: "PO-001");

        reference.ReferenceType.ShouldBe("PurchaseOrder");
        reference.AllocatedAmount.ShouldBe(5000m);
    }
}
