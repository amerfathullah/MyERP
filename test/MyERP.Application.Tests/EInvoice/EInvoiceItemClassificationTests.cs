using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.EInvoice;
using MyERP.EInvoice.Entities;
using MyERP.EInvoice.Services;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Application.Tests.EInvoice;

/// <summary>
/// Tests for LHDN MyInvois Item Classification Codes (001-045): normalization/validation,
/// line override -> Item master fallback -> default ("022"/"036"/"004") UBL XML resolution.
/// </summary>
public class EInvoiceItemClassificationTests
{
    [Fact]
    public void LhdnClassificationCodes_ShouldNormalizeAndValidateOfficialCodes()
    {
        LhdnClassificationCodes.Descriptions.Count.ShouldBe(45);
        LhdnClassificationCodes.Normalize("022:Others").ShouldBe("022");
        LhdnClassificationCodes.Normalize(" 036 : Self-billed - Others ").ShouldBe("036");
        LhdnClassificationCodes.Normalize("   ").ShouldBeNull();

        LhdnClassificationCodes.NormalizeAndValidate("003:Computer, smartphone or tablet").ShouldBe("003");
        LhdnClassificationCodes.NormalizeAndValidate(null).ShouldBeNull();

        var ex = Should.Throw<BusinessException>(() => LhdnClassificationCodes.NormalizeAndValidate("999"));
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("999");
    }

    [Fact]
    public async Task SubmitAsync_ShouldResolveLineMasterAndDefaultClassificationCodesInUblXml()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemOverrideId = Guid.NewGuid();
        var itemMasterId = Guid.NewGuid();
        var itemUnsetId = Guid.NewGuid();

        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var purchaseInvoiceRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var submissionRepo = Substitute.For<IRepository<EInvoiceSubmission, Guid>>();
        var consolidationRepo = Substitute.For<IRepository<EInvoiceConsolidation, Guid>>();
        var successLogRepo = Substitute.For<IRepository<LhdnSuccessLog, Guid>>();

        var lhdnClient = Substitute.For<ILhdnApiClient>();
        var capturedXml = new List<string>();
        lhdnClient.SubmitDocumentAsync(
                Arg.Any<string>(),
                Arg.Do<string>(xml => capturedXml.Add(xml)),
                Arg.Any<LhdnEnvironment>())
            .Returns(new LhdnSubmissionResponse
            {
                IsSuccess = true,
                SubmissionUid = "SUB-001",
                DocumentUuid = "UUID-001",
                LongId = "LONG-001",
                RawJson = "{}"
            });

        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync("EInvoice.AccessToken").Returns("valid-token");
        settingProvider.GetOrNullAsync("EInvoice.Environment").Returns("Sandbox");

        var company = new Company(companyId, "LHDN Class Co")
        {
            EnableLhdnInvoice = true,
            TaxId = "C1234567890",
            MsicCode = "46510",
            RegistrationNumber = "202001012345",
        };
        var customer = new Customer(customerId, companyId, "Customer One")
        {
            Tin = "C9876543210",
            IdType = "BRN",
            IdValue = "201901000001",
            Country = "MYS",
        };
        var supplier = new Supplier(supplierId, companyId, "Supplier One")
        {
            Tin = "C1122334455",
            IdType = "BRN",
            IdValue = "201801000002",
            Country = "MYS",
        };

        var masterItem = new Item(itemMasterId, companyId, "SVC-NET", "Internet", ItemType.Service)
        {
            LhdnClassificationCode = "017"
        };
        var unsetItem = new Item(itemUnsetId, companyId, "GEN-01", "Generic", ItemType.Goods);

        companyRepo.GetAsync(companyId).Returns(company);
        customerRepo.GetAsync(customerId).Returns(customer);
        customerRepo.FindAsync(customerId).Returns(customer);
        supplierRepo.GetAsync(supplierId).Returns(supplier);
        supplierRepo.FindAsync(supplierId).Returns(supplier);
        itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(new List<Item> { masterItem, unsetItem });
        consolidationRepo.AnyAsync(Arg.Any<Expression<Func<EInvoiceConsolidation, bool>>>()).Returns(false);

        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SINV-CLS-1", DateTime.UtcNow.Date)
        {
            IsSubmitToLhdn = true,
            CurrencyCode = "MYR",
        };
        si.AddItem(itemOverrideId, "Laptop", 1m, 3000m, 0m, "Unit");
        si.AddItem(itemMasterId, "Fiber", 1m, 100m, 0m, "Unit");
        si.AddItem(itemUnsetId, "Misc", 1m, 50m, 0m, "Unit");
        var siItems = new List<SalesInvoiceItem>(si.Items);
        siItems[0].ClassificationCode = "003"; // explicit line override
        siItems[1].ClassificationCode = null;  // resolves from master Item -> 017
        siItems[2].ClassificationCode = null;  // falls back to 022 (Others)
        si.Submit();
        salesInvoiceRepo.GetAsync(si.Id, includeDetails: true).Returns(si);
        salesInvoiceRepo.GetAsync(si.Id).Returns(si);

        var pi = new PurchaseInvoice(Guid.NewGuid(), companyId, supplierId, "PINV-CLS-1", DateTime.UtcNow.Date)
        {
            IsSubmitToLhdn = true,
            CurrencyCode = "MYR",
        };
        pi.AddItem(itemMasterId, "Cloud Fiber", 1m, 200m, 0m, "Unit");
        pi.AddItem(itemUnsetId, "Foreign Misc", 1m, 80m, 0m, "Unit");
        pi.Submit();
        purchaseInvoiceRepo.GetAsync(pi.Id, includeDetails: true).Returns(pi);
        purchaseInvoiceRepo.GetAsync(pi.Id).Returns(pi);

        var validationService = new EInvoiceValidationService(
            companyRepo, customerRepo, supplierRepo, salesInvoiceRepo, purchaseInvoiceRepo, consolidationRepo);
        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IGuidGenerator>().Returns(SimpleGuidGenerator.Instance);
        lazyProvider.LazyGetService<IGuidGenerator>().Returns(SimpleGuidGenerator.Instance);

        var eInvoiceService = new EInvoiceService(lhdnClient, submissionRepo, successLogRepo)
        {
            LazyServiceProvider = lazyProvider
        };
        var docBuilder = new InvoiceDocumentBuilder();
        var docSigner = new InvoiceDocumentSigner();
        var consolidationService = new EInvoiceConsolidationService(
            salesInvoiceRepo, consolidationRepo, customerRepo, SimpleGuidGenerator.Instance);
        var taxpayerValidationService = new TaxpayerValidationService(lhdnClient, settingProvider);

        var appService = new EInvoiceAppService(
            eInvoiceService,
            docBuilder,
            docSigner,
            validationService,
            consolidationService,
            taxpayerValidationService,
            submissionRepo,
            consolidationRepo,
            successLogRepo,
            salesInvoiceRepo,
            purchaseInvoiceRepo,
            customerRepo,
            supplierRepo,
            companyRepo,
            settingProvider,
            itemRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        await appService.SubmitAsync(new SubmitEInvoiceDto
        {
            CompanyId = companyId,
            SourceDocumentType = "SalesInvoice",
            SourceDocumentId = si.Id
        });

        await appService.SubmitAsync(new SubmitEInvoiceDto
        {
            CompanyId = companyId,
            SourceDocumentType = "PurchaseInvoice",
            SourceDocumentId = pi.Id
        });

        capturedXml.Count.ShouldBe(2);
        var salesXml = capturedXml[0];
        salesXml.ShouldContain("<cbc:ItemClassificationCode listID=\"CLASS\">003</cbc:ItemClassificationCode>");
        salesXml.ShouldContain("<cbc:ItemClassificationCode listID=\"CLASS\">017</cbc:ItemClassificationCode>");
        salesXml.ShouldContain("<cbc:ItemClassificationCode listID=\"CLASS\">022</cbc:ItemClassificationCode>");

        var purchaseXml = capturedXml[1];
        purchaseXml.ShouldContain("<cbc:ItemClassificationCode listID=\"CLASS\">017</cbc:ItemClassificationCode>");
        purchaseXml.ShouldContain("<cbc:ItemClassificationCode listID=\"CLASS\">036</cbc:ItemClassificationCode>");
    }
}
