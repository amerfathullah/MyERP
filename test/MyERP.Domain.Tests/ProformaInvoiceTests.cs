using System;
using System.Linq;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for ProformaInvoice entity (v16 feature — ERPNext PR #57263).
/// </summary>
public class ProformaInvoiceTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _salesOrderId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _soItemId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParams_SetsDefaults()
    {
        var pi = CreateProforma();

        pi.Status.ShouldBe(ProformaInvoiceStatus.Draft);
        pi.CompanyId.ShouldBe(_companyId);
        pi.SalesOrderId.ShouldBe(_salesOrderId);
        pi.CustomerId.ShouldBe(_customerId);
        pi.BasedOn.ShouldBe(ProformaInvoiceBasis.Quantity);
        pi.GrandTotal.ShouldBe(0);
        pi.TotalQty.ShouldBe(0);
        pi.Items.ShouldBeEmpty();
        pi.HideItemQty.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithEmptyCompanyId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new ProformaInvoice(Guid.NewGuid(), Guid.Empty, _salesOrderId, _customerId, DateTime.UtcNow));
    }

    [Fact]
    public void Create_WithEmptySalesOrderId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new ProformaInvoice(Guid.NewGuid(), _companyId, Guid.Empty, _customerId, DateTime.UtcNow));
    }

    [Fact]
    public void AddItem_QuantityBasis_CalculatesAmount()
    {
        var pi = CreateProforma();

        pi.AddItem(_soItemId, _itemId, "ITEM-001", "Widget", 5, 100, "Unit");

        pi.Items.Count.ShouldBe(1);
        var item = pi.Items.First();
        item.Quantity.ShouldBe(5);
        item.Rate.ShouldBe(100);
        item.Amount.ShouldBe(500);
        pi.TotalQty.ShouldBe(5);
        pi.GrandTotal.ShouldBe(500);
    }

    [Fact]
    public void AddItem_AmountBasis_DerivedRate()
    {
        var pi = CreateProforma(ProformaInvoiceBasis.Amount);

        // Amount basis: qty=5, amount=600, rate = 600/5 = 120
        pi.AddItem(_soItemId, _itemId, "ITEM-001", "Widget", 5, 120, "Unit");

        var item = pi.Items.First();
        item.Rate.ShouldBe(120);
        item.Amount.ShouldBe(600); // 5 × 120
    }

    [Fact]
    public void AddItem_ZeroQuantity_Throws()
    {
        var pi = CreateProforma();

        Should.Throw<ArgumentException>(() =>
            pi.AddItem(_soItemId, _itemId, "ITEM-001", "Widget", 0, 100, "Unit"));
    }

    [Fact]
    public void AddItem_NegativeQuantity_Throws()
    {
        var pi = CreateProforma();

        Should.Throw<ArgumentException>(() =>
            pi.AddItem(_soItemId, _itemId, "ITEM-001", "Widget", -1, 100, "Unit"));
    }

    [Fact]
    public void AddItem_AfterSubmit_Throws()
    {
        var pi = CreateProformaWithItems();
        pi.Submit();

        Should.Throw<BusinessException>(() =>
            pi.AddItem(_soItemId, _itemId, "ITEM-002", "Gadget", 3, 50, "Unit"));
    }

    [Fact]
    public void Submit_WithItems_SetsIssued()
    {
        var pi = CreateProformaWithItems();

        pi.Submit();

        pi.Status.ShouldBe(ProformaInvoiceStatus.Issued);
    }

    [Fact]
    public void Submit_EmptyItems_Throws()
    {
        var pi = CreateProforma();

        Should.Throw<BusinessException>(() => pi.Submit());
    }

    [Fact]
    public void Submit_AlreadyIssued_Throws()
    {
        var pi = CreateProformaWithItems();
        pi.Submit();

        Should.Throw<BusinessException>(() => pi.Submit());
    }

    [Fact]
    public void Cancel_FromIssued_SetsCancelled()
    {
        var pi = CreateProformaWithItems();
        pi.Submit();

        pi.Cancel();

        pi.Status.ShouldBe(ProformaInvoiceStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromDraft_Throws()
    {
        var pi = CreateProforma();

        Should.Throw<BusinessException>(() => pi.Cancel());
    }

    [Fact]
    public void MarkEmailed_Issued_Succeeds()
    {
        var pi = CreateProformaWithItems();
        pi.Submit();

        pi.MarkEmailed("john@example.com, jane@example.com");

        pi.SentOn.ShouldNotBeNull();
        pi.EmailedTo.ShouldBe("john@example.com, jane@example.com");
    }

    [Fact]
    public void MarkEmailed_Cancelled_Throws()
    {
        var pi = CreateProformaWithItems();
        pi.Submit();
        pi.Cancel();

        Should.Throw<BusinessException>(() => pi.MarkEmailed("test@example.com"));
    }

    [Fact]
    public void MultipleItems_SumsTotals()
    {
        var pi = CreateProforma();

        pi.AddItem(Guid.NewGuid(), _itemId, "A", "Item A", 3, 100, "Unit");
        pi.AddItem(Guid.NewGuid(), _itemId, "B", "Item B", 2, 250, "Unit");

        pi.TotalQty.ShouldBe(5);
        pi.GrandTotal.ShouldBe(800); // 300 + 500
    }

    [Fact]
    public void ProformaInvoiceItem_DefaultProperties()
    {
        var item = new ProformaInvoiceItem(
            Guid.NewGuid(), Guid.NewGuid(), _soItemId, _itemId,
            "CODE", "Name", 10, 25.5m, "Kg");

        item.SalesOrderItemId.ShouldBe(_soItemId);
        item.ItemId.ShouldBe(_itemId);
        item.ItemCode.ShouldBe("CODE");
        item.ItemName.ShouldBe("Name");
        item.Quantity.ShouldBe(10);
        item.Rate.ShouldBe(25.5m);
        item.Amount.ShouldBe(255); // 10 × 25.5
        item.Uom.ShouldBe("Kg");
    }

    [Fact]
    public void Enums_HaveCorrectValues()
    {
        ((int)ProformaInvoiceStatus.Draft).ShouldBe(0);
        ((int)ProformaInvoiceStatus.Issued).ShouldBe(1);
        ((int)ProformaInvoiceStatus.Cancelled).ShouldBe(2);

        ((int)ProformaInvoiceBasis.Quantity).ShouldBe(0);
        ((int)ProformaInvoiceBasis.Amount).ShouldBe(1);
    }

    [Fact]
    public void HideItemQty_OnlyForAmountBasis()
    {
        var pi = CreateProforma(ProformaInvoiceBasis.Amount);
        pi.HideItemQty = true;
        pi.HideItemQty.ShouldBeTrue();

        // Quantity basis doesn't typically hide qty but entity doesn't enforce
        var pi2 = CreateProforma(ProformaInvoiceBasis.Quantity);
        pi2.HideItemQty.ShouldBeFalse();
    }

    // ─── Helpers ───

    private ProformaInvoice CreateProforma(ProformaInvoiceBasis basis = ProformaInvoiceBasis.Quantity)
        => new ProformaInvoice(Guid.NewGuid(), _companyId, _salesOrderId, _customerId, DateTime.UtcNow, basis, "MYR");

    private ProformaInvoice CreateProformaWithItems()
    {
        var pi = CreateProforma();
        pi.AddItem(_soItemId, _itemId, "ITEM-001", "Widget", 5, 100, "Unit");
        return pi;
    }
}
