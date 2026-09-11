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
}
