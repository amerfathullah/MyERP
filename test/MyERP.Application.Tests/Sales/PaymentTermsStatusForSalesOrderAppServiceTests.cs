using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

public class PaymentTermsStatusForSalesOrderAppServiceTests
{
    private readonly IRepository<SalesOrder, Guid> _soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<PaymentTermsTemplate, Guid> _paymentTermsRepo = Substitute.For<IRepository<PaymentTermsTemplate, Guid>>();

    private readonly PaymentTermsStatusForSalesOrderAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public PaymentTermsStatusForSalesOrderAppServiceTests()
    {
        _appService = new PaymentTermsStatusForSalesOrderAppService(
            _soRepo,
            _customerRepo,
            _siRepo,
            _paymentTermsRepo);
    }

    [Fact]
    public async Task GetReportAsync_WithNoInvoices_ReturnsUnpaidOrOverdueTerms()
    {
        var custId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        var template = new PaymentTermsTemplate(templateId, "50/50");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), templateId, 50m, 0, "Due on Order"));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), templateId, 50m, 30, "Net 30"));

        var customer = new Customer(custId, _companyId, "ACME Corp")
        {
            DefaultPaymentTermsTemplateId = templateId
        };

        var soId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, custId, "SO-001", DateTime.UtcNow.AddDays(-10));
        so.AddItem(Guid.NewGuid(), "Widget", 10m, 100m, 0m); // GrandTotal = 1000m
        so.Submit();

        _soRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        _customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>())
            .Returns(Task.FromResult(new List<Customer> { customer }));

        _paymentTermsRepo.WithDetailsAsync(Arg.Any<Expression<Func<PaymentTermsTemplate, object>>[]>())
            .Returns(Task.FromResult(new List<PaymentTermsTemplate> { template }.AsQueryable()));

        _siRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice>().AsQueryable()));

        var result = await _appService.GetReportAsync(new PaymentTermsStatusForSalesOrderRequestDto
        {
            CompanyId = _companyId
        });

        result.TotalSalesOrders.ShouldBe(1);
        result.TotalPaymentTerms.ShouldBe(2);
        result.TotalPaymentAmount.ShouldBe(1000m);
        result.TotalPaidAmount.ShouldBe(0m);
        result.TotalOutstandingAmount.ShouldBe(1000m);

        // Term 1: order date - 10 days + 0 days = 10 days ago -> Overdue
        var row1 = result.Rows[0];
        row1.PaymentAmount.ShouldBe(500m);
        row1.PaidAmount.ShouldBe(0m);
        row1.Status.ShouldBe("Overdue");

        // Term 2: order date - 10 days + 30 days = 20 days in future -> Unpaid
        var row2 = result.Rows[1];
        row2.PaymentAmount.ShouldBe(500m);
        row2.PaidAmount.ShouldBe(0m);
        row2.Status.ShouldBe("Unpaid");
    }

    [Fact]
    public async Task GetReportAsync_AllocatesInvoicesFifoAcrossTerms()
    {
        var custId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        var template = new PaymentTermsTemplate(templateId, "50/50");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), templateId, 50m, 0, "Term 1"));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), templateId, 50m, 30, "Term 2"));

        var customer = new Customer(custId, _companyId, "Beta Ltd")
        {
            DefaultPaymentTermsTemplateId = templateId
        };

        var soId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, custId, "SO-BETA", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Part A", 10m, 100m, 0m); // 1000m
        so.Submit();
        var soItemId = so.Items[0].Id;

        // Invoice bills 600m
        var invId = Guid.NewGuid();
        var inv = new SalesInvoice(invId, _companyId, custId, "INV-2026-001", DateTime.UtcNow);
        inv.AddItem(Guid.NewGuid(), "Part A", 6m, 100m, 0m); // 600m
        inv.Items[0].SalesOrderItemId = soItemId;
        inv.Submit();

        _soRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        _customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>())
            .Returns(Task.FromResult(new List<Customer> { customer }));

        _paymentTermsRepo.WithDetailsAsync(Arg.Any<Expression<Func<PaymentTermsTemplate, object>>[]>())
            .Returns(Task.FromResult(new List<PaymentTermsTemplate> { template }.AsQueryable()));

        _siRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice> { inv }.AsQueryable()));

        var result = await _appService.GetReportAsync(new PaymentTermsStatusForSalesOrderRequestDto
        {
            CompanyId = _companyId
        });

        result.Rows.Count.ShouldBe(2);

        // Term 1 (500m): completely covered by 600m invoice
        var row1 = result.Rows[0];
        row1.PaymentAmount.ShouldBe(500m);
        row1.PaidAmount.ShouldBe(500m);
        row1.Status.ShouldBe("Completed");
        row1.Invoices.ShouldContain("INV-2026-001");

        // Term 2 (500m): remaining 100m applied -> Partly Paid
        var row2 = result.Rows[1];
        row2.PaymentAmount.ShouldBe(500m);
        row2.PaidAmount.ShouldBe(100m);
        row2.Status.ShouldBe("Partly Paid");
        row2.Invoices.ShouldContain("INV-2026-001");

        result.CompletedTermsCount.ShouldBe(1);
        result.TotalPaidAmount.ShouldBe(600m);
        result.TotalOutstandingAmount.ShouldBe(400m);
    }

    [Fact]
    public async Task GetReportAsync_MultiOrderInvoice_ProportionallyAllocatesGrandTotal()
    {
        var custId = Guid.NewGuid();
        var customer = new Customer(custId, _companyId, "Gamma Inc"); // No template -> single 100% term

        var so1Id = Guid.NewGuid();
        var so1 = new SalesOrder(so1Id, _companyId, custId, "SO-1", DateTime.UtcNow);
        so1.AddItem(Guid.NewGuid(), "Item 1", 4m, 100m, 0m); // 400m
        so1.Submit();
        var so1ItemId = so1.Items[0].Id;

        var so2Id = Guid.NewGuid();
        var so2 = new SalesOrder(so2Id, _companyId, custId, "SO-2", DateTime.UtcNow);
        so2.AddItem(Guid.NewGuid(), "Item 2", 6m, 100m, 0m); // 600m
        so2.Submit();
        var so2ItemId = so2.Items[0].Id;

        // Invoice billing both: grand total = 1000m
        var invId = Guid.NewGuid();
        var inv = new SalesInvoice(invId, _companyId, custId, "INV-COMBINED", DateTime.UtcNow);
        inv.AddItem(Guid.NewGuid(), "Item 1", 4m, 100m, 0m); // 400m (40%)
        inv.Items[0].SalesOrderItemId = so1ItemId;
        inv.AddItem(Guid.NewGuid(), "Item 2", 6m, 100m, 0m); // 600m (60%)
        inv.Items[1].SalesOrderItemId = so2ItemId;
        inv.Submit();

        _soRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { so1, so2 }.AsQueryable()));

        _customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>())
            .Returns(Task.FromResult(new List<Customer> { customer }));

        _paymentTermsRepo.WithDetailsAsync(Arg.Any<Expression<Func<PaymentTermsTemplate, object>>[]>())
            .Returns(Task.FromResult(new List<PaymentTermsTemplate>().AsQueryable()));

        _siRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice> { inv }.AsQueryable()));

        var result = await _appService.GetReportAsync(new PaymentTermsStatusForSalesOrderRequestDto
        {
            CompanyId = _companyId
        });

        result.Rows.Count.ShouldBe(2);

        var r1 = result.Rows.First(r => r.SalesOrderId == so1Id);
        r1.PaidAmount.ShouldBe(400m);
        r1.Status.ShouldBe("Completed");

        var r2 = result.Rows.First(r => r.SalesOrderId == so2Id);
        r2.PaidAmount.ShouldBe(600m);
        r2.Status.ShouldBe("Completed");
    }
}
