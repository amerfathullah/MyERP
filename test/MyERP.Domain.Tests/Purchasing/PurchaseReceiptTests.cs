using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Purchasing;

public class PurchaseReceiptTests
{
    [Fact]
    public void Submit_WithItems_ShouldChangeStatus()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Steel Coil", 10, 500m, 0m);

        receipt.Submit();

        receipt.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutItems_ShouldThrow()
    {
        var receipt = CreateReceipt();

        Assert.Throws<BusinessException>(() => receipt.Submit());
    }

    [Fact]
    public void Cancel_AfterSubmit_ShouldChangeStatus()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Copper Wire", 100, 15m, 0m);
        receipt.Submit();

        receipt.Cancel();

        receipt.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromDraft_ShouldThrow()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Item", 1, 100m, 0m);

        Assert.Throws<BusinessException>(() => receipt.Cancel());
    }

    [Fact]
    public void AddItem_ShouldRecalculateTotals()
    {
        var receipt = CreateReceipt();

        receipt.AddItem(Guid.NewGuid(), "Part A", 5, 100m, 30m);   // 500 + 30
        receipt.AddItem(Guid.NewGuid(), "Part B", 20, 25m, 30m);   // 500 + 30

        receipt.NetTotal.ShouldBe(1000m);
        receipt.TaxAmount.ShouldBe(60m);
        receipt.GrandTotal.ShouldBe(1060m);
    }

    [Fact]
    public void AddItem_AfterSubmit_ShouldThrow()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Item", 1, 100m, 6m);
        receipt.Submit();

        Assert.Throws<BusinessException>(() =>
            receipt.AddItem(Guid.NewGuid(), "New Item", 1, 50m, 3m));
    }

    private static PurchaseReceipt CreateReceipt()
    {
        return new PurchaseReceipt(
            Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            supplierId: Guid.NewGuid(),
            warehouseId: Guid.NewGuid(),
            receiptNumber: "PR-2026-00001",
            postingDate: DateTime.Today);
    }
}
