using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Unit tests for Customer Acquisition and Loyalty report per ERPNext PR #60697 / commit cdcb0de6b8.
/// Verifies:
/// - Returns never acquire a customer (only non-return invoices establish acquisition).
/// - Return invoices net the customer's revenue without altering acquisition classification.
/// - Standalone credit notes without acquiring sales are never classified as "new".
/// - TreeView (by territory) vs monthly grouping ("yyyy-MM").
/// </summary>
public class CustomerAcquisitionAndLoyaltyReportTests
{
    private readonly IRepository<SalesInvoice, Guid> _siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Territory, Guid> _territoryRepo = Substitute.For<IRepository<Territory, Guid>>();

    private readonly SalesAnalyticsAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customer1Id = Guid.NewGuid();
    private readonly Guid _customer2Id = Guid.NewGuid();
    private readonly Guid _territoryNorthId = Guid.NewGuid();
    private readonly Guid _territorySouthId = Guid.NewGuid();

    public CustomerAcquisitionAndLoyaltyReportTests()
    {
        _appService = new SalesAnalyticsAppService(_siRepo, _customerRepo, _itemRepo);

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<Territory, Guid>>().Returns(_territoryRepo);
        _appService.LazyServiceProvider = lazySp;
    }

    [Fact]
    public async Task ReturnsDoNotAcquireCustomer_OnlySalesInvoicesAcquire()
    {
        // Arrange:
        // Customer 1:
        // Jan 5: Return Invoice (Credit Note) 200
        // Jan 15: Sales Invoice 1000 (Acquisition!)
        var returnInvoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customer1Id, "RET-001", new DateTime(2025, 1, 5))
        {
            IsReturn = true
        };
        returnInvoice.AddItem(Guid.NewGuid(), "Item", -2m, 100m, 0m); // GrandTotal = -200 (or absolute in GrandTotal)
        returnInvoice.Submit();
        returnInvoice.Post();

        var saleInvoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customer1Id, "INV-001", new DateTime(2025, 1, 15));
        saleInvoice.AddItem(Guid.NewGuid(), "Item", 10m, 100m, 0m); // GrandTotal = 1000
        saleInvoice.Submit();
        saleInvoice.Post();

        _siRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { returnInvoice, saleInvoice }.AsQueryable());

        var customer = new Customer(_customer1Id, _companyId, "Customer 1");
        _customerRepo.GetQueryableAsync().Returns(new List<Customer> { customer }.AsQueryable());
        _territoryRepo.GetQueryableAsync().Returns(new List<Territory>().AsQueryable());

        // Act: Run report for Jan 1 to Jan 31
        var result = await _appService.GetCustomerAcquisitionAndLoyaltyReportAsync(new CustomerAcquisitionAndLoyaltyInputDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2025, 1, 1),
            ToDate = new DateTime(2025, 1, 31),
            TreeView = false
        });

        // Assert:
        // Acquisition date is Jan 15 (from saleInvoice, not returnInvoice).
        // Since Jan 15 >= FromDate, customer is "New" in period "2025-01".
        // Revenue is netted: 1000 - 200 = 800.
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.Key.ShouldBe("2025-01");
        row.NewCustomerCount.ShouldBe(1);
        row.RepeatCustomerCount.ShouldBe(0);
        row.NewCustomerRevenue.ShouldBe(800m);
        row.RepeatCustomerRevenue.ShouldBe(0m);
        result.TotalRevenue.ShouldBe(800m);
    }

    [Fact]
    public async Task ExistingCustomer_SubsequentPeriodSale_ClassifiedAsRepeat()
    {
        // Arrange:
        // Customer 1 acquired in 2024-12
        var decSale = new SalesInvoice(Guid.NewGuid(), _companyId, _customer1Id, "INV-DEC", new DateTime(2024, 12, 10));
        decSale.AddItem(Guid.NewGuid(), "Item", 5m, 100m, 0m); // 500
        decSale.Submit();
        decSale.Post();

        // Customer 1 purchases again in 2025-01
        var janSale = new SalesInvoice(Guid.NewGuid(), _companyId, _customer1Id, "INV-JAN", new DateTime(2025, 1, 20));
        janSale.AddItem(Guid.NewGuid(), "Item", 4m, 100m, 0m); // 400
        janSale.Submit();
        janSale.Post();

        _siRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { decSale, janSale }.AsQueryable());

        var customer = new Customer(_customer1Id, _companyId, "Customer 1");
        _customerRepo.GetQueryableAsync().Returns(new List<Customer> { customer }.AsQueryable());
        _territoryRepo.GetQueryableAsync().Returns(new List<Territory>().AsQueryable());

        // Act: Run report for 2025-01
        var result = await _appService.GetCustomerAcquisitionAndLoyaltyReportAsync(new CustomerAcquisitionAndLoyaltyInputDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2025, 1, 1),
            ToDate = new DateTime(2025, 1, 31),
            TreeView = false
        });

        // Assert: Customer was acquired before FromDate (in 2024-12), so in 2025-01 is "Repeat"
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.Key.ShouldBe("2025-01");
        row.NewCustomerCount.ShouldBe(0);
        row.RepeatCustomerCount.ShouldBe(1);
        row.RepeatCustomerRevenue.ShouldBe(400m);
        result.TotalRepeatCustomers.ShouldBe(1);
    }

    [Fact]
    public async Task StandaloneCreditNote_WithoutAcquiringSale_IsClassifiedAsRepeat()
    {
        // Arrange: Only a credit note exists for Customer 1 (no acquiring non-return sale)
        var returnInvoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customer1Id, "RET-ONLY", new DateTime(2025, 2, 5))
        {
            IsReturn = true
        };
        returnInvoice.AddItem(Guid.NewGuid(), "Item", -1m, 300m, 0m);
        returnInvoice.Submit();
        returnInvoice.Post();

        _siRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { returnInvoice }.AsQueryable());

        var customer = new Customer(_customer1Id, _companyId, "Customer 1");
        _customerRepo.GetQueryableAsync().Returns(new List<Customer> { customer }.AsQueryable());
        _territoryRepo.GetQueryableAsync().Returns(new List<Territory>().AsQueryable());

        // Act: Run report for 2025-02
        var result = await _appService.GetCustomerAcquisitionAndLoyaltyReportAsync(new CustomerAcquisitionAndLoyaltyInputDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2025, 2, 1),
            ToDate = new DateTime(2025, 2, 28),
            TreeView = false
        });

        // Assert: Per ERPNext gotcha / PR #60697 & commit cdcb0de6b8:
        // "without an acquiring sale (e.g. a standalone credit note) the customer is never 'new'"
        // and returns net revenue but never count towards headcount
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.NewCustomerCount.ShouldBe(0);
        row.RepeatCustomerCount.ShouldBe(0);
        row.RepeatCustomerRevenue.ShouldBe(-300m);
    }

    [Fact]
    public async Task TreeViewGrouping_GroupsByTerritory()
    {
        // Arrange
        var invNorth = new SalesInvoice(Guid.NewGuid(), _companyId, _customer1Id, "INV-N", new DateTime(2025, 3, 10));
        invNorth.AddItem(Guid.NewGuid(), "Item", 10m, 50m, 0m); // 500
        invNorth.Submit();
        invNorth.Post();

        var invSouth = new SalesInvoice(Guid.NewGuid(), _companyId, _customer2Id, "INV-S", new DateTime(2025, 3, 12));
        invSouth.AddItem(Guid.NewGuid(), "Item", 10m, 80m, 0m); // 800
        invSouth.Submit();
        invSouth.Post();

        _siRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { invNorth, invSouth }.AsQueryable());

        var cust1 = new Customer(_customer1Id, _companyId, "Customer North") { TerritoryId = _territoryNorthId };
        var cust2 = new Customer(_customer2Id, _companyId, "Customer South") { TerritoryId = _territorySouthId };
        _customerRepo.GetQueryableAsync().Returns(new List<Customer> { cust1, cust2 }.AsQueryable());

        var terrNorth = new Territory(_territoryNorthId, "North Region");
        var terrSouth = new Territory(_territorySouthId, "South Region");
        _territoryRepo.GetQueryableAsync().Returns(new List<Territory> { terrNorth, terrSouth }.AsQueryable());

        // Act: TreeView = true
        var result = await _appService.GetCustomerAcquisitionAndLoyaltyReportAsync(new CustomerAcquisitionAndLoyaltyInputDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2025, 3, 1),
            ToDate = new DateTime(2025, 3, 31),
            TreeView = true
        });

        // Assert: Grouped by territory name
        result.Rows.Count.ShouldBe(2);
        var northRow = result.Rows.FirstOrDefault(r => r.Key == "North Region");
        northRow.ShouldNotBeNull();
        northRow.NewCustomerRevenue.ShouldBe(500m);

        var southRow = result.Rows.FirstOrDefault(r => r.Key == "South Region");
        southRow.ShouldNotBeNull();
        southRow.NewCustomerRevenue.ShouldBe(800m);
    }

    [Fact]
    public async Task GetReportAsync_GroupByProject_ExcludesInvoicesWithoutProject()
    {
        // Arrange (ERPNext PR #60011 / commit a3eb5e6b09):
        // Invoice 1 has Project A (rate 1000).
        // Invoice 2 has NO Project (rate 4000).
        // Report grouped by Project should only include Project A (total 1000) and exclude Invoice 2.
        var projectId = Guid.NewGuid();
        var project = new MyERP.Projects.Entities.Project(projectId, _companyId, "PRJ-001", "Project Alpha");
        var projectRepo = Substitute.For<IRepository<MyERP.Projects.Entities.Project, Guid>>();
        projectRepo.GetQueryableAsync().Returns(Task.FromResult(new List<MyERP.Projects.Entities.Project> { project }.AsQueryable()));

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<MyERP.Projects.Entities.Project, Guid>>().Returns(projectRepo);
        _appService.LazyServiceProvider = lazySp;

        var invWithProj = new SalesInvoice(Guid.NewGuid(), _companyId, _customer1Id, "INV-PRJ", new DateTime(2025, 4, 10))
        {
            ProjectId = projectId
        };
        invWithProj.AddItem(Guid.NewGuid(), "Item", 1m, 1000m, 0m);
        invWithProj.Submit();
        invWithProj.Post();

        var invNoProj = new SalesInvoice(Guid.NewGuid(), _companyId, _customer1Id, "INV-NOPRJ", new DateTime(2025, 4, 15));
        invNoProj.AddItem(Guid.NewGuid(), "Item", 1m, 4000m, 0m);
        invNoProj.Submit();
        invNoProj.Post();

        _siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { invWithProj, invNoProj }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new SalesAnalyticsRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2025, 4, 1),
            ToDate = new DateTime(2025, 4, 30),
            GroupBy = AnalyticsGroupBy.Project,
            PeriodType = AnalyticsPeriodType.Monthly
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].EntityId.ShouldBe(projectId.ToString());
        result.Rows[0].EntityName.ShouldBe("Project Alpha");
        result.Rows[0].Total.ShouldBe(1000m);
        result.GrandTotal.ShouldBe(1000m);
    }
}
