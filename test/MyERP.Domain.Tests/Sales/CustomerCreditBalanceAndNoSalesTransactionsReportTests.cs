using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Sales;

public class CustomerCreditBalanceAndNoSalesTransactionsReportTests
{
    private readonly Guid _companyId1 = Guid.NewGuid();
    private readonly Guid _companyId2 = Guid.NewGuid();
    private readonly Guid _customerId1 = Guid.NewGuid();
    private readonly Guid _customerId2 = Guid.NewGuid();
    private readonly Guid _customerId3 = Guid.NewGuid();

    [Fact]
    public async Task CreditLimitService_CompanyCreditLimit_FallsBackWhenCustomerAndGroupZero()
    {
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var orderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var creditLimitRepo = Substitute.For<IRepository<CustomerCreditLimit, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();

        var customer = new Customer(_customerId1, _companyId1, "Customer Co Limit Fallback")
        {
            CreditLimit = 0m,
            CustomerGroupId = null
        };
        custRepo.GetAsync(_customerId1).Returns(Task.FromResult(customer));
        creditLimitRepo.GetQueryableAsync().Returns(Task.FromResult(new List<CustomerCreditLimit>().AsQueryable()));

        var company = new Company(_companyId1, "Test Co")
        {
            CreditLimit = 25000m
        };
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        companyRepo.FindAsync(_companyId1).Returns(Task.FromResult<Company?>(company));

        var priorInv = new SalesInvoice(Guid.NewGuid(), _companyId1, _customerId1, "SINV-CO-1", DateTime.Today);
        priorInv.AddItem(Guid.NewGuid(), "Item", 1m, 20000m, 0m);
        priorInv.Submit();
        priorInv.Post();
        invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { priorInv }.AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetService<IRepository<Company, Guid>>().Returns(companyRepo);

        var service = new CreditLimitService(custRepo, invoiceRepo, orderRepo, creditLimitRepo, settingProvider)
        {
            LazyServiceProvider = lazyProvider
        };

        // Outstanding 20k + new 4k = 24k <= 25k limit -> passes
        await service.ValidateCreditLimitAsync(_customerId1, 4000m, _companyId1);

        // Outstanding 20k + new 6k = 26k > 25k limit -> throws BusinessException
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.ValidateCreditLimitAsync(_customerId1, 6000m, _companyId1)
        );
        ex.Code.ShouldBe("MyERP:03002");
    }

    [Fact]
    public async Task CreditLimitService_GetResolvedCreditLimitAsync_FollowsHierarchy()
    {
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var orderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var creditLimitRepo = Substitute.For<IRepository<CustomerCreditLimit, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();

        var groupId = Guid.NewGuid();
        var customer = new Customer(_customerId1, _companyId1, "Customer Hierarchy")
        {
            CreditLimit = 10000m,
            CustomerGroupId = groupId
        };
        custRepo.GetAsync(_customerId1).Returns(Task.FromResult(customer));

        var group = new CustomerGroup(groupId, "Wholesale", isGroup: false) { DefaultCreditLimit = 20000m };
        var groupRepo = Substitute.For<IRepository<CustomerGroup, Guid>>();
        groupRepo.FindAsync(groupId).Returns(Task.FromResult<CustomerGroup?>(group));

        var company = new Company(_companyId1, "Test Co") { CreditLimit = 30000m };
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        companyRepo.FindAsync(_companyId1).Returns(Task.FromResult<Company?>(company));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetService<IRepository<CustomerGroup, Guid>>().Returns(groupRepo);
        lazyProvider.LazyGetService<IRepository<Company, Guid>>().Returns(companyRepo);

        // Case 1: Customer has explicit limit 10k -> resolves to 10k
        creditLimitRepo.GetQueryableAsync().Returns(Task.FromResult(new List<CustomerCreditLimit>().AsQueryable()));
        var service = new CreditLimitService(custRepo, invoiceRepo, orderRepo, creditLimitRepo, settingProvider)
        {
            LazyServiceProvider = lazyProvider
        };
        var (resolved1, bypass1) = await service.GetResolvedCreditLimitAsync(_customerId1, _companyId1);
        resolved1.ShouldBe(10000m);
        bypass1.ShouldBeFalse();

        // Case 2: Customer limit is 0 -> falls back to group 20k
        customer.CreditLimit = 0m;
        var (resolved2, bypass2) = await service.GetResolvedCreditLimitAsync(_customerId1, _companyId1);
        resolved2.ShouldBe(20000m);
        bypass2.ShouldBeFalse();

        // Case 3: Customer limit is 0 and group limit is 0 -> falls back to company 30k
        group.DefaultCreditLimit = 0m;
        var (resolved3, bypass3) = await service.GetResolvedCreditLimitAsync(_customerId1, _companyId1);
        resolved3.ShouldBe(30000m);
        bypass3.ShouldBeFalse();

        // Case 4: Customer per-company row bypasses
        var perCompany = new CustomerCreditLimit(Guid.NewGuid(), _customerId1, _companyId1, 5000m)
        {
            BypassCreditLimitCheck = true
        };
        creditLimitRepo.GetQueryableAsync().Returns(Task.FromResult(new List<CustomerCreditLimit> { perCompany }.AsQueryable()));
        var (resolved4, bypass4) = await service.GetResolvedCreditLimitAsync(_customerId1, _companyId1);
        bypass4.ShouldBeTrue();
    }

    [Fact]
    public async Task GetCustomerCreditBalanceReport_ListsCustomerWithCompanyLimit_PerPR60286()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();

        var company = new Company(_companyId1, "Test Co") { CreditLimit = 50000m };
        companyRepo.GetAsync(_companyId1).Returns(Task.FromResult(company));

        var customer = new Customer(_customerId1, _companyId1, "Customer With Co Limit")
        {
            CreditLimit = 0m,
            CustomerGroupId = null,
            IsActive = true,
            IsFrozen = false,
            BypassCreditLimitCheckAtSalesOrder = false
        };
        customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { customer }.AsQueryable()));

        var creditLimitService = Substitute.For<CreditLimitService>(
            customerRepo, siRepo, Substitute.For<IRepository<SalesOrder, Guid>>(),
            Substitute.For<IRepository<CustomerCreditLimit, Guid>>(), Substitute.For<ISettingProvider>());
        creditLimitService.GetResolvedCreditLimitAsync(_customerId1, _companyId1).Returns(Task.FromResult((50000m, false)));
        creditLimitService.GetCustomerOutstandingAsync(_customerId1, _companyId1).Returns(Task.FromResult(12000m));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Company, Guid>>().Returns(companyRepo);
        lazyProvider.LazyGetRequiredService<CreditLimitService>().Returns(creditLimitService);

        var appService = new SalesAnalyticsAppService(siRepo, customerRepo, itemRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        var report = await appService.GetCustomerCreditBalanceReportAsync(new CustomerCreditBalanceInputDto
        {
            CompanyId = _companyId1
        });

        report.Count.ShouldBe(1);
        var row = report[0];
        row.CustomerId.ShouldBe(_customerId1);
        row.CustomerName.ShouldBe("Customer With Co Limit");
        row.CreditLimit.ShouldBe(50000m);
        row.OutstandingAmount.ShouldBe(12000m);
        row.CreditBalance.ShouldBe(38000m);
        row.Disabled.ShouldBeFalse();
        row.IsFrozen.ShouldBeFalse();
    }

    [Fact]
    public async Task GetCustomerCreditBalanceReport_ExcludesCustomerWithoutAnyLimit()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();

        var company = new Company(_companyId1, "Test Co") { CreditLimit = 0m };
        companyRepo.GetAsync(_companyId1).Returns(Task.FromResult(company));

        var customer = new Customer(_customerId1, _companyId1, "Unlimited Customer") { CreditLimit = 0m };
        customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { customer }.AsQueryable()));

        var creditLimitService = Substitute.For<CreditLimitService>(
            customerRepo, siRepo, Substitute.For<IRepository<SalesOrder, Guid>>(),
            Substitute.For<IRepository<CustomerCreditLimit, Guid>>(), Substitute.For<ISettingProvider>());
        creditLimitService.GetResolvedCreditLimitAsync(_customerId1, _companyId1).Returns(Task.FromResult((0m, false)));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Company, Guid>>().Returns(companyRepo);
        lazyProvider.LazyGetRequiredService<CreditLimitService>().Returns(creditLimitService);

        var appService = new SalesAnalyticsAppService(siRepo, customerRepo, itemRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        var report = await appService.GetCustomerCreditBalanceReportAsync(new CustomerCreditBalanceInputDto
        {
            CompanyId = _companyId1
        });

        report.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetCustomersWithoutSalesTransactions_TreatsDeliveryNoteAndPosAsSalesTransactions_PerPR60308()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        var groupRepo = Substitute.For<IRepository<CustomerGroup, Guid>>();
        var territoryRepo = Substitute.For<IRepository<Territory, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();

        companyRepo.GetAsync(_companyId1).Returns(Task.FromResult(new Company(_companyId1, "Test Co")));

        var custNoTxn = new Customer(_customerId1, _companyId1, "No Txn Customer");
        var custWithDn = new Customer(_customerId2, _companyId1, "DN Only Customer");
        var custWithPos = new Customer(_customerId3, _companyId1, "POS Only Customer");

        customerRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<Customer> { custNoTxn, custWithDn, custWithPos }.AsQueryable()));

        // Delivery Note submitted for custWithDn (PR #60308)
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId1, _customerId2, Guid.NewGuid(), "DN-1", DateTime.Today);
        dn.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        dn.Submit(); // Status = Submitted
        dnRepo.GetQueryableAsync().Returns(Task.FromResult(new List<DeliveryNote> { dn }.AsQueryable()));

        // POS Sales Invoice posted for custWithPos (PR #60308)
        var posInv = new SalesInvoice(Guid.NewGuid(), _companyId1, _customerId3, "POS-1", DateTime.Today)
        {
            IsPos = true
        };
        posInv.AddItem(Guid.NewGuid(), "Item", 1m, 50m, 0m);
        posInv.Submit();
        posInv.Post();
        siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { posInv }.AsQueryable()));

        // No Sales Orders
        soRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder>().AsQueryable()));

        // Groups / Territories
        groupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<CustomerGroup>().AsQueryable()));
        territoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Territory>().AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Company, Guid>>().Returns(companyRepo);
        lazyProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>().Returns(soRepo);
        lazyProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>().Returns(dnRepo);
        lazyProvider.LazyGetRequiredService<IRepository<CustomerGroup, Guid>>().Returns(groupRepo);
        lazyProvider.LazyGetRequiredService<IRepository<Territory, Guid>>().Returns(territoryRepo);

        var appService = new SalesAnalyticsAppService(siRepo, customerRepo, itemRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        var result = await appService.GetCustomersWithoutSalesTransactionsAsync(new CustomersWithoutSalesInputDto
        {
            CompanyId = _companyId1
        });

        // Only custNoTxn should be returned
        result.Count.ShouldBe(1);
        result[0].CustomerId.ShouldBe(_customerId1);
        result[0].CustomerName.ShouldBe("No Txn Customer");
    }

    [Fact]
    public async Task GetCustomersWithoutSalesTransactions_DraftAndCancelledDoNotExclude()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        var groupRepo = Substitute.For<IRepository<CustomerGroup, Guid>>();
        var territoryRepo = Substitute.For<IRepository<Territory, Guid>>();

        var custDraftOnly = new Customer(_customerId1, _companyId1, "Draft Only Customer");
        customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { custDraftOnly }.AsQueryable()));

        // Draft invoice (Status = Draft)
        var draftInv = new SalesInvoice(Guid.NewGuid(), _companyId1, _customerId1, "DRAFT-1", DateTime.Today);
        draftInv.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { draftInv }.AsQueryable()));

        // Cancelled delivery note (Status = Cancelled)
        var cancelledDn = new DeliveryNote(Guid.NewGuid(), _companyId1, _customerId1, Guid.NewGuid(), "CAN-DN-1", DateTime.Today);
        cancelledDn.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        cancelledDn.Submit();
        cancelledDn.Cancel();
        dnRepo.GetQueryableAsync().Returns(Task.FromResult(new List<DeliveryNote> { cancelledDn }.AsQueryable()));

        soRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder>().AsQueryable()));
        groupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<CustomerGroup>().AsQueryable()));
        territoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Territory>().AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>().Returns(soRepo);
        lazyProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>().Returns(dnRepo);
        lazyProvider.LazyGetRequiredService<IRepository<CustomerGroup, Guid>>().Returns(groupRepo);
        lazyProvider.LazyGetRequiredService<IRepository<Territory, Guid>>().Returns(territoryRepo);

        var appService = new SalesAnalyticsAppService(siRepo, customerRepo, itemRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        var result = await appService.GetCustomersWithoutSalesTransactionsAsync(new CustomersWithoutSalesInputDto());

        // Draft and Cancelled are not submitted transactions -> customer should still be listed
        result.Count.ShouldBe(1);
        result[0].CustomerId.ShouldBe(_customerId1);
    }

    [Fact]
    public async Task GetCustomersWithoutSalesTransactions_CompanyFilterScopesTransactions_PerCommitBed57babd4()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        var groupRepo = Substitute.For<IRepository<CustomerGroup, Guid>>();
        var territoryRepo = Substitute.For<IRepository<Territory, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();

        companyRepo.GetAsync(_companyId1).Returns(Task.FromResult(new Company(_companyId1, "Company 1")));
        companyRepo.GetAsync(_companyId2).Returns(Task.FromResult(new Company(_companyId2, "Company 2")));

        var customer = new Customer(_customerId1, _companyId1, "Scoped Customer");
        customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { customer }.AsQueryable()));

        // Customer has posted invoice in Company 1 ONLY
        var invCo1 = new SalesInvoice(Guid.NewGuid(), _companyId1, _customerId1, "CO1-INV", DateTime.Today);
        invCo1.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        invCo1.Submit();
        invCo1.Post();
        siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { invCo1 }.AsQueryable()));

        dnRepo.GetQueryableAsync().Returns(Task.FromResult(new List<DeliveryNote>().AsQueryable()));
        soRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder>().AsQueryable()));
        groupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<CustomerGroup>().AsQueryable()));
        territoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Territory>().AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Company, Guid>>().Returns(companyRepo);
        lazyProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>().Returns(soRepo);
        lazyProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>().Returns(dnRepo);
        lazyProvider.LazyGetRequiredService<IRepository<CustomerGroup, Guid>>().Returns(groupRepo);
        lazyProvider.LazyGetRequiredService<IRepository<Territory, Guid>>().Returns(territoryRepo);

        var appService = new SalesAnalyticsAppService(siRepo, customerRepo, itemRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        // Scoped to Company 1: customer has invoice -> excluded
        var resultCo1 = await appService.GetCustomersWithoutSalesTransactionsAsync(new CustomersWithoutSalesInputDto
        {
            CompanyId = _companyId1
        });
        resultCo1.ShouldBeEmpty();

        // Scoped to Company 2: customer has NO transactions in Company 2 -> included
        var resultCo2 = await appService.GetCustomersWithoutSalesTransactionsAsync(new CustomersWithoutSalesInputDto
        {
            CompanyId = _companyId2
        });
        resultCo2.Count.ShouldBe(1);
        resultCo2[0].CustomerId.ShouldBe(_customerId1);
    }
}
