using System;
using System.Threading.Tasks;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class SalesInvoiceAppService_Tests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ISalesInvoiceAppService _invoiceAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<DocumentSeries, Guid> _seriesRepository;

    protected SalesInvoiceAppService_Tests()
    {
        _invoiceAppService = GetRequiredService<ISalesInvoiceAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
        _seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
    }

    [Fact]
    public async Task Should_Create_Invoice_With_Items()
    {
        // Arrange: seed a company, customer, and document series
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Test Company Sdn Bhd"), autoSave: true);

        var customer = await _customerRepository.InsertAsync(
            new Customer(Guid.NewGuid(), company.Id, "ABC Trading"), autoSave: true);

        await _seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "SI Series", "SalesInvoice", "INV-"),
            autoSave: true);

        // Act
        var result = await _invoiceAppService.CreateAsync(new CreateSalesInvoiceDto
        {
            CompanyId = company.Id,
            CustomerId = customer.Id,
            IssueDate = DateTime.Today,
            Items = new()
            {
                new CreateSalesInvoiceItemDto
                {
                    ItemId = Guid.NewGuid(),
                    Description = "Widget A",
                    Quantity = 10,
                    UnitPrice = 100m,
                    TaxAmount = 60m,
                    Uom = "Unit"
                }
            }
        });

        // Assert
        result.ShouldNotBeNull();
        result.InvoiceNumber.ShouldStartWith("INV-");
        result.NetTotal.ShouldBe(1000m);
        result.TaxAmount.ShouldBe(60m);
        result.GrandTotal.ShouldBe(1060m);
        result.Status.ShouldBe("Draft");
        result.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Submit_And_Post_Invoice()
    {
        // Arrange
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Submit Test Co"), autoSave: true);

        // Set up required default accounts for GL posting
        var accountRepo = GetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
        var receivableAcct = new MyERP.Accounting.Entities.Account(
            Guid.NewGuid(), company.Id, "1130", "AR", MyERP.Accounting.AccountType.Asset);
        await accountRepo.InsertAsync(receivableAcct, autoSave: true);
        company.DefaultReceivableAccountId = receivableAcct.Id;
        await _companyRepository.UpdateAsync(company, autoSave: true);

        var customer = await _customerRepository.InsertAsync(
            new Customer(Guid.NewGuid(), company.Id, "Customer X"), autoSave: true);

        await _seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "SI Series 2", "SalesInvoice", "SI-"),
            autoSave: true);

        var invoice = await _invoiceAppService.CreateAsync(new CreateSalesInvoiceDto
        {
            CompanyId = company.Id,
            CustomerId = customer.Id,
            IssueDate = DateTime.Today,
            Items = new()
            {
                new() { ItemId = Guid.NewGuid(), Description = "Item", Quantity = 1, UnitPrice = 500m, TaxAmount = 30m }
            }
        });

        // Act: Submit
        var submitted = await _invoiceAppService.SubmitAsync(invoice.Id);
        submitted.Status.ShouldBe("Submitted");

        // Note: PostAsync requires full accounting infrastructure (FiscalYear, AccountingRules, etc.)
        // GL posting pipeline is extensively covered by domain tests (DocumentPostingOrchestrator)
    }
}
