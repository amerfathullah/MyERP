using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

public abstract class UpstreamBatch31IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    protected async Task InsertDocumentSeriesAsync(Guid companyId, string docType, string prefix)
    {
        var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        await seriesRepo.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), companyId, $"{docType} Series", docType, prefix),
            autoSave: true);
    }
    [Fact]
    public async Task SalesInvoice_Submit_RejectsDuplicateFixedAsset_OnNonReturnInvoice()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Asset Check Co B31") { CurrencyCode = "MYR" },
                autoSave: true);
            await InsertDocumentSeriesAsync(company.Id, "SalesInvoice", "SI-B31-");

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Asset Cust B31"),
                autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-ASSET-B31", "Asset Item B31", ItemType.Goods),
                autoSave: true);

            var duplicateAssetId = Guid.NewGuid();

            var created = await siAppService.CreateAsync(new CreateSalesInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                IssueDate = DateTime.UtcNow,
                Items = new List<CreateSalesInvoiceItemDto>
                {
                    new()
                    {
                        ItemId = item.Id,
                        Description = "Asset Part 1",
                        Quantity = 1,
                        UnitPrice = 1000,
                        IsFixedAsset = true,
                        AssetId = duplicateAssetId
                    },
                    new()
                    {
                        ItemId = item.Id,
                        Description = "Asset Part 2",
                        Quantity = 1,
                        UnitPrice = 500,
                        IsFixedAsset = true,
                        AssetId = duplicateAssetId
                    }
                }
            });

            // Submitting invoice with duplicate asset should throw ValidationFailed (PR #63881)
            var ex = await Should.ThrowAsync<BusinessException>(() => siAppService.SubmitAsync(created.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            ex.Data["detail"]!.ToString()!.ShouldContain($"Asset {duplicateAssetId} is already added in another row");
        });
    }

    [Fact]
    public async Task Dunning_OpeningInvoiceWithoutSchedule_AllowedAndRejectsDuplicates()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var dunningAppService = GetRequiredService<IDunningAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Dunning Opening Co B31") { CurrencyCode = "MYR" },
                autoSave: true);

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Dunning Cust B31"),
                autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-DUNNING-B31", "Dunning Item B31", ItemType.Goods),
                autoSave: true);

            var today = DateTime.UtcNow.Date;
            var dueDate = today.AddDays(-15);

            var openingInvoice = new SalesInvoice(
                Guid.NewGuid(), company.Id, customer.Id, "SINV-OPENING-B31", today.AddDays(-30))
            {
                IsOpening = true,
                DueDate = dueDate
            };
            openingInvoice.AddItem(item.Id, "Opening Item", 1m, 2500m, 0m);
            openingInvoice.Submit();
            await siRepo.InsertAsync(openingInvoice, autoSave: true);

            // 1. PR #63608 / commit 480a9faeb6: duplicate invoice reference in dunning should be rejected
            var duplicateInput = new CreateDunningDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                PostingDate = today,
                Currency = "MYR",
                OverduePayments = new[]
                {
                    new CreateDunningOverdueDto
                    {
                        SalesInvoiceId = openingInvoice.Id,
                        PaymentScheduleId = null,
                        OutstandingAmount = 1000m,
                        DueDate = dueDate,
                        OverdueDays = 15
                    },
                    new CreateDunningOverdueDto
                    {
                        SalesInvoiceId = openingInvoice.Id,
                        PaymentScheduleId = null,
                        OutstandingAmount = 1500m,
                        DueDate = dueDate,
                        OverdueDays = 15
                    }
                }
            };

            var ex = await Should.ThrowAsync<BusinessException>(() => dunningAppService.CreateAsync(duplicateInput));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            ex.Data["detail"]!.ToString()!.ShouldContain("Sales Invoice is already included in this Dunning.");

            // 2. PR #63608 / commit 480a9faeb6: valid single opening invoice without payment schedule succeeds
            var validInput = new CreateDunningDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                PostingDate = today,
                Currency = "MYR",
                OverduePayments = new[]
                {
                    new CreateDunningOverdueDto
                    {
                        SalesInvoiceId = openingInvoice.Id,
                        PaymentScheduleId = null,
                        OutstandingAmount = 2500m,
                        DueDate = dueDate,
                        OverdueDays = 15
                    }
                }
            };

            var dunning = await dunningAppService.CreateAsync(validInput);
            dunning.ShouldNotBeNull();
            dunning.OverduePayments.Count.ShouldBe(1);
            dunning.OverduePayments[0].SalesInvoiceId.ShouldBe(openingInvoice.Id);
            dunning.OverduePayments[0].PaymentScheduleId.ShouldBeNull();
            dunning.OverduePayments[0].OutstandingAmount.ShouldBe(2500m);
        });
    }
}
