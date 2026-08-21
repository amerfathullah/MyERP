using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Customer credit limit table validation (Gotcha #302):
/// 1. Same company cannot appear twice in credit_limit child list.
/// 2. New credit limit cannot be set below current outstanding amount for that company.
/// </summary>
public class CustomerCreditLimitValidationTests
{
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _companyId1 = Guid.NewGuid();
    private readonly Guid _companyId2 = Guid.NewGuid();

    [Fact]
    public async Task ValidateCustomerCreditLimits_DuplicateCompany_ThrowsDuplicateRecord()
    {
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var orderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var creditLimitRepo = Substitute.For<IRepository<CustomerCreditLimit, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();

        var service = new CreditLimitService(customerRepo, invoiceRepo, orderRepo, creditLimitRepo, settingProvider);

        var limits = new List<CustomerCreditLimit>
        {
            new(Guid.NewGuid(), _customerId, _companyId1, 1000m),
            new(Guid.NewGuid(), _customerId, _companyId1, 2000m) // Duplicate company 1
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ValidateCustomerCreditLimitsAsync(_customerId, limits));

        Assert.Equal(MyERPDomainErrorCodes.DuplicateRecord, ex.Code);
    }

    [Fact]
    public async Task ValidateCustomerCreditLimits_LimitBelowOutstanding_Throws()
    {
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var orderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var creditLimitRepo = Substitute.For<IRepository<CustomerCreditLimit, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();

        // Customer has 5,000 outstanding posted invoice
        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId1, _customerId, "SINV-001", DateTime.UtcNow);
        invoice.AddItem(Guid.NewGuid(), "Widget", 1m, 5000m, 0m);
        invoice.Submit();
        invoice.Post();

        var invoices = new List<SalesInvoice> { invoice }.AsQueryable();
        invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(invoices));

        var service = new CreditLimitService(customerRepo, invoiceRepo, orderRepo, creditLimitRepo, settingProvider);

        var limits = new List<CustomerCreditLimit>
        {
            new(Guid.NewGuid(), _customerId, _companyId1, 3000m) // 3000 < 5000 outstanding
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ValidateCustomerCreditLimitsAsync(_customerId, limits));

        Assert.Equal("MyERP:03002", ex.Code);
    }

    [Fact]
    public async Task ValidateCustomerCreditLimits_ValidLimits_Succeeds()
    {
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var orderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var creditLimitRepo = Substitute.For<IRepository<CustomerCreditLimit, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();

        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId1, _customerId, "SINV-001", DateTime.UtcNow);
        invoice.AddItem(Guid.NewGuid(), "Widget", 1m, 5000m, 0m);
        invoice.Submit();
        invoice.Post();

        var invoices = new List<SalesInvoice> { invoice }.AsQueryable();
        invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(invoices));

        var service = new CreditLimitService(customerRepo, invoiceRepo, orderRepo, creditLimitRepo, settingProvider);

        var limits = new List<CustomerCreditLimit>
        {
            new(Guid.NewGuid(), _customerId, _companyId1, 10000m), // 10000 >= 5000
            new(Guid.NewGuid(), _customerId, _companyId2, 5000m)
        };

        await service.ValidateCustomerCreditLimitsAsync(_customerId, limits);
    }
}
