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
}
