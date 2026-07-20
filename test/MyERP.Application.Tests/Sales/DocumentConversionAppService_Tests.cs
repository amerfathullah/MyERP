using System;
using System.Threading.Tasks;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class DocumentConversionAppService_Tests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IDocumentConversionAppService _conversionService;
    private readonly IQuotationAppService _quotationService;
    private readonly ISalesOrderAppService _salesOrderService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<DocumentSeries, Guid> _seriesRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;

    protected DocumentConversionAppService_Tests()
    {
        _conversionService = GetRequiredService<IDocumentConversionAppService>();
        _quotationService = GetRequiredService<IQuotationAppService>();
        _salesOrderService = GetRequiredService<ISalesOrderAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
        _seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        _warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        _salesOrderRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
    }

    private async Task<(Guid companyId, Guid customerId, Guid warehouseId)> SeedDataAsync()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Conversion Test Co"), autoSave: true);

        var customer = await _customerRepository.InsertAsync(
            new Customer(Guid.NewGuid(), company.Id, "Test Buyer"), autoSave: true);

        var warehouse = await _warehouseRepository.InsertAsync(
            new Warehouse(Guid.NewGuid(), company.Id, "Main Warehouse"), autoSave: true);

        await _seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "QS", "Quotation", "QT-"), autoSave: true);
        await _seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "SOS", "SalesOrder", "SO-"), autoSave: true);
        await _seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "DNS", "DeliveryNote", "DN-"), autoSave: true);
        await _seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "SIS", "SalesInvoice", "SI-"), autoSave: true);

        return (company.Id, customer.Id, warehouse.Id);
    }

    [Fact]
    public async Task Should_Convert_Quotation_To_SalesOrder()
    {
        // Arrange
        var (companyId, customerId, _) = await SeedDataAsync();

        var quotation = await _quotationService.CreateAsync(new CreateQuotationDto
        {
            CompanyId = companyId,
            CustomerId = customerId,
            IssueDate = DateTime.Today,
            Items = new()
            {
                new() { ItemId = Guid.NewGuid(), Description = "Widget", Quantity = 5, UnitPrice = 200m, TaxAmount = 60m }
            }
        });

        // Must submit before conversion
        await _quotationService.SubmitAsync(quotation.Id);

        // Act
        var salesOrder = await _conversionService.ConvertQuotationToSalesOrderAsync(quotation.Id);

        // Assert
        salesOrder.ShouldNotBeNull();
        salesOrder.OrderNumber.ShouldStartWith("SO-");
        salesOrder.CustomerId.ShouldBe(customerId);
        salesOrder.QuotationId.ShouldBe(quotation.Id);
        salesOrder.Items.Count.ShouldBe(1);
        salesOrder.Items[0].Quantity.ShouldBe(5);
        salesOrder.Items[0].UnitPrice.ShouldBe(200m);
        salesOrder.NetTotal.ShouldBe(1000m);
        salesOrder.Status.ShouldBe("Draft");
    }

    [Fact]
    public async Task Should_Fail_Converting_Draft_Quotation()
    {
        // Arrange
        var (companyId, customerId, _) = await SeedDataAsync();

        var quotation = await _quotationService.CreateAsync(new CreateQuotationDto
        {
            CompanyId = companyId,
            CustomerId = customerId,
            IssueDate = DateTime.Today,
            Items = new()
            {
                new() { ItemId = Guid.NewGuid(), Description = "Item", Quantity = 1, UnitPrice = 100m }
            }
        });

        // Act & Assert: converting a Draft should fail
        await Assert.ThrowsAsync<Volo.Abp.BusinessException>(
            () => _conversionService.ConvertQuotationToSalesOrderAsync(quotation.Id));
    }

    [Fact]
    public async Task Should_Convert_SalesOrder_To_SalesInvoice()
    {
        // Arrange
        var (companyId, customerId, _) = await SeedDataAsync();

        var quotation = await _quotationService.CreateAsync(new CreateQuotationDto
        {
            CompanyId = companyId,
            CustomerId = customerId,
            IssueDate = DateTime.Today,
            Items = new()
            {
                new() { ItemId = Guid.NewGuid(), Description = "Service A", Quantity = 2, UnitPrice = 500m, TaxAmount = 60m },
                new() { ItemId = Guid.NewGuid(), Description = "Service B", Quantity = 1, UnitPrice = 300m, TaxAmount = 18m },
            }
        });
        await _quotationService.SubmitAsync(quotation.Id);

        var salesOrder = await _conversionService.ConvertQuotationToSalesOrderAsync(quotation.Id);
        await _salesOrderService.SubmitAsync(salesOrder.Id);

        // Act
        var invoice = await _conversionService.ConvertSalesOrderToSalesInvoiceAsync(salesOrder.Id);

        // Assert
        invoice.ShouldNotBeNull();
        invoice.InvoiceNumber.ShouldStartWith("SI-");
        invoice.CustomerId.ShouldBe(customerId);
        invoice.Items.Count.ShouldBe(2);
        invoice.NetTotal.ShouldBe(1300m); // 2*500 + 1*300
        invoice.TaxAmount.ShouldBe(78m);  // 60 + 18
        invoice.GrandTotal.ShouldBe(1378m);
        invoice.Status.ShouldBe("Draft");
    }

    [Fact]
    public async Task Should_Prevent_Double_Conversion_Of_Quotation()
    {
        // Arrange
        var (companyId, customerId, _) = await SeedDataAsync();

        var quotation = await _quotationService.CreateAsync(new CreateQuotationDto
        {
            CompanyId = companyId,
            CustomerId = customerId,
            IssueDate = DateTime.Today,
            Items = new()
            {
                new() { ItemId = Guid.NewGuid(), Description = "Item", Quantity = 1, UnitPrice = 100m }
            }
        });
        await _quotationService.SubmitAsync(quotation.Id);

        // First conversion succeeds
        await _conversionService.ConvertQuotationToSalesOrderAsync(quotation.Id);

        // Act & Assert: second conversion should fail
        await Assert.ThrowsAsync<Volo.Abp.BusinessException>(
            () => _conversionService.ConvertQuotationToSalesOrderAsync(quotation.Id));
    }

    [Fact]
    public async Task Should_Convert_SalesOrder_To_DeliveryNote()
    {
        // Arrange
        var (companyId, customerId, warehouseId) = await SeedDataAsync();

        var salesOrder = await _salesOrderService.CreateAsync(new CreateSalesOrderDto
        {
            CompanyId = companyId,
            CustomerId = customerId,
            OrderDate = DateTime.Today,
            Items = new()
            {
                new() { ItemId = Guid.NewGuid(), Description = "Product X", Quantity = 10, UnitPrice = 50m, TaxAmount = 30m, WarehouseId = warehouseId }
            }
        });
        await _salesOrderService.SubmitAsync(salesOrder.Id);

        // Act
        var deliveryNote = await _conversionService.ConvertSalesOrderToDeliveryNoteAsync(salesOrder.Id);

        // Assert
        deliveryNote.ShouldNotBeNull();
        deliveryNote.DeliveryNumber.ShouldStartWith("DN-");
        deliveryNote.CustomerId.ShouldBe(customerId);
        deliveryNote.SalesOrderId.ShouldBe(salesOrder.Id);
        deliveryNote.Items.Count.ShouldBe(1);
        deliveryNote.Items[0].Quantity.ShouldBe(10);
        deliveryNote.Status.ShouldBe("Draft");
    }
}
