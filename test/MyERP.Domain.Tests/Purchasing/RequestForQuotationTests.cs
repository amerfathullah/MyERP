using System;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Purchasing;

public class RequestForQuotationTests
{
    [Fact]
    public void Create_SetsDefaultStatus()
    {
        var rfq = CreateRfq();
        rfq.Status.ShouldBe(Core.DocumentStatus.Draft);
    }

    [Fact]
    public void AddItem_InDraft_Succeeds()
    {
        var rfq = CreateRfq();
        rfq.AddItem(Guid.NewGuid(), "Widget", 10, "Unit");
        rfq.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void AddSupplier_InDraft_Succeeds()
    {
        var rfq = CreateRfq();
        rfq.AddSupplier(Guid.NewGuid(), "Supplier A", "a@example.com");
        rfq.Suppliers.Count.ShouldBe(1);
        rfq.Suppliers[0].QuoteStatus.ShouldBe("Pending");
        rfq.Suppliers[0].EmailSent.ShouldBeFalse();
    }

    [Fact]
    public void AddSupplier_Duplicate_Throws()
    {
        var rfq = CreateRfq();
        var supplierId = Guid.NewGuid();
        rfq.AddSupplier(supplierId, "Supplier A");

        Should.Throw<BusinessException>(() =>
            rfq.AddSupplier(supplierId, "Supplier A"));
    }

    [Fact]
    public void Submit_WithItemsAndSuppliers_Succeeds()
    {
        var rfq = CreateRfq();
        rfq.AddItem(Guid.NewGuid(), "Widget", 10, "Unit");
        rfq.AddSupplier(Guid.NewGuid(), "Supplier A");

        rfq.Submit();
        rfq.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutItems_Throws()
    {
        var rfq = CreateRfq();
        rfq.AddSupplier(Guid.NewGuid(), "Supplier A");

        Should.Throw<BusinessException>(() => rfq.Submit());
    }

    [Fact]
    public void Submit_WithoutSuppliers_Throws()
    {
        var rfq = CreateRfq();
        rfq.AddItem(Guid.NewGuid(), "Widget", 10, "Unit");

        Should.Throw<BusinessException>(() => rfq.Submit());
    }

    [Fact]
    public void Cancel_FromSubmitted_Succeeds()
    {
        var rfq = CreateRfq();
        rfq.AddItem(Guid.NewGuid(), "Widget", 10, "Unit");
        rfq.AddSupplier(Guid.NewGuid(), "Supplier A");
        rfq.Submit();

        rfq.Cancel();
        rfq.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void AddItem_AfterSubmit_Throws()
    {
        var rfq = CreateRfq();
        rfq.AddItem(Guid.NewGuid(), "Widget", 10, "Unit");
        rfq.AddSupplier(Guid.NewGuid(), "Supplier A");
        rfq.Submit();

        Should.Throw<BusinessException>(() =>
            rfq.AddItem(Guid.NewGuid(), "Another", 5, "Unit"));
    }

    [Fact]
    public void MarkQuoteReceived_ChangesStatus()
    {
        var supplier = new RfqSupplier(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test");
        supplier.QuoteStatus.ShouldBe("Pending");

        supplier.MarkQuoteReceived();
        supplier.QuoteStatus.ShouldBe("Received");
    }

    [Fact]
    public void AddItem_NegativeQty_Throws()
    {
        var rfq = CreateRfq();
        Should.Throw<ArgumentException>(() =>
            rfq.AddItem(Guid.NewGuid(), "Widget", -5, "Unit"));
    }

    private static RequestForQuotation CreateRfq()
    {
        return new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.UtcNow);
    }
}
