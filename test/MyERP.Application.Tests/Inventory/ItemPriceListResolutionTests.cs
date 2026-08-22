using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for the price-list-selector feature: ItemDetailsResolverService's
/// ResolveEffectivePriceListIdAsync now resolves ONE type-matched (Selling vs Buying) price list
/// before searching ItemPrice candidates, instead of scanning across all price lists regardless of
/// type — previously a Buying-only ItemPrice could be picked up on a Selling transaction (or vice
/// versa) if it happened to be the first row found. Also covers ItemDetailsAppService's fallback to
/// the party's own DefaultPriceListId (Customer/Supplier) when the document/request didn't specify one.
/// </summary>
public abstract class ItemPriceListResolutionTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GetItemDetails_Selling_UsesSellingPriceList_NotBuyingPriceList()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var itemPriceRepo = GetRequiredService<IRepository<ItemPrice, Guid>>();
            var itemDetailsAppService = GetRequiredService<IItemDetailsAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Price List Test Co 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PL-1", "Price List Test Item 1", ItemType.Goods), autoSave: true);

            var buyingList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Standard Buying", "MYR", isSelling: false, isBuying: true),
                autoSave: true);
            var sellingList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Standard Selling", "MYR", isSelling: true, isBuying: false),
                autoSave: true);

            // Buying price inserted first so a type-blind "first match wins" resolver would pick it.
            await itemPriceRepo.InsertAsync(
                new ItemPrice(Guid.NewGuid(), item.Id, buyingList.Id, priceListRate: 10m, uom: "Unit", currencyCode: "MYR"),
                autoSave: true);
            await itemPriceRepo.InsertAsync(
                new ItemPrice(Guid.NewGuid(), item.Id, sellingList.Id, priceListRate: 99m, uom: "Unit", currencyCode: "MYR"),
                autoSave: true);

            var result = await itemDetailsAppService.GetItemDetailsAsync(new GetItemDetailsInput
            {
                ItemId = item.Id,
                CompanyId = company.Id,
                PriceListId = sellingList.Id,
                TransactionType = "Selling",
            });

            result.Rate.ShouldBe(99m);
        });
    }

    [Fact]
    public async Task GetItemDetails_Buying_UsesBuyingPriceList_NotSellingPriceList()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var itemPriceRepo = GetRequiredService<IRepository<ItemPrice, Guid>>();
            var itemDetailsAppService = GetRequiredService<IItemDetailsAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Price List Test Co 2"), autoSave: true);
            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PL-2", "Price List Test Item 2", ItemType.Goods), autoSave: true);

            var sellingList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Standard Selling", "MYR", isSelling: true, isBuying: false),
                autoSave: true);
            var buyingList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Standard Buying", "MYR", isSelling: false, isBuying: true),
                autoSave: true);

            await itemPriceRepo.InsertAsync(
                new ItemPrice(Guid.NewGuid(), item.Id, sellingList.Id, priceListRate: 99m, uom: "Unit", currencyCode: "MYR"),
                autoSave: true);
            await itemPriceRepo.InsertAsync(
                new ItemPrice(Guid.NewGuid(), item.Id, buyingList.Id, priceListRate: 10m, uom: "Unit", currencyCode: "MYR"),
                autoSave: true);

            var result = await itemDetailsAppService.GetItemDetailsAsync(new GetItemDetailsInput
            {
                ItemId = item.Id,
                CompanyId = company.Id,
                PriceListId = buyingList.Id,
                TransactionType = "Buying",
            });

            result.Rate.ShouldBe(10m);
        });
    }

    [Fact]
    public async Task GetItemDetails_Selling_FallsBackToCustomerDefaultPriceList()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var itemPriceRepo = GetRequiredService<IRepository<ItemPrice, Guid>>();
            var itemDetailsAppService = GetRequiredService<IItemDetailsAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Price List Test Co 3"), autoSave: true);
            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PL-3", "Price List Test Item 3", ItemType.Goods), autoSave: true);

            // System-default selling list has a low generic rate.
            var systemDefaultList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Standard Selling", "MYR", isSelling: true, isBuying: false),
                autoSave: true);
            // Customer's own preferred selling list has a distinct, higher rate.
            var customerList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "VIP Selling", "MYR", isSelling: true, isBuying: false),
                autoSave: true);

            await itemPriceRepo.InsertAsync(
                new ItemPrice(Guid.NewGuid(), item.Id, systemDefaultList.Id, priceListRate: 50m, uom: "Unit", currencyCode: "MYR"),
                autoSave: true);
            await itemPriceRepo.InsertAsync(
                new ItemPrice(Guid.NewGuid(), item.Id, customerList.Id, priceListRate: 42m, uom: "Unit", currencyCode: "MYR"),
                autoSave: true);

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "VIP Customer") { DefaultPriceListId = customerList.Id },
                autoSave: true);

            var result = await itemDetailsAppService.GetItemDetailsAsync(new GetItemDetailsInput
            {
                ItemId = item.Id,
                CompanyId = company.Id,
                CustomerId = customer.Id,
                TransactionType = "Selling",
            });

            result.Rate.ShouldBe(42m);
        });
    }

    [Fact]
    public async Task GetItemDetails_Buying_FallsBackToSupplierDefaultPriceList()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var itemPriceRepo = GetRequiredService<IRepository<ItemPrice, Guid>>();
            var itemDetailsAppService = GetRequiredService<IItemDetailsAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Price List Test Co 4"), autoSave: true);
            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PL-4", "Price List Test Item 4", ItemType.Goods), autoSave: true);

            var systemDefaultList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Standard Buying", "MYR", isSelling: false, isBuying: true),
                autoSave: true);
            var supplierList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Preferred Supplier Buying", "MYR", isSelling: false, isBuying: true),
                autoSave: true);

            await itemPriceRepo.InsertAsync(
                new ItemPrice(Guid.NewGuid(), item.Id, systemDefaultList.Id, priceListRate: 30m, uom: "Unit", currencyCode: "MYR"),
                autoSave: true);
            await itemPriceRepo.InsertAsync(
                new ItemPrice(Guid.NewGuid(), item.Id, supplierList.Id, priceListRate: 22m, uom: "Unit", currencyCode: "MYR"),
                autoSave: true);

            var supplier = await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), company.Id, "Preferred Supplier") { DefaultPriceListId = supplierList.Id },
                autoSave: true);

            var result = await itemDetailsAppService.GetItemDetailsAsync(new GetItemDetailsInput
            {
                ItemId = item.Id,
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                TransactionType = "Buying",
            });

            result.Rate.ShouldBe(22m);
        });
    }

    [Fact]
    public async Task GetItemDetails_ExplicitPriceListId_OverridesCustomerDefault()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var itemPriceRepo = GetRequiredService<IRepository<ItemPrice, Guid>>();
            var itemDetailsAppService = GetRequiredService<IItemDetailsAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Price List Test Co 5"), autoSave: true);
            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PL-5", "Price List Test Item 5", ItemType.Goods), autoSave: true);

            var customerDefaultList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Customer Default Selling", "MYR", isSelling: true, isBuying: false),
                autoSave: true);
            var explicitList = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Promo Selling", "MYR", isSelling: true, isBuying: false),
                autoSave: true);

            await itemPriceRepo.InsertAsync(
                new ItemPrice(Guid.NewGuid(), item.Id, customerDefaultList.Id, priceListRate: 42m, uom: "Unit", currencyCode: "MYR"),
                autoSave: true);
            await itemPriceRepo.InsertAsync(
                new ItemPrice(Guid.NewGuid(), item.Id, explicitList.Id, priceListRate: 5m, uom: "Unit", currencyCode: "MYR"),
                autoSave: true);

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Regular Customer") { DefaultPriceListId = customerDefaultList.Id },
                autoSave: true);

            var result = await itemDetailsAppService.GetItemDetailsAsync(new GetItemDetailsInput
            {
                ItemId = item.Id,
                CompanyId = company.Id,
                CustomerId = customer.Id,
                PriceListId = explicitList.Id,
                TransactionType = "Selling",
            });

            result.Rate.ShouldBe(5m);
        });
    }
}
