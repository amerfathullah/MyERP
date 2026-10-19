using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Unit tests for UOM conversion in Sales Order and Delivery Note progress/billing per
/// ERPNext PR #60570 (commit b4746585a6 / 257d827f5b) and PR #60569 (commit 57ede92f65 / aaf0a339a3 / d860f5def5).
/// </summary>
public class SalesUomConversionFulfillmentTests
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _orderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<DeliveryNote, Guid> _dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
    private readonly IRepository<Core.Entities.Company, Guid> _companyRepo = Substitute.For<IRepository<Core.Entities.Company, Guid>>();

    private readonly SalesInvoiceManager _invoiceManager;
    private readonly DeliveryNoteManager _dnManager;

    public SalesUomConversionFulfillmentTests()
    {
        _invoiceManager = new SalesInvoiceManager(_invoiceRepo, _orderRepo, _itemRepo);
        _dnManager = new DeliveryNoteManager(_dnRepo, _orderRepo, _companyRepo, _itemRepo);
    }

    [Fact]
    public async Task UpdateLinkedOrderBillingAsync_WhenCreditNoteInDifferentUom_ConvertsToOrderUom()
    {
        // Arrange: SO in Box (factor = 12), Qty = 2 Box, BilledQty = 2 Box
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var order = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-UOM-001", DateTime.Today);
        order.AddItem(itemId, "Widget", 2m, 120m, 0m, uom: "Box");
        var soItem = order.Items.Last();
        soItem.ConversionFactor = 12m;
        soItem.BilledQty = 2m;

        _orderRepo.GetQueryableAsync()
            .Returns(new List<SalesOrder> { order }.AsQueryable());

        // Credit note for -12 Nos (factor = 1) -> should reduce BilledQty by 1 Box
        var creditNote = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "CN-001", DateTime.Today)
        {
            IsReturn = true,
            UpdateBilledAmountInSalesOrder = true
        };
        creditNote.AddItem(itemId, "Widget", -12m, 10m, 0m, uom: "Nos");
        var cnItem = creditNote.Items.Last();
        cnItem.SalesOrderItemId = soItem.Id;
        cnItem.ConversionFactor = 1m;

        // Act
        await _invoiceManager.UpdateLinkedOrderBillingAsync(creditNote, reverse: false);

        // Assert: 2 - (12 * 1 / 12) = 1 Box
        soItem.BilledQty.ShouldBe(1m);
    }

    [Fact]
    public async Task UpdateLinkedOrderStockDeliveryAsync_WhenUpdateStockInvoiceAndCreditNote_UpdatesDeliveredAndReturnedQty()
    {
        // Arrange: matches ERPNext test_delivery_progress_after_returns_in_stock_uom
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var order = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-UOM-002", DateTime.Today);
        order.AddItem(itemId, "Widget", 2m, 120m, 0m, uom: "Box");
        var soItem = order.Items.Last();
        soItem.ConversionFactor = 12m;
        soItem.DeliveredQty = 0.8333m; // 1 Box delivered previously - 2 Nos returned previously (10/12 Box)
        soItem.ReturnedQty = 0.1667m;  // 2 Nos returned previously (2/12 Box)

        _orderRepo.GetQueryableAsync()
            .Returns(new List<SalesOrder> { order }.AsQueryable());

        // Step 1: Submit SI with UpdateStock = true for 1 Box
        var invoice = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "INV-001", DateTime.Today)
        {
            UpdateStock = true
        };
        invoice.AddItem(itemId, "Widget", 1m, 120m, 0m, uom: "Box");
        var siItem = invoice.Items.Last();
        siItem.SalesOrderItemId = soItem.Id;
        siItem.ConversionFactor = 12m;

        await _invoiceManager.UpdateLinkedOrderStockDeliveryAsync(invoice, reverse: false);
        Math.Round(soItem.DeliveredQty, 4).ShouldBe(1.8333m);

        // Step 2: Submit Credit Note with UpdateStock = true for -4 Nos (factor = 1)
        var creditNote = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "CN-002", DateTime.Today)
        {
            IsReturn = true,
            UpdateStock = true
        };
        creditNote.AddItem(itemId, "Widget", -4m, 10m, 0m, uom: "Nos");
        var cnItem = creditNote.Items.Last();
        cnItem.SalesOrderItemId = soItem.Id;
        cnItem.ConversionFactor = 1m;

        await _invoiceManager.UpdateLinkedOrderStockDeliveryAsync(creditNote, reverse: false);

        // Assert:
        // Returned: 2/12 + 4/12 = 6/12 = 0.5 Box
        // Delivered: (1 - 2/12) + 1 - 4/12 = 2 - 6/12 = 1.5 Box
        Math.Round(soItem.DeliveredQty, 1).ShouldBe(1.5m);
        Math.Round(soItem.ReturnedQty, 1).ShouldBe(0.5m);
    }

    [Fact]
    public async Task UpdateLinkedDeliveryNoteBillingAsync_WhenCreditNoteInDifferentUom_ReducesBilledQtyCorrectly()
    {
        // Arrange: matches ERPNext test_make_sales_invoice_after_return_and_credit_note_in_stock_uom
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var dn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-UOM-001", DateTime.Today);
        dn.AddItem(itemId, "Widget", 2m, 40m, 0m, uom: "Box");
        var dnItem = dn.Items.Last();
        dnItem.ConversionFactor = 4m;
        dnItem.ReturnedQty = 1m; // 4 Nos returned in original DN = 1 Box
        dnItem.BilledQty = 1m;   // 1 Box billed in first SI

        // At this point:
        // dnItem.BillableQty = 2 - 1 = 1 Box
        // dnItem.PendingBillingQty = 1 - 1 = 0 Box

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IRepository<DeliveryNote, Guid>)).Returns(_dnRepo);
        var abpLazy = new Volo.Abp.DependencyInjection.AbpLazyServiceProvider(serviceProvider);
        _invoiceManager.LazyServiceProvider = abpLazy;

        _dnRepo.GetQueryableAsync()
            .Returns(new List<DeliveryNote> { dn }.AsQueryable());

        // Credit Note for -1 Nos (factor = 1) against the DN row
        var creditNote = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "CN-DN-001", DateTime.Today)
        {
            IsReturn = true,
            UpdateStock = false
        };
        creditNote.AddItem(itemId, "Widget", -1m, 10m, 0m, uom: "Nos");
        var cnItem = creditNote.Items.Last();
        cnItem.DeliveryNoteItemId = dnItem.Id;
        cnItem.ConversionFactor = 1m;

        // Act
        await _invoiceManager.UpdateLinkedDeliveryNoteBillingAsync(creditNote, reverse: false);

        // Assert:
        // BilledQty reduced by: (-1 * 1) / 4 = -0.25 => BilledQty becomes 0.75 Box
        dnItem.BilledQty.ShouldBe(0.75m);
        // BillableQty remains 1 Box, PendingBillingQty becomes: 1 - 0.75 = 0.25 Box!
        dnItem.PendingBillingQty.ShouldBe(0.25m);
    }

    [Fact]
    public async Task ValidateOverBillingAsync_WhenInvoiceInDifferentUom_ConvertsBeforeValidation()
    {
        // Arrange: SO ordered 1 Box (factor = 12), allowance = 0%
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var order = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-UOM-003", DateTime.Today);
        order.AddItem(itemId, "Widget", 1m, 120m, 0m, uom: "Box");
        var soItem = order.Items.Last();
        soItem.ConversionFactor = 12m;
        soItem.BilledQty = 0.5m; // 6 Nos already billed

        _orderRepo.GetQueryableAsync()
            .Returns(new List<SalesOrder> { order }.AsQueryable());

        // SI trying to bill 7 Nos (factor = 1):
        // 0.5 + 7/12 = 0.5 + 0.5833 = 1.0833 Box > 1 Box => OverBilling
        var invoice = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "INV-UOM-003", DateTime.Today);
        invoice.AddItem(itemId, "Widget", 7m, 10m, 0m, uom: "Nos");
        var siItem = invoice.Items.Last();
        siItem.SalesOrderItemId = soItem.Id;
        siItem.ConversionFactor = 1m;

        // Act & Assert
        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            _invoiceManager.ValidateOverBillingAsync(invoice, 0m));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.OverBilling);
    }

    [Fact]
    public async Task ValidateAgainstSalesOrderAsync_WhenDnInDifferentUom_ConvertsBeforeValidation()
    {
        // Arrange: SO ordered 1 Box (factor = 12), allowance = 0%
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var order = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-UOM-004", DateTime.Today);
        order.AddItem(itemId, "Widget", 1m, 120m, 0m, uom: "Box");
        order.Submit();
        var soItem = order.Items.Last();
        soItem.ConversionFactor = 12m;
        soItem.DeliveredQty = 0.5m; // 6 Nos delivered

        _orderRepo.GetAsync(order.Id).Returns(order);
        _companyRepo.GetAsync(companyId).Returns(new Core.Entities.Company(companyId, "Test Co"));

        // DN delivering 6 Nos (factor = 1):
        // 6 * 1 / 12 = 0.5 Box. Remaining allowed = 1 - 0.5 = 0.5 Box => should succeed!
        var dn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-004", DateTime.Today)
        {
            SalesOrderId = order.Id
        };
        dn.AddItem(itemId, "Widget", 6m, 10m, 0m, uom: "Nos", salesOrderItemId: soItem.Id);
        var dnItem = dn.Items.Last();
        dnItem.ConversionFactor = 1m;

        // Act: should not throw
        await _dnManager.ValidateAgainstSalesOrderAsync(dn);

        // Now test 7 Nos (7 * 1 / 12 = 0.5833 Box > 0.5 Box remaining) => OverDelivery
        var dnOver = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-005", DateTime.Today)
        {
            SalesOrderId = order.Id
        };
        dnOver.AddItem(itemId, "Widget", 7m, 10m, 0m, uom: "Nos", salesOrderItemId: soItem.Id);
        dnOver.Items.Last().ConversionFactor = 1m;

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            _dnManager.ValidateAgainstSalesOrderAsync(dnOver));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.OverDelivery);
    }
}
