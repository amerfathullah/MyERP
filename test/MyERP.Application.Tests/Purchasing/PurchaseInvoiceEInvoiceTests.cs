using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

public abstract class PurchaseInvoiceEInvoiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private async Task<(Company Company, Supplier Supplier, Item Item)> SeedAsync(string tag)
    {
        var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
        var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
        var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
        var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

        var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), $"PI EInv Co {tag}"), autoSave: true);
        await seriesRepo.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, $"PI Series {tag}", "PurchaseInvoice", $"PIE{tag}-"),
            autoSave: true);
        var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, $"PI EInv Supp {tag}"), autoSave: true);
        var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, $"PI-EINV-{tag}", $"EInv Item {tag}", ItemType.Goods), autoSave: true);
        return (company, supplier, item);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistAndDeriveSelfBilledEInvoiceDocType()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (company, supplier, item) = await SeedAsync("A");
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();

            var created = await piAppService.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                IsSubmitToLhdn = true,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = item.Id, Description = "EInv Item A", Quantity = 1, UnitPrice = 100 }
                }
            });

            created.EInvoiceDocType.ShouldBe(EInvoiceDocumentType.SelfBilledInvoice);

            var updatedDebit = await piAppService.UpdateAsync(created.Id, new CreatePurchaseInvoiceDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                EInvoiceDocType = EInvoiceDocumentType.SelfBilledDebitNote,
                IsDebitNote = true,
                IsSubmitToLhdn = false,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = item.Id, Description = "EInv Item A", Quantity = 1, UnitPrice = 120 }
                }
            });

            updatedDebit.EInvoiceDocType.ShouldBe(EInvoiceDocumentType.SelfBilledDebitNote);
            updatedDebit.IsSubmitToLhdn.ShouldBeFalse();

            var updatedRefund = await piAppService.UpdateAsync(created.Id, new CreatePurchaseInvoiceDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                IsReturn = true,
                IsReturnRefund = true,
                EInvoiceDocType = null,
                IsSubmitToLhdn = true,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = item.Id, Description = "EInv Item A", Quantity = -1, UnitPrice = 120 }
                }
            });

            updatedRefund.EInvoiceDocType.ShouldBe(EInvoiceDocumentType.SelfBilledRefundNote);
            updatedRefund.IsSubmitToLhdn.ShouldBeTrue();
        });
    }

    [Fact]
    public async Task UpdateAsync_MismatchedReturnAndEInvoiceDocType_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (company, supplier, item) = await SeedAsync("B");
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();

            var created = await piAppService.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = item.Id, Description = "EInv Item B", Quantity = 1, UnitPrice = 50 }
                }
            });

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                piAppService.UpdateAsync(created.Id, new CreatePurchaseInvoiceDto
                {
                    CompanyId = company.Id,
                    SupplierId = supplier.Id,
                    IssueDate = DateTime.UtcNow.Date,
                    IsReturn = true,
                    IsReturnRefund = false,
                    EInvoiceDocType = EInvoiceDocumentType.SelfBilledInvoice,
                    Items = new List<CreatePurchaseInvoiceItemDto>
                    {
                        new() { ItemId = item.Id, Description = "EInv Item B", Quantity = -1, UnitPrice = 50 }
                    }
                }));

            (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Self-Billed Credit Note");
        });
    }

    [Fact]
    public async Task CreateAsync_ShouldInheritItemClassificationCodeAndAllowLineOverride()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (company, supplier, item) = await SeedAsync("C");
            item.LhdnClassificationCode = "035";
            await GetRequiredService<IRepository<Item, Guid>>().UpdateAsync(item, autoSave: true);

            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();
            var created = await piAppService.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = item.Id, Description = "Inherited Line", Quantity = 1, UnitPrice = 100 },
                    new() { ItemId = item.Id, Description = "Explicit Override Line", Quantity = 1, UnitPrice = 200, ClassificationCode = "034:Self-billed - Importation of goods" }
                }
            });

            created.Items.Count.ShouldBe(2);
            created.Items[0].ClassificationCode.ShouldBe("035");
            created.Items[1].ClassificationCode.ShouldBe("034");
        });
    }

    [Fact]
    public async Task GetPurchaseStatusReportAsync_ReturnsSubmissionQrCodeUrlAndSubmittedAt()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (company, supplier, item) = await SeedAsync("D");
            var piRepo = GetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseInvoice, Guid>>();
            var subRepo = GetRequiredService<IRepository<MyERP.EInvoice.Entities.EInvoiceSubmission, Guid>>();
            var consolRepo = GetRequiredService<IRepository<MyERP.EInvoice.Entities.EInvoiceConsolidation, Guid>>();
            var logRepo = GetRequiredService<IRepository<MyERP.EInvoice.Entities.LhdnSuccessLog, Guid>>();
            var siRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.SalesInvoice, Guid>>();
            var custRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.Customer, Guid>>();
            var suppRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var compRepo = GetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var settingProvider = GetRequiredService<Volo.Abp.Settings.ISettingProvider>();
            var lazyProvider = GetRequiredService<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();

            var httpFactory = NSubstitute.Substitute.For<System.Net.Http.IHttpClientFactory>();
            var lhdnLogger = GetRequiredService<Microsoft.Extensions.Logging.ILogger<MyERP.EInvoice.Services.LhdnApiClient>>();
            var lhdnClient = new MyERP.EInvoice.Services.LhdnApiClient(httpFactory, lhdnLogger);
            var einvServiceDomain = new MyERP.EInvoice.Services.EInvoiceService(lhdnClient, subRepo, logRepo)
            {
                LazyServiceProvider = lazyProvider
            };
            var einvAppService = new MyERP.EInvoice.EInvoiceAppService(
                einvServiceDomain,
                GetRequiredService<MyERP.EInvoice.Services.InvoiceDocumentBuilder>(),
                GetRequiredService<MyERP.EInvoice.Services.InvoiceDocumentSigner>(),
                GetRequiredService<MyERP.EInvoice.Services.EInvoiceValidationService>(),
                GetRequiredService<MyERP.EInvoice.Services.EInvoiceConsolidationService>(),
                new MyERP.EInvoice.Services.TaxpayerValidationService(lhdnClient, settingProvider),
                subRepo,
                consolRepo,
                logRepo,
                siRepo,
                piRepo,
                custRepo,
                suppRepo,
                compRepo,
                settingProvider,
                itemRepo)
            {
                LazyServiceProvider = lazyProvider
            };

            var pi = new MyERP.Purchasing.Entities.PurchaseInvoice(
                Guid.NewGuid(), company.Id, supplier.Id, "PI-QR-001", new DateTime(2026, 7, 10));
            pi.AddItem(item.Id, "Self-billed QR item", 1m, 250m, 0m);
            pi.Submit();
            pi.Post();

            var sub = new MyERP.EInvoice.Entities.EInvoiceSubmission(
                Guid.NewGuid(), company.Id, "PurchaseInvoice", pi.Id, company.TenantId);
            sub.MarkAccepted(
                "sub-uid-123",
                "uuid-123",
                "long-456",
                "https://preprod.myinvois.hasil.gov.my/uuid-123/share/long-456",
                "{}");
            await subRepo.InsertAsync(sub, autoSave: true);

            pi.LhdnSubmissionId = sub.Id;
            pi.LhdnUuid = sub.DocumentUuid;
            pi.LhdnLongId = sub.LongId;
            pi.LhdnSubmittedAt = sub.SubmittedAt;
            pi.EInvoiceStatus = MyERP.Sales.EInvoiceStatus.Valid;
            await piRepo.InsertAsync(pi, autoSave: true);

            var rows = await einvAppService.GetPurchaseStatusReportAsync(new MyERP.EInvoice.LhdnStatusReportRequestDto
            {
                CompanyId = company.Id
            });

            var row = rows.ShouldHaveSingleItem();
            row.InvoiceNumber.ShouldBe("PI-QR-001");
            row.DocumentUuid.ShouldBe("uuid-123");
            row.QrCodeUrl.ShouldBe("https://preprod.myinvois.hasil.gov.my/uuid-123/share/long-456");
            row.SubmittedAt.ShouldBe(sub.SubmittedAt);
        });
    }
}
