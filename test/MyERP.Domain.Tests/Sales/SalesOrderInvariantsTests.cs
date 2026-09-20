using System;
using MyERP.Core;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Sales;

public class SalesOrderInvariantsTests
{
    private static SalesOrder CreateSalesOrder()
    {
        return new SalesOrder(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SO-2026-00001",
            DateTime.UtcNow.Date);
    }

    [Fact]
    public void SalesOrder_PerDelivered_CapsPerItemBeforeSumming()
    {
        var so = CreateSalesOrder();
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();

        so.AddItem(itemA, "Item A", 100m, 10m, 0m);
        so.AddItem(itemB, "Item B", 50m, 20m, 0m);

        so.Items[0].DeliveredQty = 100m; // 100% delivered
        so.Items[1].DeliveredQty = 25m;  // 50% delivered

        // Weighted formula: (min(100, 100) + min(25, 50)) / (100 + 50) = 125 / 150 = 83.33%
        so.PerDelivered.ShouldBe(83.33m);

        // Over-delivery on Item A should cap at 100
        so.Items[0].DeliveredQty = 150m;
        so.PerDelivered.ShouldBe(83.33m);

        // Fully deliver Item B
        so.Items[1].DeliveredQty = 50m;
        so.PerDelivered.ShouldBe(100m);
    }

    [Fact]
    public void SalesOrder_DeliveryDates_SyncsMaxItemDate()
    {
        var so = CreateSalesOrder();
        var today = DateTime.UtcNow.Date;
        so.OrderDate = today;

        so.AddItem(Guid.NewGuid(), "Item A", 10m, 100m, 0m, deliveryDate: today.AddDays(2));
        so.AddItem(Guid.NewGuid(), "Item B", 5m, 200m, 0m, deliveryDate: today.AddDays(7));

        so.Submit();

        so.DeliveryDate.ShouldBe(today.AddDays(7));
    }

    [Fact]
    public void SalesOrder_DeliveryDates_EarlierThanOrderDate_Throws()
    {
        var so = CreateSalesOrder();
        var today = DateTime.UtcNow.Date;
        so.OrderDate = today;

        so.AddItem(Guid.NewGuid(), "Item A", 10m, 100m, 0m, deliveryDate: today.AddDays(-1));

        var ex = Should.Throw<BusinessException>(() => so.Submit());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("cannot be earlier than order date");
    }

    [Fact]
    public void SalesOrder_DeliveryDates_GatedToSalesOrderType()
    {
        var so = CreateSalesOrder();
        var today = DateTime.UtcNow.Date;
        so.OrderDate = today;
        so.OrderType = SalesOrderType.Maintenance;

        // Even with earlier delivery date, Maintenance order bypasses delivery date validation
        so.AddItem(Guid.NewGuid(), "Service Item", 1m, 100m, 0m, deliveryDate: today.AddDays(-1));

        Should.NotThrow(() => so.Submit());
    }

    [Fact]
    public void Lead_RevertCustomer_SetsStatusToInterested()
    {
        var lead = new Lead(Guid.NewGuid(), Guid.NewGuid(), "LEAD-001", "Bob");
        var customerId = Guid.NewGuid();
        lead.ConvertToCustomer(customerId);

        lead.Status.ShouldBe(LeadStatus.Converted);
        lead.ConvertedCustomerId.ShouldBe(customerId);

        lead.RevertCustomer();

        lead.Status.ShouldBe(LeadStatus.Interested);
        lead.ConvertedCustomerId.ShouldBeNull();
    }
}
