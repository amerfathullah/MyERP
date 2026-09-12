using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Tax.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class SalesOrderBillableAllowanceTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    [Fact]
    public async Task GetUnbilledOrderItems_OrdersOldestFirst_FIFO()
    {
        // Per ERPNext PR #59010 / commit 5dfd21cce6:
        // List billable sales orders oldest first (OrderDate asc, CreationTime asc)
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();

        var order1 = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-NEWER", DateTime.UtcNow.AddDays(-1));
        order1.AddItem(Guid.NewGuid(), "Item Newer", 5m, 100m, 0m);
        order1.Submit();

        var order2 = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-OLDER", DateTime.UtcNow.AddDays(-10));
        order2.AddItem(Guid.NewGuid(), "Item Older", 5m, 100m, 0m);
        order2.Submit();

        var orders = new List<SalesOrder> { order1, order2 }.AsQueryable();
        soRepo.GetQueryableAsync().Returns(Task.FromResult(orders));

        var appService = new SalesInvoiceAppService(
            Substitute.For<IRepository<SalesInvoice, Guid>>(),
            Substitute.For<IRepository<Customer, Guid>>(),
            soRepo,
            Substitute.For<IRepository<Company, Guid>>(),
            Substitute.For<IRepository<TransactionTaxRow, Guid>>(),
            Substitute.For<IRepository<PaymentTermsTemplate, Guid>>(),
            Substitute.For<IRepository<PaymentScheduleEntry, Guid>>(),
            Substitute.For<IDocumentNumberGenerator>(),
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

        var result = await appService.GetUnbilledOrderItemsAsync(_customerId, _companyId);

        result.Count.ShouldBe(2);
        result[0].SalesOrderId.ShouldBe(order2.Id);
        result[0].OrderNumber.ShouldBe("SO-OLDER");
        result[1].SalesOrderId.ShouldBe(order1.Id);
        result[1].OrderNumber.ShouldBe("SO-NEWER");
    }

    [Fact]
    public async Task GetUnbilledOrderItems_ExcludesOrderWithZeroPendingBillingQty()
    {
        // Per ERPNext PR #58966 / commit 5f216c5d55:
        // Sales order where all items have PendingBillingQty <= 0 must not be offered
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();

        var order = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", DateTime.UtcNow);
        order.AddItem(Guid.NewGuid(), "Item 1", 10m, 50m, 0m);
        order.Submit();
        order.Items[0].BilledQty = 10m; // 100% billed
        order.PerBilled.ShouldBe(100m);

        var orders = new List<SalesOrder> { order }.AsQueryable();
        soRepo.GetQueryableAsync().Returns(Task.FromResult(orders));

        var appService = new SalesInvoiceAppService(
            Substitute.For<IRepository<SalesInvoice, Guid>>(),
            Substitute.For<IRepository<Customer, Guid>>(),
            soRepo,
            Substitute.For<IRepository<Company, Guid>>(),
            Substitute.For<IRepository<TransactionTaxRow, Guid>>(),
            Substitute.For<IRepository<PaymentTermsTemplate, Guid>>(),
            Substitute.For<IRepository<PaymentScheduleEntry, Guid>>(),
            Substitute.For<IDocumentNumberGenerator>(),
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

        var result = await appService.GetUnbilledOrderItemsAsync(_customerId, _companyId);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConvertSalesOrderToSalesInvoice_FullyBilledOrder_ThrowsDocumentAlreadyConverted()
    {
        // Per ERPNext PR #58966: cannot convert fully billed SO to SI
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();

        var order = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", DateTime.UtcNow);
        order.AddItem(Guid.NewGuid(), "Item 1", 10m, 50m, 0m);
        order.Submit();
        order.Items[0].BilledQty = 10m;
        order.PerBilled.ShouldBe(100m);

        soRepo.GetAsync(order.Id).Returns(Task.FromResult(order));

        var activityLog = new DocumentActivityLogService(
            Substitute.For<IRepository<DocumentActivityLog, Guid>>(),
            Substitute.For<Volo.Abp.Users.ICurrentUser>());

        var appService = new DocumentConversionAppService(
            Substitute.For<IRepository<Quotation, Guid>>(),
            soRepo,
            Substitute.For<IRepository<DeliveryNote, Guid>>(),
            siRepo,
            Substitute.For<IRepository<Customer, Guid>>(),
            Substitute.For<IDocumentNumberGenerator>(),
            activityLog);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await appService.ConvertSalesOrderToSalesInvoiceAsync(order.Id));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.DocumentAlreadyConverted);
        (ex.Data["reason"]?.ToString() ?? string.Empty).ShouldContain("already fully billed");
    }

    [Fact]
    public async Task GetUnbilledOrderItems_IncludesUnbilledZeroAmountRows()
    {
        // Per ERPNext PR #58816 / commit c613a7f7f0:
        // When an order has both priced items and zero-rate items (e.g. free promotional samples),
        // billing only the priced item must not mark the order fully billed or skip unbilled free items.
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();

        var order = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-FREE", DateTime.UtcNow);
        order.AddItem(Guid.NewGuid(), "Paid Item", 10m, 100m, 0m); // NetTotal = 1000
        order.AddItem(Guid.NewGuid(), "Free Sample", 5m, 0m, 0m);   // NetTotal = 0
        order.Submit();

        // Bill the paid item in full
        order.Items[0].BilledQty = 10m;
        order.Items[1].BilledQty = 0m; // Free sample unbilled

        // PerBilled must not report 100% while an unbilled row has pending billing qty
        order.PerBilled.ShouldBe(99.99m);
        order.UpdateFulfillmentStatus();
        order.Status.ShouldNotBe(DocumentStatus.Completed);

        var orders = new List<SalesOrder> { order }.AsQueryable();
        soRepo.GetQueryableAsync().Returns(Task.FromResult(orders));

        var appService = new SalesInvoiceAppService(
            Substitute.For<IRepository<SalesInvoice, Guid>>(),
            Substitute.For<IRepository<Customer, Guid>>(),
            soRepo,
            Substitute.For<IRepository<Company, Guid>>(),
            Substitute.For<IRepository<TransactionTaxRow, Guid>>(),
            Substitute.For<IRepository<PaymentTermsTemplate, Guid>>(),
            Substitute.For<IRepository<PaymentScheduleEntry, Guid>>(),
            Substitute.For<IDocumentNumberGenerator>(),
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

        var result = await appService.GetUnbilledOrderItemsAsync(_customerId, _companyId);

        // Result must include the unbilled free item
        result.Count.ShouldBe(1);
        result[0].ItemName.ShouldBe("Free Sample");
        result[0].Quantity.ShouldBe(5m);
        result[0].Rate.ShouldBe(0m);

        // Once the free sample is also billed
        order.Items[1].BilledQty = 5m;
        order.PerBilled.ShouldBe(100m);
        order.Items.All(i => i.PendingBillingQty <= 0).ShouldBeTrue();

        var resultAfter = await appService.GetUnbilledOrderItemsAsync(_customerId, _companyId);
        resultAfter.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConvertSalesOrderToSalesInvoice_AllowsInvoicingZeroAmountRow()
    {
        // Per ERPNext PR #58816 / commit c613a7f7f0:
        // Conversion must succeed when a zero-amount row still has pending billing qty
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();

        var order = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-FREE-CONVERT", DateTime.UtcNow);
        order.AddItem(Guid.NewGuid(), "Paid Item", 10m, 100m, 0m);
        order.AddItem(Guid.NewGuid(), "Free Sample", 5m, 0m, 0m);
        order.Submit();

        order.Items[0].BilledQty = 10m; // Paid item fully billed
        order.Items[1].BilledQty = 0m;  // Free item unbilled

        soRepo.GetAsync(order.Id).Returns(Task.FromResult(order));
        var emptySiList = new List<SalesInvoice>().AsQueryable();
        siRepo.GetQueryableAsync().Returns(Task.FromResult(emptySiList));

        var numGen = Substitute.For<IDocumentNumberGenerator>();
        numGen.GenerateAsync("SalesInvoice", _companyId).Returns(Task.FromResult("SI-001"));

        var activityLog = new DocumentActivityLogService(
            Substitute.For<IRepository<DocumentActivityLog, Guid>>(),
            Substitute.For<Volo.Abp.Users.ICurrentUser>());

        var appService = new DocumentConversionAppService(
            Substitute.For<IRepository<Quotation, Guid>>(),
            soRepo,
            Substitute.For<IRepository<DeliveryNote, Guid>>(),
            siRepo,
            Substitute.For<IRepository<Customer, Guid>>(),
            numGen,
            activityLog);

        var lazyProvider = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        var guidGen = Substitute.For<Volo.Abp.Guids.IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());
        lazyProvider.LazyGetService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetRequiredService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);
        lazyProvider.LazyGetRequiredService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);

        var mapper = Substitute.For<Volo.Abp.ObjectMapping.IObjectMapper>();
        mapper.Map<SalesInvoice, SalesInvoiceDto>(Arg.Any<SalesInvoice>()).Returns(call =>
        {
            var inv = call.Arg<SalesInvoice>();
            return new SalesInvoiceDto
            {
                Id = inv.Id,
                InvoiceNumber = inv.InvoiceNumber,
                Items = inv.Items.Select(i => new SalesInvoiceItemDto
                {
                    ItemId = i.ItemId,
                    Description = i.Description,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };
        });
        lazyProvider.LazyGetService<Volo.Abp.ObjectMapping.IObjectMapper>(Arg.Any<Func<IServiceProvider, object>>()).Returns(mapper);
        lazyProvider.LazyGetService<Volo.Abp.ObjectMapping.IObjectMapper>().Returns(mapper);
        lazyProvider.LazyGetRequiredService<Volo.Abp.ObjectMapping.IObjectMapper>().Returns(mapper);
        lazyProvider.LazyGetService(typeof(Volo.Abp.ObjectMapping.IObjectMapper)).Returns(mapper);
        lazyProvider.LazyGetRequiredService(typeof(Volo.Abp.ObjectMapping.IObjectMapper)).Returns(mapper);

        appService.LazyServiceProvider = lazyProvider;
        activityLog.LazyServiceProvider = lazyProvider;

        var dto = await appService.ConvertSalesOrderToSalesInvoiceAsync(order.Id);

        dto.ShouldNotBeNull();
        dto.Items.Count.ShouldBe(1);
        dto.Items[0].Description.ShouldBe("Free Sample");
        dto.Items[0].Quantity.ShouldBe(5m);
        dto.Items[0].UnitPrice.ShouldBe(0m);
    }

    [Fact]
    public void LandedCostVoucher_ZeroValuedItems_BasedOnAmount_FallsBackToQuantity()
    {
        // Per ERPNext PR #58841 / #58842:
        // When incoming items have basic amount = 0, BasedOnAmount falls back to distributing by Quantity.
        var lcv = new MyERP.Inventory.Entities.LandedCostVoucher(Guid.NewGuid(), _companyId, DateTime.UtcNow)
        {
            DistributionMethod = MyERP.Inventory.LandedCostDistributionMethod.BasedOnAmount
        };

        var receiptId = Guid.NewGuid();
        // Item 1: 20 qty, 0 amount
        lcv.AddItem(receiptId, "PurchaseReceipt", Guid.NewGuid(), 20m, 0m, "Zero Rate Item 1");
        // Item 2: 30 qty, 0 amount
        lcv.AddItem(receiptId, "PurchaseReceipt", Guid.NewGuid(), 30m, 0m, "Zero Rate Item 2");

        lcv.AddCharge("Freight", Guid.NewGuid(), 100m);

        lcv.DistributeCharges();

        // Item 1: 20/50 * 100 = 40
        lcv.Items[0].ApplicableCharges.ShouldBe(40m);
        // Item 2: 30/50 * 100 = 60
        lcv.Items[1].ApplicableCharges.ShouldBe(60m);
        lcv.TotalDistributedAmount.ShouldBe(100m);
    }

    [Fact]
    public void PurchaseOrder_ZeroAmountRow_PerBilledCapsAndTracksPendingBilling()
    {
        // Per ERPNext PR #58816:
        // PO with priced item and zero-rate item caps PerBilled at 99.99 when free item is unbilled.
        var supplierId = Guid.NewGuid();
        var po = new MyERP.Purchasing.Entities.PurchaseOrder(Guid.NewGuid(), _companyId, supplierId, "PO-FREE", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Paid Raw Material", 10m, 50m, 0m);
        po.AddItem(Guid.NewGuid(), "Free Vendor Sample", 5m, 0m, 0m);
        po.Submit();

        // Bill only the paid item
        po.Items[0].BilledQty = 10m;
        po.Items[1].BilledQty = 0m;

        po.PerBilled.ShouldBe(99.99m);
        po.UpdateFulfillmentStatus();
        po.Status.ShouldNotBe(DocumentStatus.Completed);
        po.Items.All(i => i.PendingBillingQty <= 0).ShouldBeFalse();

        // Bill the vendor sample
        po.Items[1].BilledQty = 5m;
        po.PerBilled.ShouldBe(100m);
        po.Items.All(i => i.PendingBillingQty <= 0).ShouldBeTrue();
    }
}

