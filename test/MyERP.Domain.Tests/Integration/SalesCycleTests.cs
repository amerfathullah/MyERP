using System;
using System.Collections.Generic;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using MyERP.Tax.DomainServices;
using MyERP.Tax.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Integration tests validating the complete sales cycle:
/// Create Items → Create SO → Calculate Taxes → Convert to SI → Verify Totals
/// </summary>
public class SalesCycleTests
{
    private readonly TaxesAndTotalsService _taxService = new();

    [Fact]
    public void FullSalesCycle_QuotationToInvoice_WithSSTCalculation()
    {
        // 1. Create items
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // 2. Create Sales Order with 2 items
        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-2026-0001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Laptop Computer", 2, 3500m, 0, "Unit");
        so.AddItem(Guid.NewGuid(), "USB Cable", 5, 25m, 0, "Unit");

        // Verify SO totals
        so.NetTotal.ShouldBe(7125m); // 7000 + 125
        so.Status.ShouldBe(DocumentStatus.Draft);

        // 3. Calculate SST 8% via TaxesAndTotalsService
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 2, Rate = 3500, NetAmount = 7000 },
            new() { ItemId = Guid.NewGuid(), Qty = 5, Rate = 25, NetAmount = 125 },
        };
        var taxRows = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST 8%", "On Net Total", 8),
        };

        var totals = _taxService.Calculate(items, taxRows);

        totals.NetTotal.ShouldBe(7125m);
        totals.TotalTax.ShouldBe(570m); // 8% of 7125
        totals.GrandTotal.ShouldBe(7695m);

        // 4. Create Sales Invoice from SO data
        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-2026-0001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Laptop Computer", 2, 3500m, 280m); // tax = 3500*2*0.08/7125*570 = 560*570/7125 ≈ 280
        si.AddItem(Guid.NewGuid(), "USB Cable", 5, 25m, 10m);

        // 5. Submit SO
        so.Submit();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);

        // 6. Submit SI
        si.Submit();
        si.Status.ShouldBe(DocumentStatus.Submitted);

        // 7. Verify outstanding (LineTotal includes taxAmount in RecalculateTotals)
        // Items: (2*3500)=7000 + (5*25)=125 = 7125 net, + 280+10=290 tax = 7415
        si.GrandTotal.ShouldBe(7415m);
        si.OutstandingAmount.ShouldBe(si.GrandTotal); // nothing paid yet
        si.AmountPaid.ShouldBe(0);
    }

    [Fact]
    public void SalesOrder_FulfillmentTracking_WorksEndToEnd()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-0002", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Widget A", 100, 10, 0);
        so.AddItem(Guid.NewGuid(), "Widget B", 50, 20, 0);

        // Initially: 0% delivered, 0% billed
        so.PerDelivered.ShouldBe(0);
        so.PerBilled.ShouldBe(0);

        // Simulate partial delivery: 60 of A, 50 of B
        so.Items[0].DeliveredQty = 60;
        so.Items[1].DeliveredQty = 50;
        so.PerDelivered.ShouldBe(60m); // Min(60/100=60%, 50/50=100%) = 60%

        // Simulate billing: all of Widget A
        so.Items[0].BilledQty = 100;
        // Billed value = 100*10 = 1000, GrandTotal = 100*10 + 50*20 = 2000
        so.PerBilled.ShouldBe(50m);

        // Full billing
        so.Items[1].BilledQty = 50;
        so.PerBilled.ShouldBe(100m);
    }

    [Fact]
    public void TaxCalculation_WithDiscountOnNetTotal_RecalculatesTax()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 10000, NetAmount = 10000 },
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SI", Guid.NewGuid(), 1, "SST 6%", "On Net Total", 6),
        };

        // Without discount
        var before = _taxService.Calculate(items, taxes);
        before.GrandTotal.ShouldBe(10600m);

        // With RM500 discount on net total — tax recalculated on discounted amount
        items[0].NetAmount = 10000; // reset
        var after = _taxService.Calculate(items, taxes, discountAmount: 500, applyDiscountOn: "Net Total");
        after.NetTotal.ShouldBe(9500m);
        after.TotalTax.ShouldBe(570m); // 6% of 9500
        after.GrandTotal.ShouldBe(10070m);
    }

    [Fact]
    public void MultiCurrency_InvoiceWithExchangeRate()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-USD-001", DateTime.UtcNow);
        si.CurrencyCode = "USD";
        si.ExchangeRate = 4.5m; // 1 USD = 4.5 MYR

        si.AddItem(Guid.NewGuid(), "Consulting Service", 10, 200, 0, "Hour"); // USD 2000

        si.NetTotal.ShouldBe(2000m);
        si.BaseNetTotal.ShouldBe(9000m); // 2000 * 4.5
        si.BaseGrandTotal.ShouldBe(9000m);
    }

    [Fact]
    public void CreditNote_IsReturnWithNegativeAmount()
    {
        var originalSI = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.UtcNow);
        originalSI.AddItem(Guid.NewGuid(), "Product", 5, 100, 0);
        originalSI.Submit();

        // Create credit note
        var cn = new SalesInvoice(Guid.NewGuid(), originalSI.CompanyId, originalSI.CustomerId, "CN-001", DateTime.UtcNow);
        cn.IsReturn = true;
        cn.ReturnAgainstId = originalSI.Id;
        cn.AddItem(Guid.NewGuid(), "Product (Return)", 2, 100, 0);

        cn.IsReturn.ShouldBeTrue();
        cn.ReturnAgainstId.ShouldBe(originalSI.Id);
        cn.GrandTotal.ShouldBe(200m); // 2 items returned
    }
}
