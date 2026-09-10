using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: every other
/// Purchasing/Sales AppService (PurchaseOrder, PurchaseInvoice, PurchaseReceipt, MaterialRequest,
/// SalesOrder, SalesInvoice, DeliveryNote, PaymentEntry, JournalEntry) wires
/// CompanyRestrictionValidationService.ValidateTransactionCompanyAsync into CreateAsync, blocking a
/// cross-company Item/Supplier/Warehouse reference. RequestForQuotationAppService.CreateAsync was
/// the one sibling that never called it — a Supplier or Warehouse belonging to a different company
/// could be referenced on an RFQ even though the identical reference is blocked on the Purchase
/// Order the RFQ eventually leads to.
/// </summary>
public abstract class RequestForQuotationCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_SupplierFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var rfqAppService = GetRequiredService<IRequestForQuotationAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "RFQ Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "RFQ Guard Other Co"), autoSave: true);

            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "RFQ-ITEM-001", "RFQ Guard Item", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(
                new MyERP.Purchasing.Entities.Supplier(Guid.NewGuid(), otherCompany.Id, "RFQ Guard Cross-Co Supplier"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                rfqAppService.CreateAsync(new CreateRfqDto
                {
                    CompanyId = ownerCompany.Id,
                    TransactionDate = DateTime.Today,
                    Items = new List<CreateRfqItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Widget", Qty = 10m, Uom = "Unit" }
                    },
                    Suppliers = new List<CreateRfqSupplierDto>
                    {
                        new() { SupplierId = supplier.Id }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SameCompanySupplierAndItem_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var rfqAppService = GetRequiredService<IRequestForQuotationAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "RFQ Guard Happy Co"), autoSave: true);

            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RFQ-ITEM-002", "RFQ Guard Item 2", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(
                new MyERP.Purchasing.Entities.Supplier(Guid.NewGuid(), company.Id, "RFQ Guard Same-Co Supplier"), autoSave: true);

            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            await seriesRepository.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "RFQ Series", "RFQ", "RFQ-"), autoSave: true);

            var dto = await rfqAppService.CreateAsync(new CreateRfqDto
            {
                CompanyId = company.Id,
                TransactionDate = DateTime.Today,
                Items = new List<CreateRfqItemDto>
                {
                    new() { ItemId = item.Id, Description = "Widget", Qty = 10m, Uom = "Unit" }
                },
                Suppliers = new List<CreateRfqSupplierDto>
                {
                    new() { SupplierId = supplier.Id }
                }
            });

            dto.Id.ShouldNotBe(Guid.Empty);
        });
    }
}
