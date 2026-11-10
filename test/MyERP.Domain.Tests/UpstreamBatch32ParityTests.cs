using System;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP;

/// <summary>
/// Unit tests for Batch 32 upstream parity:
/// 1) Gross profit resolution in company base currency and accounting for UOM conversion factor
///    (PR #63122 / commits 338314ba35, fe8b401332, 766d130f6c).
/// 2) SalesInvoiceItem GrossProfit formula parity with ERPNext selling_controller:
///    flt((flt(stock_uom_rate) * conversion_rate - flt(valuation_rate)) * stock_qty).
/// </summary>
public class UpstreamBatch32ParityTests
{
    // === 1) PR #63122: Gross Profit in Base Currency ===

    [Fact]
    public void GrossProfitService_CalculateForInvoice_UsesCompanyCurrency()
    {
        var service = new GrossProfitService(null!);

        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        // Foreign currency invoice: USD with exchange rate 4.0 to MYR
        var invoice = new SalesInvoice(
            Guid.NewGuid(), companyId, customerId, "SINV-USD-001", DateTime.UtcNow)
        {
            CurrencyCode = "USD",
            ExchangeRate = 4.0m
        };

        // Item: 2 units at 100 USD each = 200 USD = 800 MYR base revenue
        invoice.AddItem(itemId, "Imported Widget", 2m, 100m, 0m);
        // ValuationRate is always stored in company base currency (MYR) = 250 MYR
        invoice.Items[0].ValuationRate = 250m;

        var result = service.CalculateForInvoice(invoice);

        // Base Revenue: 2 * 100 * 4 = 800 MYR
        result.TotalRevenue.ShouldBe(800m);
        // Base Cost: 2 * 250 = 500 MYR
        result.TotalCost.ShouldBe(500m);
        // Gross Profit: 800 - 500 = 300 MYR
        result.GrossProfit.ShouldBe(300m);
        // Margin: (300 / 800) * 100 = 37.5%
        result.GrossProfitPercentage.ShouldBe(37.5m);

        result.ItemDetails.Count.ShouldBe(1);
        result.ItemDetails[0].SellingRate.ShouldBe(400m); // 100 USD * 4.0
        result.ItemDetails[0].GrossProfit.ShouldBe(300m);
        result.ItemDetails[0].GrossProfitPercentage.ShouldBe(37.5m);
    }

    // === 2) PR #63122 / commit fe8b401332 parity: UOM conversion factor ===

    [Theory]
    // Nos (factor 1): qty 2, rate 8300, val 5200 -> profit = (8300 - 5200) * 2 = 6200
    [InlineData("Nos", 1.0, 2.0, 8300.0, 5200.0, 6200.0)]
    // Box of 5 (factor 5): qty 2 boxes (10 Nos), box rate 41500 (8300/unit), val 5200 -> profit = 31000 = 6200 * 5
    [InlineData("Box", 5.0, 2.0, 41500.0, 5200.0, 31000.0)]
    public void SalesInvoiceItem_GrossProfit_AccountsForConversionFactorAndStockQty(
        string uom, decimal conversionFactor, decimal quantity, decimal unitPrice, decimal valuationRate, decimal expectedGrossProfit)
    {
        var item = new SalesInvoiceItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Item", quantity, unitPrice, 0m, uom)
        {
            ConversionFactor = conversionFactor,
            ValuationRate = valuationRate
        };

        item.StockQty.ShouldBe(quantity * conversionFactor);
        item.GrossProfit.ShouldBe(expectedGrossProfit);
    }
}
