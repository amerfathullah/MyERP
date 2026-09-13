using System;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class SecondaryItemValuationTypeTests
{
    [Fact]
    public void SetBomlessSecondaryValuationTypes_Throws_WhenPercentageOfComponentCostWithoutBomSecondaryItem()
    {
        var entry = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.UtcNow);
        entry.AddItem(
            itemId: Guid.NewGuid(),
            quantity: 10,
            valuationRate: 50,
            secondaryItemType: "Co-Product",
            valuationType: SecondaryItemValuationType.PercentageOfComponentCost,
            bomSecondaryItemId: null);

        var ex = Should.Throw<BusinessException>(() => entry.SetBomlessSecondaryValuationTypes());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("% of Component Cost needs a BOM secondary item");
    }

    [Fact]
    public void SetBomlessSecondaryValuationTypes_Allows_PercentageOfComponentCost_WhenBomSecondaryItemPresent()
    {
        var entry = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.UtcNow);
        var bomSecondaryItemId = Guid.NewGuid();
        entry.AddItem(
            itemId: Guid.NewGuid(),
            quantity: 10,
            valuationRate: 50,
            secondaryItemType: "Co-Product",
            valuationType: SecondaryItemValuationType.PercentageOfComponentCost,
            bomSecondaryItemId: bomSecondaryItemId);

        // Should not throw
        entry.SetBomlessSecondaryValuationTypes();

        var item = entry.Items[0];
        item.ValuationType.ShouldBe(SecondaryItemValuationType.PercentageOfComponentCost);
        item.BomSecondaryItemId.ShouldBe(bomSecondaryItemId);
    }

    [Fact]
    public void SetBomlessSecondaryValuationTypes_Allows_ValuationRateOrManual_WhenBomSecondaryItemNull()
    {
        var entry = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.UtcNow);
        entry.AddItem(
            itemId: Guid.NewGuid(),
            quantity: 5,
            valuationRate: 20,
            secondaryItemType: "By-Product",
            valuationType: SecondaryItemValuationType.ValuationRate,
            bomSecondaryItemId: null);
        entry.AddItem(
            itemId: Guid.NewGuid(),
            quantity: 2,
            valuationRate: 15,
            secondaryItemType: "Scrap",
            valuationType: SecondaryItemValuationType.Manual,
            bomSecondaryItemId: null);

        entry.SetBomlessSecondaryValuationTypes();

        entry.Items[0].ValuationType.ShouldBe(SecondaryItemValuationType.ValuationRate);
        entry.Items[1].ValuationType.ShouldBe(SecondaryItemValuationType.Manual);
    }

    [Fact]
    public void SetBomlessSecondaryValuationTypes_ClearsValuationTypeAndManual_WhenSecondaryItemTypeBlank()
    {
        var entry = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.UtcNow);
        entry.AddItem(
            itemId: Guid.NewGuid(),
            quantity: 5,
            valuationRate: 20,
            secondaryItemType: null,
            valuationType: SecondaryItemValuationType.ValuationRate,
            bomSecondaryItemId: null);
        entry.Items[0].SetBasicRateManually = true;

        entry.SetBomlessSecondaryValuationTypes();

        entry.Items[0].ValuationType.ShouldBeNull();
        entry.Items[0].SetBasicRateManually.ShouldBeFalse();
    }

    [Fact]
    public void Submit_Calls_SetBomlessSecondaryValuationTypes_AndThrowsIfInvalid()
    {
        var entry = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.UtcNow);
        entry.AddItem(
            itemId: Guid.NewGuid(),
            quantity: 10,
            sourceWarehouseId: Guid.NewGuid(),
            targetWarehouseId: Guid.NewGuid(),
            valuationRate: 50,
            secondaryItemType: "Co-Product",
            valuationType: SecondaryItemValuationType.PercentageOfComponentCost,
            bomSecondaryItemId: null);

        var ex = Should.Throw<BusinessException>(() => entry.Submit());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("% of Component Cost needs a BOM secondary item");
    }
}
