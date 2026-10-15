using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Unit tests for SalesInvoice.UpdateBilledAmountInSalesOrder per ERPNext PR #59947 / commit c706da2899.
/// Allows credit notes (sales invoice returns) to skip updating billed amount in sales orders.
/// </summary>
public class SalesInvoiceReturnSoBillingTests
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _orderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();

    private readonly SalesInvoiceManager _manager;

    public SalesInvoiceReturnSoBillingTests()
    {
        _manager = new SalesInvoiceManager(_invoiceRepo, _orderRepo, _itemRepo);
    }

    [Fact]
    public void SalesInvoice_UpdateBilledAmountInSalesOrder_DefaultsToTrue()
    {
        var invoice = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-001", DateTime.Today);
        invoice.UpdateBilledAmountInSalesOrder.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateLinkedOrderBillingAsync_WhenReturnWithFlagFalse_SkipsOrderUpdate()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var order = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-001", DateTime.Today);
        order.AddItem(itemId, "Widget", 10m, 100m, 0m);
        var soItem = order.Items.Last();
        soItem.BilledQty = 10m;

        _orderRepo.GetQueryableAsync()
            .Returns(new List<SalesOrder> { order }.AsQueryable());

        // Return invoice with UpdateBilledAmountInSalesOrder = false
        var returnInvoice = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "RET-001", DateTime.Today)
        {
            IsReturn = true,
            UpdateBilledAmountInSalesOrder = false
        };
        returnInvoice.AddItem(itemId, "Widget", -2m, 100m, 0m);
        var returnItem = returnInvoice.Items.Last();
        returnItem.SalesOrderItemId = soItem.Id;

        // Act
        await _manager.UpdateLinkedOrderBillingAsync(returnInvoice, reverse: false);

        // Assert
        soItem.BilledQty.ShouldBe(10m); // unchanged
        await _orderRepo.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task UpdateLinkedOrderBillingAsync_WhenReturnWithFlagTrue_UpdatesOrderBilledQty()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var order = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-002", DateTime.Today);
        order.AddItem(itemId, "Widget", 10m, 100m, 0m);
        var soItem = order.Items.Last();
        soItem.BilledQty = 10m;

        _orderRepo.GetQueryableAsync()
            .Returns(new List<SalesOrder> { order }.AsQueryable());

        // Return invoice with UpdateBilledAmountInSalesOrder = true
        var returnInvoice = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "RET-002", DateTime.Today)
        {
            IsReturn = true,
            UpdateBilledAmountInSalesOrder = true
        };
        returnInvoice.AddItem(itemId, "Widget", -2m, 100m, 0m);
        var returnItem = returnInvoice.Items.Last();
        returnItem.SalesOrderItemId = soItem.Id;

        // Act
        await _manager.UpdateLinkedOrderBillingAsync(returnInvoice, reverse: false);

        // Assert
        // In return invoice, siItem.Quantity is -2m.
        // Formula: soItem.BilledQty += siItem.Quantity => 10 + (-2) = 8
        soItem.BilledQty.ShouldBe(8m);
        await _orderRepo.Received(1).UpdateAsync(order, autoSave: true);
    }
}
