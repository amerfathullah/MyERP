using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Integration tests verifying that transactions reject disabled price lists (PR #58891 / commit f8c2f3440b),
/// ignore disabled party default price lists (PR #58926 / commit fd492100b0), and permit return documents
/// referencing submitted documents with disabled price lists.
/// </summary>
public abstract class PriceListValidationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
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
    public async Task SalesOrder_Create_WithDisabledPriceList_ThrowsPriceListDisabled()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO PL Co 1"), autoSave: true);
            await InsertDocumentSeriesAsync(company.Id, "SalesOrder", "SO-PL1-");
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "SO PL Cust 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SO-PL-1", "SO Item 1", ItemType.Goods), autoSave: true);

            var disabledList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Disabled SO PL", "MYR", isSelling: true, isBuying: false) { IsActive = false },
                autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                soAppService.CreateAsync(new CreateSalesOrderDto
                {
                    CompanyId = company.Id,
                    CustomerId = customer.Id,
                    PriceListId = disabledList.Id,
                    Items = new List<CreateSalesOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "SO Item 1", Quantity = 1, UnitPrice = 100 }
                    }
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PriceListDisabled);
        });
    }

    [Fact]
    public async Task SalesOrder_Create_WithDisabledCustomerDefault_IgnoresPartyDefault()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO PL Co 2"), autoSave: true);
            await InsertDocumentSeriesAsync(company.Id, "SalesOrder", "SO-PL2-");
            var disabledList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Disabled Cust PL 2", "MYR", isSelling: true, isBuying: false) { IsActive = false },
                autoSave: true);
            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "SO PL Cust 2") { DefaultPriceListId = disabledList.Id },
                autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SO-PL-2", "SO Item 2", ItemType.Goods), autoSave: true);

            var order = await soAppService.CreateAsync(new CreateSalesOrderDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                PriceListId = null,
                Items = new List<CreateSalesOrderItemDto>
                {
                    new() { ItemId = item.Id, Description = "SO Item 2", Quantity = 1, UnitPrice = 100 }
                }
            });

            // Disabled default was ignored, so order price list remains null
            order.PriceListId.ShouldBeNull();
        });
    }

    [Fact]
    public async Task SalesInvoice_Create_WithDisabledPriceList_ThrowsPriceListDisabled()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI PL Co 1"), autoSave: true);
            await InsertDocumentSeriesAsync(company.Id, "SalesInvoice", "SI-PL1-");
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "SI PL Cust 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SI-PL-1", "SI Item 1", ItemType.Goods), autoSave: true);

            var disabledList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Disabled SI PL", "MYR", isSelling: true, isBuying: false) { IsActive = false },
                autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                siAppService.CreateAsync(new CreateSalesInvoiceDto
                {
                    CompanyId = company.Id,
                    CustomerId = customer.Id,
                    PriceListId = disabledList.Id,
                    Items = new List<CreateSalesInvoiceItemDto>
                    {
                        new() { ItemId = item.Id, Description = "SI Item 1", Quantity = 1, UnitPrice = 100 }
                    }
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PriceListDisabled);
        });
    }

    [Fact]
    public async Task Quotation_Create_WithDisabledPriceList_ThrowsPriceListDisabled()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var quotAppService = GetRequiredService<IQuotationAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Quot PL Co 1"), autoSave: true);
            await InsertDocumentSeriesAsync(company.Id, "Quotation", "QUOT-PL1-");
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Quot PL Cust 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "QUOT-PL-1", "Quot Item 1", ItemType.Goods), autoSave: true);

            var disabledList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Disabled Quot PL", "MYR", isSelling: true, isBuying: false) { IsActive = false },
                autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                quotAppService.CreateAsync(new CreateQuotationDto
                {
                    CompanyId = company.Id,
                    CustomerId = customer.Id,
                    PriceListId = disabledList.Id,
                    Items = new List<CreateQuotationItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Quot Item 1", Quantity = 1, UnitPrice = 100 }
                    }
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PriceListDisabled);
        });
    }

    [Fact]
    public async Task PurchaseOrder_Create_WithDisabledPriceList_ThrowsPriceListDisabled()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var poAppService = GetRequiredService<IPurchaseOrderAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO PL Co 1"), autoSave: true);
            await InsertDocumentSeriesAsync(company.Id, "PurchaseOrder", "PO-PL1-");
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "PO PL Supp 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "PO-PL-1", "PO Item 1", ItemType.Goods), autoSave: true);

            var disabledList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Disabled PO PL", "MYR", isSelling: false, isBuying: true) { IsActive = false },
                autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                poAppService.CreateAsync(new CreatePurchaseOrderDto
                {
                    CompanyId = company.Id,
                    SupplierId = supplier.Id,
                    PriceListId = disabledList.Id,
                    Items = new List<CreatePurchaseOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "PO Item 1", Quantity = 1, UnitPrice = 100 }
                    }
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PriceListDisabled);
        });
    }

    [Fact]
    public async Task PurchaseInvoice_Create_WithDisabledPriceList_ThrowsPriceListDisabled()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI PL Co 1"), autoSave: true);
            await InsertDocumentSeriesAsync(company.Id, "PurchaseInvoice", "PI-PL1-");
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "PI PL Supp 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "PI-PL-1", "PI Item 1", ItemType.Goods), autoSave: true);

            var disabledList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Disabled PI PL", "MYR", isSelling: false, isBuying: true) { IsActive = false },
                autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                piAppService.CreateAsync(new CreatePurchaseInvoiceDto
                {
                    CompanyId = company.Id,
                    SupplierId = supplier.Id,
                    PriceListId = disabledList.Id,
                    Items = new List<CreatePurchaseInvoiceItemDto>
                    {
                        new() { ItemId = item.Id, Description = "PI Item 1", Quantity = 1, UnitPrice = 100 }
                    }
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PriceListDisabled);
        });
    }

    [Fact]
    public async Task SalesInvoice_Return_WithDisabledPriceListMatchingReturnAgainst_Succeeds()
    {
        // Per ERPNext PR #58891 / commit f8c2f3440b:
        // Returns retain a submitted voucher's pricing even if its price list is now disabled.
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Return PL Co"), autoSave: true);
            await InsertDocumentSeriesAsync(company.Id, "SalesInvoice", "SI-RET-");
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "SI Return PL Cust"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SI-RET-1", "SI Return Item 1", ItemType.Goods), autoSave: true);

            var priceList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Original Sales PL", "MYR", isSelling: true, isBuying: false) { IsActive = true },
                autoSave: true);

            // 1. Create original invoice with active price list
            var origInvoice = await siAppService.CreateAsync(new CreateSalesInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                PriceListId = priceList.Id,
                Items = new List<CreateSalesInvoiceItemDto>
                {
                    new() { ItemId = item.Id, Description = "SI Item 1", Quantity = 5, UnitPrice = 100 }
                }
            });

            // 2. Disable the price list
            priceList.IsActive = false;
            await priceListRepo.UpdateAsync(priceList, autoSave: true);

            // 3. Create return invoice (Credit Note) referencing original invoice with the now-disabled price list
            var returnInvoice = await siAppService.CreateAsync(new CreateSalesInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                PriceListId = priceList.Id,
                IsReturn = true,
                ReturnAgainstId = origInvoice.Id,
                Items = new List<CreateSalesInvoiceItemDto>
                {
                    new() { ItemId = item.Id, Description = "SI Item 1", Quantity = -2, UnitPrice = 100 }
                }
            });

            returnInvoice.ShouldNotBeNull();
            returnInvoice.PriceListId.ShouldBe(priceList.Id);
            returnInvoice.IsReturn.ShouldBeTrue();
            returnInvoice.ReturnAgainstId.ShouldBe(origInvoice.Id);
        });
    }
}
