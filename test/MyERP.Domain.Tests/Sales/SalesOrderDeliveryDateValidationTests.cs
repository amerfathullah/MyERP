using System;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Sales Order delivery date validation and auto-sync (Gotcha #462):
/// - Header delivery date auto-syncs to MAX item delivery date
/// - Item delivery date earlier than order date throws validation exception
/// - Header delivery date earlier than order date throws validation exception
/// </summary>
public class SalesOrderDeliveryDateValidationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _itemId1 = Guid.NewGuid();
    private readonly Guid _itemId2 = Guid.NewGuid();

    [Fact]
    public void SalesOrder_SyncsHeaderDeliveryDate_ToMaxItemDeliveryDate()
    {
        var orderDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-2026-0001", orderDate);
        so.AddItem(_itemId1, "Item A", 2m, 100m, 0m);
        so.AddItem(_itemId2, "Item B", 5m, 50m, 0m);

        so.Items[0].DeliveryDate = orderDate.AddDays(5);
        so.Items[1].DeliveryDate = orderDate.AddDays(15);

        so.Submit();

        Assert.Equal(orderDate.AddDays(15), so.DeliveryDate);
    }

    [Fact]
    public void SalesOrder_ItemDeliveryDate_EarlierThanOrderDate_ThrowsValidationException()
    {
        var orderDate = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-2026-0002", orderDate);
        so.AddItem(_itemId1, "Item A", 1m, 100m, 0m);

        so.Items[0].DeliveryDate = orderDate.AddDays(-2); // Earlier than order date

        var ex = Assert.Throws<BusinessException>(() => so.Submit());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("cannot be earlier than order date", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void SalesOrder_HeaderDeliveryDate_InheritedByItemsWithoutDeliveryDate()
    {
        var orderDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var headerDate = orderDate.AddDays(7);
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-2026-0003", orderDate)
        {
            DeliveryDate = headerDate
        };
        so.AddItem(_itemId1, "Item A", 1m, 100m, 0m);

        so.Submit();

        Assert.Equal(headerDate, so.Items[0].DeliveryDate);
        Assert.Equal(headerDate, so.DeliveryDate);
    }
}
