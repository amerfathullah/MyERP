using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Accounting;

public class PartyDashboardAndCreditLimitFallbackTests
{
    private readonly Guid _companyId1 = Guid.NewGuid();
    private readonly Guid _companyId2 = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();

    [Fact]
    public async Task CustomerDashboard_IncludesSubmittedAndPostedInvoices()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var loyaltyService = Substitute.For<LoyaltyPointService>(
            Substitute.For<IRepository<LoyaltyProgram, Guid>>(),
            Substitute.For<IRepository<LoyaltyPointEntry, Guid>>()
        );

        var postedInvoice = new SalesInvoice(Guid.NewGuid(), _companyId1, _customerId, "SINV-POSTED", DateTime.Today);
        postedInvoice.AddItem(Guid.NewGuid(), "Item 1", 1m, 1000m, 0m);
        postedInvoice.Submit();
        postedInvoice.Post();

        var submittedInvoice = new SalesInvoice(Guid.NewGuid(), _companyId1, _customerId, "SINV-SUBMITTED", DateTime.Today);
        submittedInvoice.AddItem(Guid.NewGuid(), "Item 2", 1m, 500m, 0m);
        submittedInvoice.Submit();

        var invoices = new List<SalesInvoice> { postedInvoice, submittedInvoice };
        siRepo.GetQueryableAsync().Returns(Task.FromResult(invoices.AsQueryable()));

        var company1 = new Company(_companyId1, "Company 1");
        companyRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Company, bool>>>())
            .Returns(Task.FromResult(new List<Company> { company1 }));

        var customer = new Customer(_customerId, _companyId1, "Customer 1");
        custRepo.FindAsync(_customerId).Returns(Task.FromResult<Customer?>(customer));

        var service = new PartyDashboardAppService(siRepo, piRepo, companyRepo, custRepo, loyaltyService);

        var result = await service.GetCustomerDashboardAsync(_customerId);

        result.ShouldNotBeNull();
        result.YtdBilling.ShouldBe(1500m);
        result.TotalUnpaid.ShouldBe(1500m);
        result.Companies.Count.ShouldBe(1);
        result.Companies[0].Id.ShouldBe(_companyId1);
    }

    [Fact]
    public async Task CustomerDashboard_ResolvesCompaniesFromOrdersAndJournals()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var loyaltyService = Substitute.For<LoyaltyPointService>(
            Substitute.For<IRepository<LoyaltyProgram, Guid>>(),
            Substitute.For<IRepository<LoyaltyPointEntry, Guid>>()
        );

        // Sales Invoice only in Company 1
        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId1, _customerId, "SINV-1", DateTime.Today);
        invoice.AddItem(Guid.NewGuid(), "Item 1", 1m, 200m, 0m);
        invoice.Submit();
        invoice.Post();

        siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { invoice }.AsQueryable()));

        var company1 = new Company(_companyId1, "Company 1");
        var company2 = new Company(_companyId2, "Company 2");
        companyRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Company, bool>>>())
            .Returns(Task.FromResult(new List<Company> { company1, company2 }));

        var customer = new Customer(_customerId, _companyId1, "Customer 1");
        custRepo.FindAsync(_customerId).Returns(Task.FromResult<Customer?>(customer));

        // Sales Order in Company 2
        var so = new SalesOrder(Guid.NewGuid(), _companyId2, _customerId, "SO-2", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item SO", 1m, 500m, 0m);
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        soRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetService<IRepository<SalesOrder, Guid>>().Returns(soRepo);

        var service = new PartyDashboardAppService(siRepo, piRepo, companyRepo, custRepo, loyaltyService)
        {
            LazyServiceProvider = lazyProvider
        };

        var result = await service.GetCustomerDashboardAsync(_customerId);

        result.ShouldNotBeNull();
        result.Companies.Count.ShouldBe(2);
        result.Companies.Select(c => c.Id).ShouldContain(_companyId1);
        result.Companies.Select(c => c.Id).ShouldContain(_companyId2);
    }

    [Fact]
    public async Task CreditLimitService_CompanyLimitZero_FallsBackToCustomerGroupDefaultLimit()
    {
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var orderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var creditLimitRepo = Substitute.For<IRepository<CustomerCreditLimit, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();

        var groupId = Guid.NewGuid();
        var customer = new Customer(_customerId, _companyId1, "Customer Group Tracked")
        {
            CreditLimit = 0m, // 0 = not set on customer
            CustomerGroupId = groupId
        };
        custRepo.GetAsync(_customerId).Returns(Task.FromResult(customer));

        // Company limit exists but is 0 (set to 0 to use group or company default per PR #59376)
        var companyLimit = new CustomerCreditLimit(Guid.NewGuid(), _customerId, _companyId1, 0m);
        creditLimitRepo.GetQueryableAsync().Returns(Task.FromResult(new List<CustomerCreditLimit> { companyLimit }.AsQueryable()));

        // Group has default limit 5,000
        var group = new CustomerGroup(groupId, "Wholesale", isGroup: false)
        {
            DefaultCreditLimit = 5000m
        };
        var groupRepo = Substitute.For<IRepository<CustomerGroup, Guid>>();
        groupRepo.FindAsync(groupId).Returns(Task.FromResult<CustomerGroup?>(group));

        // Outstanding 4,000
        var priorInv = new SalesInvoice(Guid.NewGuid(), _companyId1, _customerId, "SINV-PRIOR", DateTime.Today);
        priorInv.AddItem(Guid.NewGuid(), "Item", 1m, 4000m, 0m);
        priorInv.Submit();
        priorInv.Post();
        invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { priorInv }.AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetService<IRepository<CustomerGroup, Guid>>().Returns(groupRepo);

        var service = new CreditLimitService(custRepo, invoiceRepo, orderRepo, creditLimitRepo, settingProvider)
        {
            LazyServiceProvider = lazyProvider
        };

        // Adding 500 exposure (total 4,500 <= 5,000 limit) -> passes
        await service.ValidateCreditLimitAsync(_customerId, 500m, _companyId1);

        // Adding 1,500 exposure (total 5,500 > 5,000 limit) -> throws BusinessException
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.ValidateCreditLimitAsync(_customerId, 1500m, _companyId1)
        );
        ex.Code.ShouldBe("MyERP:03002");
    }
}
