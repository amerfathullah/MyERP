using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class CustomerAcquisitionAndLoyaltyReportTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId1 = Guid.NewGuid();
    private readonly Guid _customerId2 = Guid.NewGuid();
    private readonly Guid _customerId3 = Guid.NewGuid();
    private readonly Guid _customerId4 = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    [Fact]
    public async Task SameDayReturn_NetsAgainstNewCustomerRevenue_WithoutAlteringHeadcount()
    {
        // Per ERPNext PR #60697 / commit cdcb0de6b8:
        // Acquisition is resolved from the first non-return sale.
        // A same-day return nets against new customer revenue, and does not increment headcount.
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var territoryRepo = Substitute.For<IRepository<Territory, Guid>>();

        var date = new DateTime(2026, 3, 15);

        // Regular invoice: 500
        var saleInv = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId1, "SINV-001", date);
        saleInv.AddItem(_itemId, "Item A", 5m, 100m, 0m);
        saleInv.Submit();
        saleInv.Post();

        // Same-day return (credit note): -100
        var returnInv = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId1, "SINV-RET-001", date)
        {
            IsReturn = true
        };
        returnInv.AddItem(_itemId, "Item A (Ret)", -1m, 100m, 0m);
        returnInv.Submit();
        returnInv.Post();

        // Intentionally put return first in repository to test resolution regardless of row order
        var invoices = new List<SalesInvoice> { returnInv, saleInv };
        siRepo.GetQueryableAsync().Returns(Task.FromResult(invoices.AsQueryable()));

        var customers = new List<Customer>
        {
            new Customer(_customerId1, _companyId, "Customer One")
        };
        custRepo.GetQueryableAsync().Returns(Task.FromResult(customers.AsQueryable()));
        territoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Territory>().AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Territory, Guid>>().Returns(territoryRepo);

        var service = new SalesAnalyticsAppService(siRepo, custRepo, itemRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        var result = await service.GetCustomerAcquisitionAndLoyaltyReportAsync(new CustomerAcquisitionAndLoyaltyInputDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 3, 1),
            ToDate = new DateTime(2026, 3, 31),
            TreeView = false
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.Key.ShouldBe("2026-03");
        row.NewCustomerCount.ShouldBe(1);
        row.NewCustomerRevenue.ShouldBe(400m); // 500 - 100 = 400
        row.RepeatCustomerCount.ShouldBe(0);
        row.RepeatCustomerRevenue.ShouldBe(0m);
        row.TotalRevenue.ShouldBe(400m);

        result.TotalNewCustomers.ShouldBe(1);
        result.TotalRepeatCustomers.ShouldBe(0);
        result.TotalNewRevenue.ShouldBe(400m);
        result.TotalRepeatRevenue.ShouldBe(0m);
        result.TotalRevenue.ShouldBe(400m);
    }

    [Fact]
    public async Task StandaloneReturn_NeverAcquiresCustomer_AndDoesNotIncrementHeadcount()
    {
        // Per ERPNext PR #60697:
        // Returns never acquire a customer; they only net revenue.
        // Without an acquiring sale, customer is never "new", and returns never add headcount.
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var territoryRepo = Substitute.For<IRepository<Territory, Guid>>();

        var date = new DateTime(2026, 4, 10);

        var returnInv = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId2, "SINV-RET-STANDALONE", date)
        {
            IsReturn = true
        };
        returnInv.AddItem(_itemId, "Refunded Item", -2m, 75m, 0m);
        returnInv.Submit();
        returnInv.Post();

        var invoices = new List<SalesInvoice> { returnInv };
        siRepo.GetQueryableAsync().Returns(Task.FromResult(invoices.AsQueryable()));

        var customers = new List<Customer>
        {
            new Customer(_customerId2, _companyId, "Customer Two")
        };
        custRepo.GetQueryableAsync().Returns(Task.FromResult(customers.AsQueryable()));
        territoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Territory>().AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Territory, Guid>>().Returns(territoryRepo);

        var service = new SalesAnalyticsAppService(siRepo, custRepo, itemRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        var result = await service.GetCustomerAcquisitionAndLoyaltyReportAsync(new CustomerAcquisitionAndLoyaltyInputDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 4, 1),
            ToDate = new DateTime(2026, 4, 30),
            TreeView = false
        });

        var row = result.Rows[0];
        row.Key.ShouldBe("2026-04");
        row.NewCustomerCount.ShouldBe(0);
        row.NewCustomerRevenue.ShouldBe(0m);
        row.RepeatCustomerCount.ShouldBe(0); // Return does not add headcount
        row.RepeatCustomerRevenue.ShouldBe(-150m);
        row.TotalRevenue.ShouldBe(-150m);

        result.TotalNewCustomers.ShouldBe(0);
        result.TotalRepeatCustomers.ShouldBe(0);
        result.TotalNewRevenue.ShouldBe(0m);
        result.TotalRepeatRevenue.ShouldBe(-150m);
        result.TotalRevenue.ShouldBe(-150m);
    }

    [Fact]
    public async Task PriorAcquisitionBeforeFromDate_ClassifiesSubsequentSalesAsRepeat()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var territoryRepo = Substitute.For<IRepository<Territory, Guid>>();

        // Acquired in 2025
        var priorInv = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId3, "SINV-2025", new DateTime(2025, 11, 20));
        priorInv.AddItem(_itemId, "Item 2025", 1m, 1000m, 0m);
        priorInv.Submit();
        priorInv.Post();

        // Repeat sale in 2026-02
        var repeatInv = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId3, "SINV-2026", new DateTime(2026, 2, 10));
        repeatInv.AddItem(_itemId, "Item 2026", 1m, 250m, 0m);
        repeatInv.Submit();
        repeatInv.Post();

        var invoices = new List<SalesInvoice> { priorInv, repeatInv };
        siRepo.GetQueryableAsync().Returns(Task.FromResult(invoices.AsQueryable()));

        var customers = new List<Customer>
        {
            new Customer(_customerId3, _companyId, "Customer Three")
        };
        custRepo.GetQueryableAsync().Returns(Task.FromResult(customers.AsQueryable()));
        territoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Territory>().AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Territory, Guid>>().Returns(territoryRepo);

        var service = new SalesAnalyticsAppService(siRepo, custRepo, itemRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        var result = await service.GetCustomerAcquisitionAndLoyaltyReportAsync(new CustomerAcquisitionAndLoyaltyInputDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 3, 31),
            TreeView = false
        });

        result.Rows.Count.ShouldBe(3); // 2026-01, 2026-02, 2026-03
        var febRow = result.Rows.Single(r => r.Key == "2026-02");
        febRow.NewCustomerCount.ShouldBe(0);
        febRow.NewCustomerRevenue.ShouldBe(0m);
        febRow.RepeatCustomerCount.ShouldBe(1);
        febRow.RepeatCustomerRevenue.ShouldBe(250m);
        febRow.TotalRevenue.ShouldBe(250m);

        result.TotalNewCustomers.ShouldBe(0);
        result.TotalRepeatCustomers.ShouldBe(1);
        result.TotalRepeatRevenue.ShouldBe(250m);
    }

    [Fact]
    public async Task MonthlyTimeSeries_GeneratesAllMonthsInRange_EvenWithNoSales()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var territoryRepo = Substitute.For<IRepository<Territory, Guid>>();

        // Only one invoice in May 2026
        var inv = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId1, "SINV-MAY", new DateTime(2026, 5, 5));
        inv.AddItem(_itemId, "Item", 1m, 100m, 0m);
        inv.Submit();
        inv.Post();

        siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { inv }.AsQueryable()));
        custRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { new(_customerId1, _companyId, "C1") }.AsQueryable()));
        territoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Territory>().AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Territory, Guid>>().Returns(territoryRepo);

        var service = new SalesAnalyticsAppService(siRepo, custRepo, itemRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        var result = await service.GetCustomerAcquisitionAndLoyaltyReportAsync(new CustomerAcquisitionAndLoyaltyInputDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 4, 1),
            ToDate = new DateTime(2026, 6, 30),
            TreeView = false
        });

        // Must output all 3 months: 2026-04, 2026-05, 2026-06
        result.Rows.Count.ShouldBe(3);
        result.Rows[0].Key.ShouldBe("2026-04");
        result.Rows[0].NewCustomerCount.ShouldBe(0);
        result.Rows[0].TotalRevenue.ShouldBe(0m);

        result.Rows[1].Key.ShouldBe("2026-05");
        result.Rows[1].NewCustomerCount.ShouldBe(1);
        result.Rows[1].TotalRevenue.ShouldBe(100m);

        result.Rows[2].Key.ShouldBe("2026-06");
        result.Rows[2].NewCustomerCount.ShouldBe(0);
        result.Rows[2].TotalRevenue.ShouldBe(0m);
    }

    [Fact]
    public async Task TerritoryTreeView_RollsUpChildTerritoriesToParents_WithoutDoubleCountingTotals()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var territoryRepo = Substitute.For<IRepository<Territory, Guid>>();

        var rootTerritoryId = Guid.NewGuid();
        var countryTerritoryId = Guid.NewGuid();
        var stateTerritoryId = Guid.NewGuid();

        var root = new Territory(rootTerritoryId, "All Territories", parentId: null, isGroup: true);
        var country = new Territory(countryTerritoryId, "Malaysia", parentId: rootTerritoryId, isGroup: true);
        var state = new Territory(stateTerritoryId, "Penang", parentId: countryTerritoryId, isGroup: false);

        var territories = new List<Territory> { root, country, state };
        territoryRepo.GetQueryableAsync().Returns(Task.FromResult(territories.AsQueryable()));

        var customer = new Customer(_customerId4, _companyId, "Penang Customer")
        {
            TerritoryId = stateTerritoryId
        };
        custRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { customer }.AsQueryable()));

        var inv = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId4, "SINV-PENANG", new DateTime(2026, 7, 1));
        inv.AddItem(_itemId, "Solar Panel", 2m, 1500m, 0m);
        inv.Submit();
        inv.Post();

        siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { inv }.AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Territory, Guid>>().Returns(territoryRepo);

        var service = new SalesAnalyticsAppService(siRepo, custRepo, itemRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        var result = await service.GetCustomerAcquisitionAndLoyaltyReportAsync(new CustomerAcquisitionAndLoyaltyInputDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 7, 1),
            ToDate = new DateTime(2026, 7, 31),
            TreeView = true
        });

        // All Territories, Malaysia, and Penang should all show 1 new customer and 3000 revenue
        var rootRow = result.Rows.Single(r => r.Key == "All Territories");
        rootRow.NewCustomerCount.ShouldBe(1);
        rootRow.NewCustomerRevenue.ShouldBe(3000m);

        var countryRow = result.Rows.Single(r => r.Key == "Malaysia");
        countryRow.NewCustomerCount.ShouldBe(1);
        countryRow.NewCustomerRevenue.ShouldBe(3000m);

        var stateRow = result.Rows.Single(r => r.Key == "Penang");
        stateRow.NewCustomerCount.ShouldBe(1);
        stateRow.NewCustomerRevenue.ShouldBe(3000m);

        // Crucial: Report grand total must NOT sum rolled-up amounts 3x (3000 + 3000 + 3000 = 9000). It must be 3000!
        result.TotalNewRevenue.ShouldBe(3000m);
        result.TotalRevenue.ShouldBe(3000m);
        result.TotalNewCustomers.ShouldBe(1);
    }

    [Fact]
    public async Task TerritoryFilter_ScopesInvoicesToSpecifiedTerritoryAndDescendants()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var territoryRepo = Substitute.For<IRepository<Territory, Guid>>();

        var myId = Guid.NewGuid();
        var klId = Guid.NewGuid();
        var sgId = Guid.NewGuid();

        var malaysia = new Territory(myId, "Malaysia", parentId: null, isGroup: true);
        var kualaLumpur = new Territory(klId, "Kuala Lumpur", parentId: myId, isGroup: false);
        var singapore = new Territory(sgId, "Singapore", parentId: null, isGroup: false);

        var territories = new List<Territory> { malaysia, kualaLumpur, singapore };
        territoryRepo.GetQueryableAsync().Returns(Task.FromResult(territories.AsQueryable()));

        var custMY = new Customer(_customerId1, _companyId, "MY Customer") { TerritoryId = klId };
        var custSG = new Customer(_customerId2, _companyId, "SG Customer") { TerritoryId = sgId };

        var customers = new List<Customer> { custMY, custSG };
        custRepo.GetQueryableAsync().Returns(Task.FromResult(customers.AsQueryable()));

        var invMY = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId1, "SINV-MY", new DateTime(2026, 8, 1));
        invMY.AddItem(_itemId, "Item MY", 1m, 500m, 0m);
        invMY.Submit();
        invMY.Post();

        var invSG = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId2, "SINV-SG", new DateTime(2026, 8, 1));
        invSG.AddItem(_itemId, "Item SG", 1m, 1000m, 0m);
        invSG.Submit();
        invSG.Post();

        siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { invMY, invSG }.AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Territory, Guid>>().Returns(territoryRepo);

        var service = new SalesAnalyticsAppService(siRepo, custRepo, itemRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        // Filter specifically by Malaysia territory (should include Kuala Lumpur child, but exclude Singapore)
        var result = await service.GetCustomerAcquisitionAndLoyaltyReportAsync(new CustomerAcquisitionAndLoyaltyInputDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 8, 1),
            ToDate = new DateTime(2026, 8, 31),
            TerritoryId = myId,
            TreeView = false
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.Key.ShouldBe("2026-08");
        row.NewCustomerCount.ShouldBe(1);
        row.NewCustomerRevenue.ShouldBe(500m);
        result.TotalRevenue.ShouldBe(500m);
    }

    [Fact]
    public async Task MultipleInvoicesSameCustomerSameMonth_DistinctHeadcountIsOne()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var territoryRepo = Substitute.For<IRepository<Territory, Guid>>();

        var inv1 = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId1, "SINV-01", new DateTime(2026, 9, 1));
        inv1.AddItem(_itemId, "Item 1", 1m, 200m, 0m);
        inv1.Submit();
        inv1.Post();

        var inv2 = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId1, "SINV-02", new DateTime(2026, 9, 15));
        inv2.AddItem(_itemId, "Item 2", 1m, 300m, 0m);
        inv2.Submit();
        inv2.Post();

        var inv3 = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId1, "SINV-03", new DateTime(2026, 9, 25));
        inv3.AddItem(_itemId, "Item 3", 1m, 500m, 0m);
        inv3.Submit();
        inv3.Post();

        siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { inv1, inv2, inv3 }.AsQueryable()));
        custRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { new(_customerId1, _companyId, "Customer Repeat Invoices") }.AsQueryable()));
        territoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Territory>().AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Territory, Guid>>().Returns(territoryRepo);

        var service = new SalesAnalyticsAppService(siRepo, custRepo, itemRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        var result = await service.GetCustomerAcquisitionAndLoyaltyReportAsync(new CustomerAcquisitionAndLoyaltyInputDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 9, 1),
            ToDate = new DateTime(2026, 9, 30),
            TreeView = false
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.Key.ShouldBe("2026-09");
        row.NewCustomerCount.ShouldBe(1); // Distinct count is 1, not 3
        row.NewCustomerRevenue.ShouldBe(1000m); // 200 + 300 + 500 = 1000
        result.TotalNewCustomers.ShouldBe(1);
        result.TotalRevenue.ShouldBe(1000m);
    }
}

