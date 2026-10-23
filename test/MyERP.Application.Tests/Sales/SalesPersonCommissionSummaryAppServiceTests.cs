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

public class SalesPersonCommissionSummaryAppServiceTests
{
    private readonly IRepository<SalesTeamEntry, Guid> _teamEntryRepo = Substitute.For<IRepository<SalesTeamEntry, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<SalesPerson, Guid> _salesPersonRepo = Substitute.For<IRepository<SalesPerson, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Territory, Guid> _territoryRepo = Substitute.For<IRepository<Territory, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _territoryId = Guid.NewGuid();
    private readonly Guid _aliceId = Guid.NewGuid();
    private readonly Guid _bobId = Guid.NewGuid();

    private readonly Company _company;
    private readonly Customer _customer;
    private readonly Territory _territory;
    private readonly SalesPerson _alice;
    private readonly SalesPerson _bob;

    public SalesPersonCommissionSummaryAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Corp") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _territory = new Territory(_territoryId, "Central Region");
        _territoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Territory> { _territory }.AsQueryable()));

        _customer = new Customer(_customerId, _companyId, "Big Retailer")
        {
            CustomerCode = "CUST-BR",
            TerritoryId = _territoryId,
        };
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { _customer }.AsQueryable()));

        _alice = new SalesPerson(_aliceId, "Alice Tan") { CommissionRate = 5m };
        _bob = new SalesPerson(_bobId, "Bob Lee") { CommissionRate = 10m };

        _salesPersonRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesPerson> { _alice, _bob }.AsQueryable()));
    }

    [Fact]
    public async Task GetReportAsync_ShouldReturnEmpty_WhenNoEntries()
    {
        // Arrange
        _salesInvoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice>().AsQueryable()));
        _salesOrderRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder>().AsQueryable()));
        _teamEntryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesTeamEntry>().AsQueryable()));

        var service = new SalesPersonCommissionSummaryAppService(
            _teamEntryRepo,
            _salesInvoiceRepo,
            _salesOrderRepo,
            _salesPersonRepo,
            _customerRepo,
            _territoryRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new GetSalesPersonCommissionSummaryInput
        {
            CompanyId = _companyId,
            DocType = "Sales Invoice",
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.TotalNetAmount.ShouldBe(0);
        result.TotalAllocatedAmount.ShouldBe(0);
        result.TotalIncentives.ShouldBe(0);
        result.TransactionCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_ShouldReturnCommissionSummary_ForSalesInvoice()
    {
        // Arrange
        var invoiceId = Guid.NewGuid();
        var invoice = new SalesInvoice(invoiceId, _companyId, _customerId, "INV-2026-001", new DateTime(2026, 4, 1));
        invoice.AddItem(Guid.NewGuid(), "Widget Pro", 10, 1000m, 0); // NetTotal = 10000
        invoice.Submit();

        _salesInvoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { invoice }.AsQueryable()));

        var entryAlice = new SalesTeamEntry(Guid.NewGuid(), _aliceId, "SalesInvoice", invoiceId, 60m, 10000m, 5m);
        var entryBob = new SalesTeamEntry(Guid.NewGuid(), _bobId, "SalesInvoice", invoiceId, 40m, 10000m, 10m);

        _teamEntryRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<SalesTeamEntry> { entryAlice, entryBob }.AsQueryable()
        ));

        var service = new SalesPersonCommissionSummaryAppService(
            _teamEntryRepo,
            _salesInvoiceRepo,
            _salesOrderRepo,
            _salesPersonRepo,
            _customerRepo,
            _territoryRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new GetSalesPersonCommissionSummaryInput
        {
            CompanyId = _companyId,
            DocType = "Sales Invoice",
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(2);
        result.TransactionCount.ShouldBe(1);
        result.TotalNetAmount.ShouldBe(10000m);
        result.TotalAllocatedAmount.ShouldBe(10000m);
        result.TotalIncentives.ShouldBe(700m); // 300 + 400

        var aliceRow = result.Rows.FirstOrDefault(r => r.SalesPersonId == _aliceId);
        aliceRow.ShouldNotBeNull();
        aliceRow.TransactionNumber.ShouldBe("INV-2026-001");
        aliceRow.CustomerName.ShouldBe("Big Retailer");
        aliceRow.TerritoryName.ShouldBe("Central Region");
        aliceRow.AllocatedPercentage.ShouldBe(60m);
        aliceRow.AllocatedAmount.ShouldBe(6000m);
        aliceRow.CommissionRate.ShouldBe(5m);
        aliceRow.Incentives.ShouldBe(300m);

        var bobRow = result.Rows.FirstOrDefault(r => r.SalesPersonId == _bobId);
        bobRow.ShouldNotBeNull();
        bobRow.AllocatedPercentage.ShouldBe(40m);
        bobRow.AllocatedAmount.ShouldBe(4000m);
        bobRow.CommissionRate.ShouldBe(10m);
        bobRow.Incentives.ShouldBe(400m);
    }

    [Fact]
    public async Task GetReportAsync_ShouldFilterBySalesPerson()
    {
        // Arrange
        var invoiceId = Guid.NewGuid();
        var invoice = new SalesInvoice(invoiceId, _companyId, _customerId, "INV-2026-002", new DateTime(2026, 4, 2));
        invoice.AddItem(Guid.NewGuid(), "Gadget", 1, 5000m, 0);
        invoice.Submit();

        _salesInvoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { invoice }.AsQueryable()));

        var entryAlice = new SalesTeamEntry(Guid.NewGuid(), _aliceId, "SalesInvoice", invoiceId, 50m, 5000m, 5m);
        var entryBob = new SalesTeamEntry(Guid.NewGuid(), _bobId, "SalesInvoice", invoiceId, 50m, 5000m, 10m);

        _teamEntryRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<SalesTeamEntry> { entryAlice, entryBob }.AsQueryable()
        ));

        var service = new SalesPersonCommissionSummaryAppService(
            _teamEntryRepo,
            _salesInvoiceRepo,
            _salesOrderRepo,
            _salesPersonRepo,
            _customerRepo,
            _territoryRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new GetSalesPersonCommissionSummaryInput
        {
            CompanyId = _companyId,
            DocType = "Sales Invoice",
            SalesPersonId = _aliceId,
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].SalesPersonId.ShouldBe(_aliceId);
        result.TotalAllocatedAmount.ShouldBe(2500m);
        result.TotalIncentives.ShouldBe(125m);
    }

    [Fact]
    public async Task GetReportAsync_ShouldReturnCommissionSummary_ForSalesOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new SalesOrder(orderId, _companyId, _customerId, "SO-2026-001", new DateTime(2026, 4, 3));
        order.AddItem(Guid.NewGuid(), "Order Item", 5, 2000m, 0); // NetTotal = 10000
        order.Submit();

        _salesOrderRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder> { order }.AsQueryable()));

        var entryBob = new SalesTeamEntry(Guid.NewGuid(), _bobId, "SalesOrder", orderId, 100m, 10000m, 10m);

        _teamEntryRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<SalesTeamEntry> { entryBob }.AsQueryable()
        ));

        var service = new SalesPersonCommissionSummaryAppService(
            _teamEntryRepo,
            _salesInvoiceRepo,
            _salesOrderRepo,
            _salesPersonRepo,
            _customerRepo,
            _territoryRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new GetSalesPersonCommissionSummaryInput
        {
            CompanyId = _companyId,
            DocType = "Sales Order",
        });

        // Assert
        result.ShouldNotBeNull();
        result.DocType.ShouldBe("Sales Order");
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].TransactionNumber.ShouldBe("SO-2026-001");
        result.Rows[0].AllocatedAmount.ShouldBe(10000m);
        result.Rows[0].Incentives.ShouldBe(1000m);
    }

    [Fact]
    public async Task GetReportAsync_FilterByParentGroupSalesPerson_IncludesDescendantSalesPersons()
    {
        // Per ERPNext PR #61178 (commit 64af5a49d4): filtering by a group/parent Sales Person
        // includes all child/descendant sales persons in the hierarchy.
        var managerId = Guid.NewGuid();
        var manager = new SalesPerson(managerId, "Regional Manager") { IsGroup = true };
        var childRep = new SalesPerson(_aliceId, "Alice Tan", parentId: managerId) { CommissionRate = 5m };
        _salesPersonRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<SalesPerson> { manager, childRep, _bob }.AsQueryable()));

        var invoiceId = Guid.NewGuid();
        var invoice = new SalesInvoice(invoiceId, _companyId, _customerId, "INV-2026-003", new DateTime(2026, 4, 5));
        invoice.AddItem(Guid.NewGuid(), "Widget", 2, 1000m, 0);
        invoice.Submit();
        _salesInvoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { invoice }.AsQueryable()));

        var entryAlice = new SalesTeamEntry(Guid.NewGuid(), _aliceId, "SalesInvoice", invoiceId, 100m, 2000m, 5m);
        _teamEntryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesTeamEntry> { entryAlice }.AsQueryable()));

        var service = new SalesPersonCommissionSummaryAppService(
            _teamEntryRepo, _salesInvoiceRepo, _salesOrderRepo, _salesPersonRepo,
            _customerRepo, _territoryRepo, _companyRepo);

        var result = await service.GetReportAsync(new GetSalesPersonCommissionSummaryInput
        {
            CompanyId = _companyId,
            DocType = "Sales Invoice",
            SalesPersonId = managerId,
        });

        result.Rows.Count.ShouldBe(1);
        result.Rows[0].SalesPersonId.ShouldBe(_aliceId);
        result.TotalAllocatedAmount.ShouldBe(2000m);
    }
}
