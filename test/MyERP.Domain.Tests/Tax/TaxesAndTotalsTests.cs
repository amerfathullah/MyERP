using System;
using System.Collections.Generic;
using MyERP.Tax.DomainServices;
using MyERP.Tax.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tax;

public class TaxesAndTotalsTests
{
    private readonly TaxesAndTotalsService _service = new();

    [Fact]
    public void Calculate_SingleItem_SingleTax_OnNetTotal()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 2, Rate = 500, NetAmount = 1000 },
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST 8%", "On Net Total", 8),
        };

        var result = _service.Calculate(items, taxes);

        result.NetTotal.ShouldBe(1000m);
        result.TotalTax.ShouldBe(80m);
        result.GrandTotal.ShouldBe(1080m);
    }

    [Fact]
    public void Calculate_MultipleItems_ProportionalTax()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 600, NetAmount = 600 },
            new() { ItemId = Guid.NewGuid(), Qty = 3, Rate = 200, NetAmount = 600 },
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST 6%", "On Net Total", 6),
        };

        var result = _service.Calculate(items, taxes);

        result.NetTotal.ShouldBe(1200m);
        result.TotalTax.ShouldBe(72m);
        result.GrandTotal.ShouldBe(1272m);
    }

    [Fact]
    public void Calculate_MultipleTaxRows_Cascade()
    {
        var parentId = Guid.NewGuid();
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 1000, NetAmount = 1000 },
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SI", parentId, 1, "SST 10%", "On Net Total", 10),
            new(Guid.NewGuid(), "SI", parentId, 2, "Cess 1% on SST", "On Previous Row Amount", 1) { ReferenceRowIndex = 1 },
        };

        var result = _service.Calculate(items, taxes);

        result.NetTotal.ShouldBe(1000m);
        result.TotalTax.ShouldBe(101m); // 100 + 1
        result.GrandTotal.ShouldBe(1101m);
    }

    [Fact]
    public void Calculate_ActualTax_FixedAmount()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 700, NetAmount = 700 },
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 300, NetAmount = 300 },
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SI", Guid.NewGuid(), 1, "Shipping", "Actual", 50),
        };

        var result = _service.Calculate(items, taxes);

        result.NetTotal.ShouldBe(1000m);
        result.TotalTax.ShouldBe(50m);
        result.GrandTotal.ShouldBe(1050m);
    }

    [Fact]
    public void Calculate_OnItemQuantity_PerUnitTax()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 5, Rate = 100, NetAmount = 500 },
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SI", Guid.NewGuid(), 1, "Excise RM2/unit", "On Item Quantity", 2),
        };

        var result = _service.Calculate(items, taxes);

        result.NetTotal.ShouldBe(500m);
        result.TotalTax.ShouldBe(10m);
        result.GrandTotal.ShouldBe(510m);
    }

    [Fact]
    public void Calculate_WithDiscountOnNetTotal()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 1000, NetAmount = 1000 },
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SI", Guid.NewGuid(), 1, "SST 8%", "On Net Total", 8),
        };

        var result = _service.Calculate(items, taxes, discountAmount: 100, applyDiscountOn: "Net Total");

        result.NetTotal.ShouldBe(900m); // 1000 - 100
        result.TotalTax.ShouldBe(72m); // 8% of 900
        result.GrandTotal.ShouldBe(972m);
    }

    [Fact]
    public void Calculate_WithDiscountOnGrandTotal()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 1000, NetAmount = 1000 },
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SI", Guid.NewGuid(), 1, "SST 8%", "On Net Total", 8),
        };

        var result = _service.Calculate(items, taxes, discountAmount: 50, applyDiscountOn: "Grand Total");

        result.NetTotal.ShouldBe(1000m);
        result.TotalTax.ShouldBe(80m);
        result.GrandTotal.ShouldBe(1030m); // 1080 - 50
    }

    [Fact]
    public void Calculate_MultiCurrency_BaseAmounts()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 100, NetAmount = 100 },
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SI", Guid.NewGuid(), 1, "SST 8%", "On Net Total", 8),
        };

        var result = _service.Calculate(items, taxes, exchangeRate: 4.5m);

        result.NetTotal.ShouldBe(100m);
        result.BaseNetTotal.ShouldBe(450m);
        result.TotalTax.ShouldBe(8m);
        result.BaseTotalTax.ShouldBe(36m);
        result.GrandTotal.ShouldBe(108m);
        result.BaseGrandTotal.ShouldBe(486m);
    }

    [Fact]
    public void Calculate_ValuationOnlyTax_ExcludedFromGrandTotal()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 1000, NetAmount = 1000 },
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "PI", Guid.NewGuid(), 1, "Customs Duty 5%", "On Net Total", 5) { TaxCategory = "Valuation" },
            new(Guid.NewGuid(), "PI", Guid.NewGuid(), 2, "SST 8%", "On Net Total", 8) { TaxCategory = "Total" },
        };

        var result = _service.Calculate(items, taxes);

        result.NetTotal.ShouldBe(1000m);
        // Valuation tax (50) should NOT be in grand total
        result.TotalTax.ShouldBe(80m); // only SST
        result.GrandTotal.ShouldBe(1080m);
    }

    [Fact]
    public void Calculate_ZeroItems_ZeroTotals()
    {
        var items = new List<TransactionItem>();
        var taxes = new List<TransactionTaxRow>();

        var result = _service.Calculate(items, taxes);

        result.NetTotal.ShouldBe(0m);
        result.TotalTax.ShouldBe(0m);
        result.GrandTotal.ShouldBe(0m);
    }
}
