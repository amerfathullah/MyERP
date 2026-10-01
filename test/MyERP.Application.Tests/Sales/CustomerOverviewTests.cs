using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class CustomerOverviewTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GetCustomerOverviewAsync_NonExistentCustomer_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var customerService = GetRequiredService<ICustomerAppService>();
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Overview Test Co 1"), autoSave: true);

            await Should.ThrowAsync<EntityNotFoundException>(() =>
                customerService.GetCustomerOverviewAsync(new GetCustomerOverviewInputDto
                {
                    CustomerId = Guid.NewGuid(),
                    CompanyId = company.Id
                }));
        });
    }

    [Fact]
    public async Task GetCustomerOverviewAsync_WithInvoicesAndOrders_CalculatesCorrectMetrics()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var peRepo = GetRequiredService<IRepository<PaymentEntry, Guid>>();
            var customerService = GetRequiredService<ICustomerAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Overview Test Co 2") { CurrencyCode = "MYR" }, autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Overview Test Cust 2"), autoSave: true);
            customer.CreditLimit = 5000m;
            await customerRepo.UpdateAsync(customer, autoSave: true);

            var today = DateTime.UtcNow.Date;

            // Sales Invoice 1: 1000 grand total, 200 paid, overdue by 5 days
            var si1 = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "INV-OV-001", today.AddDays(-15));
            si1.DueDate = today.AddDays(-5);
            si1.AddItem(Guid.NewGuid(), "Item 1", 1, 1000m, 0m);
            si1.Submit();
            si1.Post();
            si1.AmountPaid = 200m;
            await siRepo.InsertAsync(si1, autoSave: true);

            // Sales Order 1: 600 grand total (qty 2 * 300), 1 delivered (50%), 0 billed
            var so1 = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-OV-001", today.AddDays(-3));
            so1.AddItem(Guid.NewGuid(), "Item 1", 2, 300m, 0m);
            so1.Submit();
            so1.Items.First().DeliveredQty = 1;
            await soRepo.InsertAsync(so1, autoSave: true);

            // Payment Entry 1: 150 unallocated advance
            var pe1 = new PaymentEntry(Guid.NewGuid(), company.Id, PaymentType.Receive, today.AddDays(-2), 150m, Guid.NewGuid(), Guid.NewGuid());
            pe1.PartyType = "Customer";
            pe1.PartyId = customer.Id;
            pe1.Submit();
            await peRepo.InsertAsync(pe1, autoSave: true);

            var overview = await customerService.GetCustomerOverviewAsync(new GetCustomerOverviewInputDto
            {
                CustomerId = customer.Id,
                CompanyId = company.Id,
                Period = "Last 30 Days"
            });

            overview.ShouldNotBeNull();
            overview.CustomerName.ShouldBe("Overview Test Cust 2");
            overview.Currency.ShouldBe("MYR");

            // Position checks
            overview.Position.NetSales.Value.ShouldBe(1000m);
            overview.Position.NetSales.Count.ShouldBe(1);
            overview.Position.Outstanding.Value.ShouldBe(800m);
            overview.Position.Outstanding.UnpaidCount.ShouldBe(1);
            overview.Position.Overdue.Value.ShouldBe(800m);
            overview.Position.Overdue.Count.ShouldBe(1);
            overview.Position.Credit.Limit.ShouldBe(5000m);
            overview.Position.Credit.UsedPct.ShouldBe(16.0m); // 800 / 5000 * 100

            // Pipeline checks
            overview.Pipeline.Delivery.Value.ShouldBe(300m); // 600 * 50% remaining = 300
            overview.Pipeline.Delivery.Count.ShouldBe(1);
            overview.Pipeline.Billing.Value.ShouldBe(600m); // 600 * 100% remaining = 600
            overview.Pipeline.Billing.Count.ShouldBe(1);
            overview.Pipeline.Invoices.Value.ShouldBe(800m);
            overview.Pipeline.Invoices.Count.ShouldBe(1);
            overview.Pipeline.Invoices.Overdue.ShouldBe(1);

            overview.UnallocatedAdvances.ShouldBe(150m);
        });
    }

    [Fact]
    public async Task GetCustomerTransactionsAsync_ReturnsOrderedTransactions()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var customerService = GetRequiredService<ICustomerAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Txn Test Co 3"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Txn Test Cust 3"), autoSave: true);

            var today = DateTime.UtcNow.Date;

            var si = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "INV-TXN-001", today.AddDays(-1));
            si.AddItem(Guid.NewGuid(), "Item 1", 1, 500m, 0m);
            si.Submit();
            si.Post();
            await siRepo.InsertAsync(si, autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-TXN-001", today);
            so.AddItem(Guid.NewGuid(), "Item 1", 1, 800m, 0m);
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            var txns = await customerService.GetCustomerTransactionsAsync(new GetCustomerTransactionsInputDto
            {
                CustomerId = customer.Id,
                CompanyId = company.Id,
                DocType = "All",
                MaxResultCount = 10
            });

            txns.ShouldNotBeNull();
            txns.Count.ShouldBe(2);
            txns[0].DocType.ShouldBe("Sales Order");
            txns[0].TransactionNumber.ShouldBe("SO-TXN-001");
            txns[0].Amount.ShouldBe(800m);

            txns[1].DocType.ShouldBe("Sales Invoice");
            txns[1].TransactionNumber.ShouldBe("INV-TXN-001");
            txns[1].Amount.ShouldBe(500m);
            txns[1].OutstandingAmount.ShouldBe(500m);
        });
    }

    [Fact]
    public async Task GetCustomerCompaniesAsync_ReturnsDistinctCompanies()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var customerService = GetRequiredService<ICustomerAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Co Unique Name 4"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Cust 4"), autoSave: true);

            var companies = await customerService.GetCustomerCompaniesAsync(customer.Id);
            companies.ShouldNotBeNull();
            companies.ShouldContain("Co Unique Name 4");
        });
    }
}
