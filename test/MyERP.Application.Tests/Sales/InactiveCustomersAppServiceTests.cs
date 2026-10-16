using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

public class InactiveCustomersAppServiceTests
{
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<Territory, Guid> _territoryRepo = Substitute.For<IRepository<Territory, Guid>>();
    private readonly IRepository<CustomerGroup, Guid> _customerGroupRepo = Substitute.For<IRepository<CustomerGroup, Guid>>();

    private readonly InactiveCustomersAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customer1Id = Guid.NewGuid();
    private readonly Guid _customer2Id = Guid.NewGuid();
    private readonly Guid _territoryId = Guid.NewGuid();
    private readonly Guid _customerGroupId = Guid.NewGuid();

    public InactiveCustomersAppServiceTests()
    {
        _appService = new InactiveCustomersAppService(
            _customerRepo,
            _salesOrderRepo,
            _salesInvoiceRepo,
            _territoryRepo,
            _customerGroupRepo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Validation_DaysSinceLastOrder_ZeroOrNegative_Throws(int days)
    {
        var input = new InactiveCustomersFilterDto
        {
            DaysSinceLastOrder = days
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.GetReportAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Validation_InvalidDocType_Throws()
    {
        var input = new InactiveCustomersFilterDto
        {
            DaysSinceLastOrder = 30,
            DocType = "Purchase Order"
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.GetReportAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SalesOrder_CalculatesRecencyAndConsidersClosedDeliveryProrata()
    {
        var asOfDate = new DateTime(2025, 4, 1);

        var cust1 = new Customer(_customer1Id, _companyId, "Customer Alpha")
        {
            TerritoryId = _territoryId,
            CustomerGroupId = _customerGroupId,
            CustomerCode = "CUST-001"
        };
        var cust2 = new Customer(_customer2Id, _companyId, "Customer Beta");

        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { cust1, cust2 }.AsQueryable()));
        _territoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Territory> { new Territory(_territoryId, "North Region") }.AsQueryable()));
        _customerGroupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<CustomerGroup> { new CustomerGroup(_customerGroupId, "Commercial") }.AsQueryable()));

        var itemId = Guid.NewGuid();

        // Cust 1:
        // Order 1: 90 days before asOfDate (2025-01-01), NetTotal 1000, Closed with 50% delivered
        var so1 = new SalesOrder(Guid.NewGuid(), _companyId, _customer1Id, "SO-001", asOfDate.AddDays(-90));
        so1.AddItem(itemId, "Item 1", 10m, 100m, 0m); // NetTotal = 1000
        so1.Submit();
        so1.Items[0].DeliveredQty = 5m;
        so1.Close();

        // Order 2: 70 days before asOfDate (2025-01-21), NetTotal 2000, Submitted
        var so2 = new SalesOrder(Guid.NewGuid(), _companyId, _customer1Id, "SO-002", asOfDate.AddDays(-70));
        so2.AddItem(itemId, "Item 2", 20m, 100m, 0m); // NetTotal = 2000
        so2.Submit();

        // Cust 2:
        // Order 3: 10 days before asOfDate (2025-03-22), NetTotal 500, Submitted (Active customer)
        var so3 = new SalesOrder(Guid.NewGuid(), _companyId, _customer2Id, "SO-003", asOfDate.AddDays(-10));
        so3.AddItem(itemId, "Item 3", 5m, 100m, 0m);
        so3.Submit();

        _salesOrderRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder> { so1, so2, so3 }.AsQueryable()));

        var result = await _appService.GetReportAsync(new InactiveCustomersFilterDto
        {
            DocType = "Sales Order",
            DaysSinceLastOrder = 60,
            AsOfDate = asOfDate
        });

        result.Items.Count.ShouldBe(1);
        var row = result.Items[0];
        row.CustomerId.ShouldBe(_customer1Id);
        row.CustomerName.ShouldBe("Customer Alpha");
        row.Territory.ShouldBe("North Region");
        row.CustomerGroup.ShouldBe("Commercial");
        row.NumOfOrder.ShouldBe(2);
        row.TotalOrderValue.ShouldBe(3000m);
        // so1 considered: 1000 * 50% = 500; so2 considered: 2000 => 2500
        row.TotalOrderConsidered.ShouldBe(2500m);
        row.LastOrderAmount.ShouldBe(2000m);
        row.LastOrderDate.ShouldBe(asOfDate.AddDays(-70));
        row.DaysSinceLastOrder.ShouldBe(70);

        result.TotalCustomers.ShouldBe(1);
        result.TotalOrders.ShouldBe(2);
        result.TotalOrderValue.ShouldBe(3000m);
        result.TotalOrderConsidered.ShouldBe(2500m);
    }

    [Fact]
    public async Task SalesInvoice_ExcludesCreditNotesFromRecencyAndCount()
    {
        var asOfDate = new DateTime(2025, 4, 1);

        var cust1 = new Customer(_customer1Id, _companyId, "Customer Alpha");
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { cust1 }.AsQueryable()));
        _territoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Territory>().AsQueryable()));
        _customerGroupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<CustomerGroup>().AsQueryable()));

        // Cust 1:
        // Invoice 1: 80 days ago (2025-01-11), BaseNetTotal 1500, Posted, not return
        var inv1 = new SalesInvoice(Guid.NewGuid(), _companyId, _customer1Id, "INV-001", asOfDate.AddDays(-80))
        {
            BaseNetTotal = 1500m
        };
        inv1.AddItem(Guid.NewGuid(), "Item 1", 1m, 1500m, 0m);
        inv1.Submit();
        inv1.Post();

        // Invoice 2: 5 days ago (2025-03-27), Return (Credit note), BaseNetTotal -500, Posted
        var inv2 = new SalesInvoice(Guid.NewGuid(), _companyId, _customer1Id, "RET-001", asOfDate.AddDays(-5))
        {
            IsReturn = true,
            BaseNetTotal = -500m
        };
        inv2.AddItem(Guid.NewGuid(), "Item 1", -1m, 500m, 0m);
        inv2.Submit();
        inv2.Post();

        _salesInvoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { inv1, inv2 }.AsQueryable()));

        var result = await _appService.GetReportAsync(new InactiveCustomersFilterDto
        {
            DocType = "Sales Invoice",
            DaysSinceLastOrder = 60,
            AsOfDate = asOfDate
        });

        result.Items.Count.ShouldBe(1);
        var row = result.Items[0];
        row.CustomerId.ShouldBe(_customer1Id);
        row.NumOfOrder.ShouldBe(1);
        row.TotalOrderValue.ShouldBe(1500m);
        row.TotalOrderConsidered.ShouldBe(1500m);
        row.LastOrderAmount.ShouldBe(1500m);
        row.LastOrderDate.ShouldBe(asOfDate.AddDays(-80));
        row.DaysSinceLastOrder.ShouldBe(80);
    }
}
