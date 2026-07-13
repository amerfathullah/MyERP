using System;
using MyERP.Core;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Tests.Inventory;

public class StockReconciliationPostingTests
{
    private static StockReconciliation CreateSR()
    {
        return new StockReconciliation(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
    }

    [Fact]
    public void Item_QuantityDifference_Positive()
    {
        var sr = CreateSR();
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(),
            newQuantity: 100, newValuationRate: 10,
            currentQuantity: 80, currentValuationRate: 10);

        sr.Items[0].QuantityDifference.ShouldBe(20m);
    }

    [Fact]
    public void Item_QuantityDifference_Negative()
    {
        var sr = CreateSR();
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(),
            newQuantity: 50, newValuationRate: 10,
            currentQuantity: 80, currentValuationRate: 10);

        sr.Items[0].QuantityDifference.ShouldBe(-30m);
    }

    [Fact]
    public void Item_DifferenceAmount_QtyChange()
    {
        var sr = CreateSR();
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(),
            newQuantity: 100, newValuationRate: 10,
            currentQuantity: 80, currentValuationRate: 10);

        // (100 × 10) - (80 × 10) = 1000 - 800 = 200
        sr.Items[0].DifferenceAmount.ShouldBe(200m);
    }

    [Fact]
    public void Item_DifferenceAmount_RateChange()
    {
        var sr = CreateSR();
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(),
            newQuantity: 100, newValuationRate: 12,
            currentQuantity: 100, currentValuationRate: 10);

        // (100 × 12) - (100 × 10) = 1200 - 1000 = 200
        sr.Items[0].DifferenceAmount.ShouldBe(200m);
    }

    [Fact]
    public void DifferenceAmount_SumsAcrossItems()
    {
        var sr = CreateSR();
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(),
            newQuantity: 100, newValuationRate: 10,
            currentQuantity: 80, currentValuationRate: 10); // +200

        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(),
            newQuantity: 50, newValuationRate: 20,
            currentQuantity: 60, currentValuationRate: 20); // -200

        sr.DifferenceAmount.ShouldBe(0m); // Net zero
    }

    [Fact]
    public void Submit_ChangesStatus()
    {
        var sr = CreateSR();
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(), 100, 10, 80, 10);
        sr.Submit();
        sr.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Cancel_AfterSubmit()
    {
        var sr = CreateSR();
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(), 100, 10, 80, 10);
        sr.Submit();
        sr.Cancel();
        sr.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void Submit_FromCancelled_Throws()
    {
        var sr = CreateSR();
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(), 100, 10, 80, 10);
        sr.Submit();
        sr.Cancel();
        Should.Throw<BusinessException>(() => sr.Submit());
    }

    [Fact]
    public void Cancel_FromDraft_Throws()
    {
        var sr = CreateSR();
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(), 100, 10, 80, 10);
        Should.Throw<BusinessException>(() => sr.Cancel());
    }
}
