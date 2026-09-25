using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class QuotationRevisionAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IQuotationAppService _quotationAppService;
    private readonly ISalesOrderAppService _salesOrderAppService;
    private readonly IDocumentConversionAppService _conversionAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<DocumentSeries, Guid> _seriesRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Quotation, Guid> _quotationRepository;

    protected QuotationRevisionAppServiceTests()
    {
        _quotationAppService = GetRequiredService<IQuotationAppService>();
        _salesOrderAppService = GetRequiredService<ISalesOrderAppService>();
        _conversionAppService = GetRequiredService<IDocumentConversionAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
        _itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        _seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        _warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        _quotationRepository = GetRequiredService<IRepository<Quotation, Guid>>();
    }

    private async Task<(Guid companyId, Guid customerId, Guid itemId, Guid warehouseId)> SeedDataAsync()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Revision Co"), autoSave: true);

        var customer = await _customerRepository.InsertAsync(
            new Customer(Guid.NewGuid(), company.Id, "Revision Customer"), autoSave: true);

        var item = await _itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "REV-ITEM", "Revision Item", ItemType.Goods), autoSave: true);

        var warehouse = await _warehouseRepository.InsertAsync(
            new Warehouse(Guid.NewGuid(), company.Id, "Revision Warehouse"), autoSave: true);

        await _seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "QS", "Quotation", "QT-"), autoSave: true);
        await _seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "SOS", "SalesOrder", "SO-"), autoSave: true);

        return (company.Id, customer.Id, item.Id, warehouse.Id);
    }

    [Fact]
    public async Task CreateRevision_FromSubmittedQuotation_CreatesDraftWithIncrementedIndex()
    {
        var (companyId, customerId, itemId, _) = await SeedDataAsync();

        var qtn = await _quotationAppService.CreateAsync(new CreateQuotationDto
        {
            CompanyId = companyId,
            CustomerId = customerId,
            IssueDate = DateTime.Today,
            Items = new()
            {
                new() { ItemId = itemId, Description = "Widget", Quantity = 10, UnitPrice = 50m }
            }
        });

        await _quotationAppService.SubmitAsync(qtn.Id);

        var revision = await _quotationAppService.CreateRevisionAsync(qtn.Id);

        revision.ShouldNotBeNull();
        revision.Status.ShouldBe("Draft");
        revision.RevisionOfId.ShouldBe(qtn.Id);
        revision.RevisionIndex.ShouldBe(1);
        revision.QuotationNumber.ShouldEndWith("-R1");
        revision.IsActive.ShouldBeFalse();

        // Original remains active until revision is submitted
        var original = await _quotationRepository.GetAsync(qtn.Id);
        original.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task SubmitRevision_DeactivatesPreviousVersions()
    {
        var (companyId, customerId, itemId, _) = await SeedDataAsync();

        var qtn1 = await _quotationAppService.CreateAsync(new CreateQuotationDto
        {
            CompanyId = companyId,
            CustomerId = customerId,
            IssueDate = DateTime.Today,
            Items = new() { new() { ItemId = itemId, Description = "Widget", Quantity = 10, UnitPrice = 50m } }
        });
        await _quotationAppService.SubmitAsync(qtn1.Id);

        var qtn2 = await _quotationAppService.CreateRevisionAsync(qtn1.Id);
        await _quotationAppService.SubmitAsync(qtn2.Id);

        var original = await _quotationRepository.GetAsync(qtn1.Id);
        original.IsActive.ShouldBeFalse();

        var rev = await _quotationRepository.GetAsync(qtn2.Id);
        rev.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task ConversionToSalesOrder_BlockedForInactiveQuotation()
    {
        var (companyId, customerId, itemId, _) = await SeedDataAsync();

        var qtn1 = await _quotationAppService.CreateAsync(new CreateQuotationDto
        {
            CompanyId = companyId,
            CustomerId = customerId,
            IssueDate = DateTime.Today,
            Items = new() { new() { ItemId = itemId, Description = "Widget", Quantity = 10, UnitPrice = 50m } }
        });
        await _quotationAppService.SubmitAsync(qtn1.Id);

        var qtn2 = await _quotationAppService.CreateRevisionAsync(qtn1.Id);
        await _quotationAppService.SubmitAsync(qtn2.Id);

        // Attempting to convert deactivated qtn1 throws validation exception
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _conversionAppService.ConvertQuotationToSalesOrderAsync(qtn1.Id));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }
}
