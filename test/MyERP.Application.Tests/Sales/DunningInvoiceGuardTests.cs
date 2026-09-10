using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Regression coverage for a real gap found while reading dunning.py: DunningAppService.CreateAsync
/// took every overdue-payment row's SalesInvoiceId/OutstandingAmount fully from client input, with
/// nothing re-fetched or verified server-side — and Submit() posts a GL entry (DR Receivable / CR
/// Income) off the resulting GrandTotal. An unchecked row could reference another customer's
/// invoice, an invoice from a different company, or an unsubmitted invoice with no error.
/// </summary>
public abstract class DunningInvoiceGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_InvoiceBelongsToDifferentCustomer_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var invoiceRepository = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var dunningAppService = GetRequiredService<IDunningAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Dunning Guard Co"), autoSave: true);
            var actualCustomer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Actual Invoice Owner"), autoSave: true);
            var targetCustomer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Dunning Target Customer"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "DUN-ITEM-1", "Dunning Guard Item", ItemType.Goods), autoSave: true);

            var invoice = new SalesInvoice(Guid.NewGuid(), company.Id, actualCustomer.Id, "SI-DUN-001", DateTime.Today);
            invoice.AddItem(item.Id, "Widget", quantity: 1m, unitPrice: 100m, taxAmount: 0m);
            invoice.Submit();
            await invoiceRepository.InsertAsync(invoice, autoSave: true);

            // Dunning is being issued to targetCustomer, but the row references actualCustomer's invoice.
            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                dunningAppService.CreateAsync(new CreateDunningDto
                {
                    CompanyId = company.Id,
                    CustomerId = targetCustomer.Id,
                    PostingDate = DateTime.Today,
                    OverduePayments =
                    [
                        new CreateDunningOverdueDto { SalesInvoiceId = invoice.Id, OutstandingAmount = 100m, DueDate = DateTime.Today.AddDays(-10), OverdueDays = 10 }
                    ]
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_UnsubmittedInvoice_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var invoiceRepository = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var dunningAppService = GetRequiredService<IDunningAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Dunning Draft Guard Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Dunning Draft Customer"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "DUN-ITEM-2", "Dunning Draft Item", ItemType.Goods), autoSave: true);

            var invoice = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SI-DUN-002", DateTime.Today);
            invoice.AddItem(item.Id, "Widget", quantity: 1m, unitPrice: 50m, taxAmount: 0m);
            // Deliberately left in Draft — never submitted.
            await invoiceRepository.InsertAsync(invoice, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                dunningAppService.CreateAsync(new CreateDunningDto
                {
                    CompanyId = company.Id,
                    CustomerId = customer.Id,
                    PostingDate = DateTime.Today,
                    OverduePayments =
                    [
                        new CreateDunningOverdueDto { SalesInvoiceId = invoice.Id, OutstandingAmount = 50m, DueDate = DateTime.Today.AddDays(-5), OverdueDays = 5 }
                    ]
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_ValidSubmittedInvoiceForSameCustomer_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var invoiceRepository = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var dunningAppService = GetRequiredService<IDunningAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Dunning Happy Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Dunning Happy Customer"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "DUN-ITEM-3", "Dunning Happy Item", ItemType.Goods), autoSave: true);

            var invoice = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SI-DUN-003", DateTime.Today);
            invoice.AddItem(item.Id, "Widget", quantity: 1m, unitPrice: 75m, taxAmount: 0m);
            invoice.Submit();
            await invoiceRepository.InsertAsync(invoice, autoSave: true);

            var dto = await dunningAppService.CreateAsync(new CreateDunningDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                PostingDate = DateTime.Today,
                OverduePayments =
                [
                    new CreateDunningOverdueDto { SalesInvoiceId = invoice.Id, OutstandingAmount = 75m, DueDate = DateTime.Today.AddDays(-3), OverdueDays = 3 }
                ]
            });

            dto.Id.ShouldNotBe(Guid.Empty);
        });
    }
}
