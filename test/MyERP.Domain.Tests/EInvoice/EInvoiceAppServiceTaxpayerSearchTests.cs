using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.EInvoice;
using MyERP.EInvoice.Entities;
using MyERP.EInvoice.Services;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests.EInvoice;

public class EInvoiceAppServiceTaxpayerSearchTests
{
    private readonly TaxpayerValidationService _mockValidationService;
    private readonly IRepository<EInvoiceSubmission, Guid> _submissionRepo;
    private readonly IRepository<EInvoiceConsolidation, Guid> _consolidationRepo;
    private readonly IRepository<LhdnSuccessLog, Guid> _successLogRepo;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Company, Guid> _companyRepo;
    private readonly ISettingProvider _settingProvider;
    private readonly ILhdnApiClient _lhdnApiClient;

    public EInvoiceAppServiceTaxpayerSearchTests()
    {
        _lhdnApiClient = Substitute.For<ILhdnApiClient>();
        _settingProvider = Substitute.For<ISettingProvider>();
        _settingProvider.GetOrNullAsync("EInvoice.AccessToken").Returns("valid-access-token");
        _settingProvider.GetOrNullAsync("EInvoice.Environment").Returns("Sandbox");

        _mockValidationService = new TaxpayerValidationService(_lhdnApiClient, _settingProvider);

        _submissionRepo = Substitute.For<IRepository<EInvoiceSubmission, Guid>>();
        _consolidationRepo = Substitute.For<IRepository<EInvoiceConsolidation, Guid>>();
        _successLogRepo = Substitute.For<IRepository<LhdnSuccessLog, Guid>>();
        _salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        _purchaseInvoiceRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        _companyRepo = Substitute.For<IRepository<Company, Guid>>();
    }

    private EInvoiceAppService CreateAppService()
    {
        var eInvoiceService = new EInvoiceService(
            _lhdnApiClient,
            _submissionRepo,
            _successLogRepo);
        var docBuilder = new InvoiceDocumentBuilder();
        var docSigner = new InvoiceDocumentSigner();
        var validationService = new EInvoiceValidationService(
            _companyRepo, _customerRepo, _supplierRepo, _salesInvoiceRepo, _purchaseInvoiceRepo, _consolidationRepo);
        var consolService = new EInvoiceConsolidationService(
            _salesInvoiceRepo, _consolidationRepo, _customerRepo, Substitute.For<Volo.Abp.Guids.IGuidGenerator>());

        return new EInvoiceAppService(
            eInvoiceService,
            docBuilder,
            docSigner,
            validationService,
            consolService,
            _mockValidationService,
            _submissionRepo,
            _consolidationRepo,
            _successLogRepo,
            _salesInvoiceRepo,
            _purchaseInvoiceRepo,
            _customerRepo,
            _supplierRepo,
            _companyRepo,
            _settingProvider);
    }

    [Fact]
    public async Task SearchTaxpayerAsync_WithCustomerId_AutoResolvesIdAndPersistsTin()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var customer = new Customer(customerId, companyId, "Customer A")
        {
            IdType = "BRN",
            RegistrationNumber = "202001001234"
        };
        _customerRepo.FindAsync(customerId).Returns(customer);

        _lhdnApiClient.SearchTaxpayerAsync("valid-access-token", "BRN", "202001001234", LhdnEnvironment.Sandbox, "Customer A")
            .Returns(new LhdnTaxpayerSearchResponse
            {
                IsFound = true,
                Tin = "C1234567890",
                TaxpayerName = "Customer A Sdn Bhd"
            });

        var appService = CreateAppService();
        var result = await appService.SearchTaxpayerAsync(new SearchTaxpayerDto
        {
            CustomerId = customerId
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.IsPersisted);
        Assert.Equal("Customer", result.PersistedTarget);
        Assert.Equal("C1234567890", result.Tin);
        Assert.Equal("C1234567890", customer.Tin);
        await _customerRepo.Received(1).UpdateAsync(customer);
    }

    [Fact]
    public async Task SearchTaxpayerAsync_WithSupplierId_AutoResolvesIdAndPersistsTin()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var supplier = new Supplier(supplierId, companyId, "Supplier B")
        {
            IdType = "BRN",
            RegistrationNumber = "202001005678"
        };
        _supplierRepo.FindAsync(supplierId).Returns(supplier);

        _lhdnApiClient.SearchTaxpayerAsync("valid-access-token", "BRN", "202001005678", LhdnEnvironment.Sandbox, "Supplier B")
            .Returns(new LhdnTaxpayerSearchResponse
            {
                IsFound = true,
                Tin = "C5678567800",
                TaxpayerName = "Supplier B Sdn Bhd"
            });

        var appService = CreateAppService();
        var result = await appService.SearchTaxpayerAsync(new SearchTaxpayerDto
        {
            SupplierId = supplierId
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.IsPersisted);
        Assert.Equal("Supplier", result.PersistedTarget);
        Assert.Equal("C5678567800", result.Tin);
        Assert.Equal("C5678567800", supplier.Tin);
        await _supplierRepo.Received(1).UpdateAsync(supplier);
    }

    [Fact]
    public async Task SearchTaxpayerAsync_WithCompanyId_AutoResolvesIdAndPersistsTaxId()
    {
        var companyId = Guid.NewGuid();
        var company = new Company(companyId, "My Company")
        {
            RegistrationNumber = "202001008888"
        };
        _companyRepo.FindAsync(companyId).Returns(company);

        _lhdnApiClient.SearchTaxpayerAsync("valid-access-token", "BRN", "202001008888", LhdnEnvironment.Sandbox, "My Company")
            .Returns(new LhdnTaxpayerSearchResponse
            {
                IsFound = true,
                Tin = "C8888888800",
                TaxpayerName = "My Company Sdn Bhd"
            });

        var appService = CreateAppService();
        var result = await appService.SearchTaxpayerAsync(new SearchTaxpayerDto
        {
            CompanyId = companyId
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.IsPersisted);
        Assert.Equal("Company", result.PersistedTarget);
        Assert.Equal("C8888888800", result.Tin);
        Assert.Equal("C8888888800", company.TaxId);
        await _companyRepo.Received(1).UpdateAsync(company);
    }

    [Fact]
    public async Task SearchTaxpayerAsync_WithSalesInvoiceId_PersistsToSI_AndBackfillsCustomerTin_Gotcha223()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        var customer = new Customer(customerId, companyId, "Cust For SI")
        {
            IdType = "BRN",
            RegistrationNumber = "202001001111",
            Tin = null
        };
        var salesInvoice = new SalesInvoice(invoiceId, companyId, customerId, "INV-2026-0001", DateTime.UtcNow);

        _customerRepo.FindAsync(customerId).Returns(customer);
        _salesInvoiceRepo.FindAsync(invoiceId).Returns(salesInvoice);

        _lhdnApiClient.SearchTaxpayerAsync("valid-access-token", "BRN", "202001001111", LhdnEnvironment.Sandbox, "Cust For SI")
            .Returns(new LhdnTaxpayerSearchResponse
            {
                IsFound = true,
                Tin = "C1112223334",
                TaxpayerName = "Cust For SI Sdn Bhd"
            });

        var appService = CreateAppService();
        var result = await appService.SearchTaxpayerAsync(new SearchTaxpayerDto
        {
            SalesInvoiceId = invoiceId
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.IsPersisted);
        Assert.Equal("SalesInvoice", result.PersistedTarget);
        Assert.Equal("C1112223334", salesInvoice.BuyerTin);

        // Per Gotcha #223 / search_taxpayer.py after_insert: backfill customer TIN when empty
        Assert.Equal("C1112223334", customer.Tin);
        await _salesInvoiceRepo.Received(1).UpdateAsync(salesInvoice);
        await _customerRepo.Received(1).UpdateAsync(customer);
    }

    [Fact]
    public async Task SearchTaxpayerAsync_WithPurchaseInvoiceId_PersistsToPI_AndBackfillsSupplierTin()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        var supplier = new Supplier(supplierId, companyId, "Supp For PI")
        {
            IdType = "BRN",
            RegistrationNumber = "202001002222",
            Tin = null
        };
        var purchaseInvoice = new PurchaseInvoice(invoiceId, companyId, supplierId, "PINV-2026-0001", DateTime.UtcNow);

        _supplierRepo.FindAsync(supplierId).Returns(supplier);
        _purchaseInvoiceRepo.FindAsync(invoiceId).Returns(purchaseInvoice);

        _lhdnApiClient.SearchTaxpayerAsync("valid-access-token", "BRN", "202001002222", LhdnEnvironment.Sandbox, "Supp For PI")
            .Returns(new LhdnTaxpayerSearchResponse
            {
                IsFound = true,
                Tin = "C9998887776",
                TaxpayerName = "Supp For PI Sdn Bhd"
            });

        var appService = CreateAppService();
        var result = await appService.SearchTaxpayerAsync(new SearchTaxpayerDto
        {
            PurchaseInvoiceId = invoiceId
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.IsPersisted);
        Assert.Equal("PurchaseInvoice", result.PersistedTarget);
        Assert.Equal("C9998887776", purchaseInvoice.SupplierTin);

        // Backfill supplier TIN when empty
        Assert.Equal("C9998887776", supplier.Tin);
        await _purchaseInvoiceRepo.Received(1).UpdateAsync(purchaseInvoice);
        await _supplierRepo.Received(1).UpdateAsync(supplier);
    }
}
