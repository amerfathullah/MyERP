using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

public class CustomersWithoutAnySalesTransactionsAppServiceTests
{
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
    private readonly IRepository<Territory, Guid> _territoryRepo = Substitute.For<IRepository<Territory, Guid>>();
    private readonly IRepository<CustomerGroup, Guid> _customerGroupRepo = Substitute.For<IRepository<CustomerGroup, Guid>>();

    private readonly CustomersWithoutAnySalesTransactionsAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _otherCompanyId = Guid.NewGuid();
    private readonly Guid _custWithoutSalesId = Guid.NewGuid();
    private readonly Guid _custWithInvoiceId = Guid.NewGuid();
    private readonly Guid _custWithOrderId = Guid.NewGuid();
    private readonly Guid _custWithDeliveryId = Guid.NewGuid();
    private readonly Guid _custWithDraftOnlyId = Guid.NewGuid();
    private readonly Guid _territoryId = Guid.NewGuid();
    private readonly Guid _customerGroupId = Guid.NewGuid();

    public CustomersWithoutAnySalesTransactionsAppServiceTests()
    {
        _appService = new CustomersWithoutAnySalesTransactionsAppService(
            _customerRepo,
            _salesInvoiceRepo,
            _salesOrderRepo,
            _deliveryNoteRepo,
            _territoryRepo,
            _customerGroupRepo);
    }

    [Fact]
    public async Task ExcludesCustomersWithSubmittedTransactions_IncludesDraftOnlyAndNoTransactions()
    {
        var cust1 = new Customer(_custWithoutSalesId, _companyId, "Customer Zero")
        {
            TerritoryId = _territoryId,
            CustomerGroupId = _customerGroupId,
            CustomerCode = "CUST-ZERO"
        };
        var cust2 = new Customer(_custWithInvoiceId, _companyId, "Customer With Invoice");
        var cust3 = new Customer(_custWithOrderId, _companyId, "Customer With Order");
        var cust4 = new Customer(_custWithDeliveryId, _companyId, "Customer With Delivery");
        var cust5 = new Customer(_custWithDraftOnlyId, _companyId, "Customer Draft Only");

        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<Customer> { cust1, cust2, cust3, cust4, cust5 }.AsQueryable()));

        _territoryRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<Territory> { new Territory(_territoryId, "Kuala Lumpur") }.AsQueryable()));

        _customerGroupRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<CustomerGroup> { new CustomerGroup(_customerGroupId, "Retail") }.AsQueryable()));

        // Submitted invoice for cust2
        var inv = new SalesInvoice(Guid.NewGuid(), _companyId, _custWithInvoiceId, "INV-001", DateTime.UtcNow);
        inv.AddItem(Guid.NewGuid(), "Item 1", 1m, 100m, 0m);
        inv.Submit();

        // Draft invoice for cust5 (should not count as completed sale)
        var draftInv = new SalesInvoice(Guid.NewGuid(), _companyId, _custWithDraftOnlyId, "INV-DRAFT", DateTime.UtcNow);

        _salesInvoiceRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<SalesInvoice> { inv, draftInv }.AsQueryable()));

        // Submitted SO for cust3
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _custWithOrderId, "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item 2", 1m, 200m, 0m);
        so.Submit();

        _salesOrderRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<SalesOrder> { so }.AsQueryable()));

        // Submitted Delivery Note for cust4
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _custWithDeliveryId, Guid.NewGuid(), "DN-001", DateTime.UtcNow);
        dn.AddItem(Guid.NewGuid(), "Item 3", 1m, 50m, 0m);
        dn.Submit();

        _deliveryNoteRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<DeliveryNote> { dn }.AsQueryable()));

        var result = await _appService.GetReportAsync(new CustomersWithoutAnySalesTransactionsFilterDto
        {
            CompanyId = _companyId
        });

        result.TotalCustomers.ShouldBe(2);
        result.Items.Any(i => i.CustomerId == _custWithoutSalesId).ShouldBeTrue();
        result.Items.Any(i => i.CustomerId == _custWithDraftOnlyId).ShouldBeTrue();

        var zeroItem = result.Items.First(i => i.CustomerId == _custWithoutSalesId);
        zeroItem.CustomerName.ShouldBe("Customer Zero");
        zeroItem.CustomerCode.ShouldBe("CUST-ZERO");
        zeroItem.Territory.ShouldBe("Kuala Lumpur");
        zeroItem.CustomerGroup.ShouldBe("Retail");

        result.Items.Any(i => i.CustomerId == _custWithInvoiceId).ShouldBeFalse();
        result.Items.Any(i => i.CustomerId == _custWithOrderId).ShouldBeFalse();
        result.Items.Any(i => i.CustomerId == _custWithDeliveryId).ShouldBeFalse();
    }

    [Fact]
    public async Task CompanyFilter_IsolatesSalesTransactionsByCompany()
    {
        var cust = new Customer(_custWithoutSalesId, _companyId, "Customer Branch");
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { cust }.AsQueryable()));
        _territoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Territory>().AsQueryable()));
        _customerGroupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<CustomerGroup>().AsQueryable()));

        // Order exists but in OTHER company
        var otherCompanySo = new SalesOrder(Guid.NewGuid(), _otherCompanyId, _custWithoutSalesId, "SO-OTHER", DateTime.UtcNow);
        otherCompanySo.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        otherCompanySo.Submit();

        _salesInvoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice>().AsQueryable()));
        _salesOrderRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder> { otherCompanySo }.AsQueryable()));
        _deliveryNoteRepo.GetQueryableAsync().Returns(Task.FromResult(new List<DeliveryNote>().AsQueryable()));

        var result = await _appService.GetReportAsync(new CustomersWithoutAnySalesTransactionsFilterDto
        {
            CompanyId = _companyId
        });

        result.TotalCustomers.ShouldBe(1);
        result.Items[0].CustomerId.ShouldBe(_custWithoutSalesId);
    }
}
