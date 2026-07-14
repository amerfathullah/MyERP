using System;
using System.Collections.Generic;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Tests for selling price validation.
/// Per ERPNext validate_selling_price: selling rate must be >= buying/valuation rate.
/// Action "Stop" = hard error, "Warn" = soft warning.
/// </summary>
public class SellingPriceValidationTests
{
    [Fact]
    public void ValidateSellingPrice_AboveCost_Passes()
    {
        var items = CreateItems(unitPrice: 150);
        var result = SalesInvoiceManager.ValidateSellingPrice(
            items, _ => 100m, action: "Stop");

        result.HasWarnings.ShouldBeFalse();
    }

    [Fact]
    public void ValidateSellingPrice_BelowCost_StopMode_Throws()
    {
        var items = CreateItems(unitPrice: 80);

        Should.Throw<BusinessException>(() =>
            SalesInvoiceManager.ValidateSellingPrice(items, _ => 100m, action: "Stop"))
            .Code.ShouldBe(MyERPDomainErrorCodes.SellingPriceBelowCost);
    }

    [Fact]
    public void ValidateSellingPrice_BelowCost_WarnMode_ReturnsWarning()
    {
        var items = CreateItems(unitPrice: 80);
        var result = SalesInvoiceManager.ValidateSellingPrice(
            items, _ => 100m, action: "Warn");

        result.HasWarnings.ShouldBeTrue();
        result.Warnings.Count.ShouldBe(1);
        result.Warnings[0].ShouldContain("80");
        result.Warnings[0].ShouldContain("100");
    }

    [Fact]
    public void ValidateSellingPrice_EqualToCost_Passes()
    {
        var items = CreateItems(unitPrice: 100);
        var result = SalesInvoiceManager.ValidateSellingPrice(
            items, _ => 100m, action: "Stop");

        result.HasWarnings.ShouldBeFalse();
    }

    [Fact]
    public void ValidateSellingPrice_ZeroCost_Skipped()
    {
        // No valuation data → skip validation (don't block sale)
        var items = CreateItems(unitPrice: 50);
        var result = SalesInvoiceManager.ValidateSellingPrice(
            items, _ => 0m, action: "Stop");

        result.HasWarnings.ShouldBeFalse();
    }

    [Fact]
    public void ValidateSellingPrice_MultipleItems_FirstBelowCost_Throws()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Cheap Item", 1, 50, 0);
        si.AddItem(Guid.NewGuid(), "Normal Item", 1, 200, 0);

        var rates = new Dictionary<Guid, decimal>();
        foreach (var item in si.Items)
            rates[item.ItemId] = 100m; // both have 100 valuation rate

        Should.Throw<BusinessException>(() =>
            SalesInvoiceManager.ValidateSellingPrice(
                si.Items, id => rates.GetValueOrDefault(id, 0m), action: "Stop"))
            .Code.ShouldBe(MyERPDomainErrorCodes.SellingPriceBelowCost);
    }

    [Fact]
    public void ValidateSellingPrice_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.SellingPriceBelowCost.ShouldBe("MyERP:03015");
    }

    private static IReadOnlyList<SalesInvoiceItem> CreateItems(decimal unitPrice)
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-TEST", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Test Item", 1, unitPrice, 0);
        return si.Items;
    }
}
