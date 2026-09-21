using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
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

public abstract class DeliveryNoteCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_WarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Owner Co 1"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Other Co 1"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "DN Guard Cust 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "DN-ITEM-1", "DN Item 1", ItemType.Goods), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Warehouse 1"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                dnAppService.CreateAsync(new CreateDeliveryNoteDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    WarehouseId = crossWh.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "DN Item 1", Quantity = 1, UnitPrice = 100 }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SalesOrderFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Owner Co 2"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Other Co 2"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "DN Guard Cust 2"), autoSave: true);
            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "DN Guard Other Cust 2"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "DN-ITEM-2", "DN Item 2", ItemType.Goods), autoSave: true);
            var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "Owner Warehouse 2"), autoSave: true);

            var crossSo = new SalesOrder(Guid.NewGuid(), otherCompany.Id, otherCustomer.Id, "SO-OTHER-2", DateTime.UtcNow.Date);
            await soRepo.InsertAsync(crossSo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                dnAppService.CreateAsync(new CreateDeliveryNoteDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    WarehouseId = ownerWh.Id,
                    SalesOrderId = crossSo.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "DN Item 2", Quantity = 1, UnitPrice = 100 }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SalesOrderItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Owner Co 3"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Other Co 3"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "DN Guard Cust 3"), autoSave: true);
            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "DN Guard Other Cust 3"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "DN-ITEM-3", "DN Item 3", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "DN-ITEM-3-OTHER", "DN Item 3 Other", ItemType.Goods), autoSave: true);
            var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "Owner Warehouse 3"), autoSave: true);

            var crossSo = new SalesOrder(Guid.NewGuid(), otherCompany.Id, otherCustomer.Id, "SO-OTHER-3", DateTime.UtcNow.Date);
            crossSo.AddItem(otherItem.Id, "Other Item", 1, 100, 0);
            await soRepo.InsertAsync(crossSo, autoSave: true);

            var crossSoItemId = crossSo.Items[0].Id;

            await Should.ThrowAsync<BusinessException>(() =>
                dnAppService.CreateAsync(new CreateDeliveryNoteDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    WarehouseId = ownerWh.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    SalesOrderId = crossSo.Id,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "DN Item 3", Quantity = 1, UnitPrice = 100, SalesOrderItemId = crossSoItemId }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_ReturnAgainstFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Owner Co 4"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Other Co 4"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "DN Guard Cust 4"), autoSave: true);
            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "DN Guard Other Cust 4"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "DN-ITEM-4", "DN Item 4", ItemType.Goods), autoSave: true);
            var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "Owner Warehouse 4"), autoSave: true);
            var otherWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Other Warehouse 4"), autoSave: true);

            var crossDn = new DeliveryNote(Guid.NewGuid(), otherCompany.Id, otherCustomer.Id, otherWh.Id, "DN-OTHER-4", DateTime.UtcNow.Date);
            await dnRepo.InsertAsync(crossDn, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                dnAppService.CreateAsync(new CreateDeliveryNoteDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    WarehouseId = ownerWh.Id,
                    IsReturn = true,
                    ReturnAgainstId = crossDn.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "DN Item 4", Quantity = 1, UnitPrice = 100 }
                    }
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_SalesOrderItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Owner Co 5"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Other Co 5"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "DN Guard Cust 5"), autoSave: true);
            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "DN Guard Other Cust 5"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "DN-ITEM-5", "DN Item 5", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "DN-ITEM-5-OTHER", "DN Item 5 Other", ItemType.Goods), autoSave: true);
            var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "Owner Warehouse 5"), autoSave: true);

            var crossSo = new SalesOrder(Guid.NewGuid(), otherCompany.Id, otherCustomer.Id, "SO-OTHER-5", DateTime.UtcNow.Date);
            crossSo.AddItem(otherItem.Id, "Other Item", 1, 100, 0);
            await soRepo.InsertAsync(crossSo, autoSave: true);

            var existingDn = new DeliveryNote(Guid.NewGuid(), ownerCompany.Id, customer.Id, ownerWh.Id, "DN-OWNER-5", DateTime.UtcNow.Date);
            existingDn.AddItem(item.Id, "Item 5", 1, 100, 0);
            await dnRepo.InsertAsync(existingDn, autoSave: true);

            var crossSoItemId = crossSo.Items[0].Id;

            await Should.ThrowAsync<BusinessException>(() =>
                dnAppService.UpdateAsync(existingDn.Id, new CreateDeliveryNoteDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    WarehouseId = ownerWh.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    SalesOrderId = crossSo.Id,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Updated Item 5", Quantity = 1, UnitPrice = 100, SalesOrderItemId = crossSoItemId }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_UnpairedSalesOrderReference_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Paired Co"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "DN Paired Cust"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "DN-PAIRED-ITEM", "DN Paired Item", ItemType.Goods), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "DN Paired Wh"), autoSave: true);

            // Case 1: Item has SalesOrderItemId without parent SalesOrderId (Gotcha #233 & #710)
            var ex1 = await Should.ThrowAsync<BusinessException>(() =>
                dnAppService.CreateAsync(new CreateDeliveryNoteDto
                {
                    CompanyId = company.Id,
                    CustomerId = customer.Id,
                    WarehouseId = wh.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 100, SalesOrderItemId = Guid.NewGuid() }
                    }
                }));
            ex1.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // Case 2: Parent SalesOrderId provided, but item has SalesOrderItemId not in that SalesOrder
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PAIRED", DateTime.UtcNow.Date);
            so.AddItem(item.Id, "SO Item", 1, 100, 0);
            await soRepo.InsertAsync(so, autoSave: true);

            var ex2 = await Should.ThrowAsync<BusinessException>(() =>
                dnAppService.CreateAsync(new CreateDeliveryNoteDto
                {
                    CompanyId = company.Id,
                    CustomerId = customer.Id,
                    WarehouseId = wh.Id,
                    SalesOrderId = so.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 100, SalesOrderItemId = Guid.NewGuid() }
                    }
                }));
            ex2.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task SubmitAndCancelAsync_LinkedPickList_UpdatesAndRevertsDeliveredQty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var plRepo = GetRequiredService<IRepository<PickList, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN PickList Co"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "DN PickList Cust"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "DN-PL-ITEM", "DN PL Item", ItemType.Goods) { AllowNegativeStock = true }, autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "DN PL Wh"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "DN Series", "DeliveryNote", "DN-"), autoSave: true);

            // GL fixture: SubmitAsync posts DR COGS / CR Stock (perpetual inventory), which needs a
            // matching AccountingRule per document type plus accounts/fiscal year/cost center — a
            // bare `new Company(...)` here (unlike the real ICompanyAppService) gets none of that
            // seeded automatically (see SubscriptionCatchUpInvoiceTests.SeedGlFixtureAsync for the
            // established pattern).
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var fiscalYearRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var costCenterRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();

            var stockAccount = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1140-DNPL", "Test Stock", AccountType.Asset), autoSave: true);
            var cogsAccount = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5000-DNPL", "Test COGS", AccountType.Expense), autoSave: true);
            var costCenter = await costCenterRepo.InsertAsync(
                new CostCenter(Guid.NewGuid(), company.Id, "DN PL Cost Center"), autoSave: true);

            company.DefaultInventoryAccountId = stockAccount.Id;
            company.DefaultExpenseAccountId = cogsAccount.Id;
            company.DefaultCostCenterId = costCenter.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            await fiscalYearRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY DN PL", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
                autoSave: true);

            // NetTotal (not StockCostTotal) — matches the rule ICompanyAppService.CreateAsync
            // actually seeds for real companies. StockCostTotal would be 0 here since this item
            // has no prior stock ledger history (ValuationRate resolves to 0), which would make
            // the rule engine skip both lines (rawAmount == 0) and post an empty, invalid journal.
            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "DN DR COGS", "DeliveryNote", true, AccountSource.ItemExpense, AmountSource.NetTotal) { SortOrder = 1 },
                autoSave: true);
            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "DN CR Stock", "DeliveryNote", false, AccountSource.FixedAccount, AmountSource.NetTotal) { SortOrder = 2, FixedAccountId = stockAccount.Id },
                autoSave: true);

            var pl = new PickList(Guid.NewGuid(), company.Id, "Delivery") { PickListNumber = "PL-DN-TEST" };
            pl.AddItem(item.Id, wh.Id, 10m);
            pl.Submit();
            await plRepo.InsertAsync(pl, autoSave: true);

            var plItem = pl.Items[0];
            plItem.DeliveredQty.ShouldBe(0m);

            // Create DN linked to PickList
            var dnDto = await dnAppService.CreateAsync(new CreateDeliveryNoteDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                WarehouseId = wh.Id,
                PickListId = pl.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreateDeliveryNoteItemDto>
                {
                    new()
                    {
                        ItemId = item.Id,
                        Description = "Item 1",
                        Quantity = 10,
                        UnitPrice = 50,
                        PickListItemId = plItem.Id
                    }
                }
            });

            // Submit DN -> PickListItem.DeliveredQty increases to 10
            await dnAppService.SubmitAsync(dnDto.Id);

            var loadedPlAfterSubmit = await plRepo.GetAsync(pl.Id);
            loadedPlAfterSubmit.Items[0].DeliveredQty.ShouldBe(10m);
            loadedPlAfterSubmit.PerDelivered.ShouldBe(100m);

            // Cancel DN -> PickListItem.DeliveredQty reverts to 0 (Gotcha #427)
            await dnAppService.CancelAsync(dnDto.Id);

            var loadedPlAfterCancel = await plRepo.GetAsync(pl.Id);
            loadedPlAfterCancel.Items[0].DeliveredQty.ShouldBe(0m);
            loadedPlAfterCancel.PerDelivered.ShouldBe(0m);
        });
    }

    [Fact]
    public async Task CreateAsync_ClosedSalesOrder_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(new Company(Guid.NewGuid(), "DN Closed SO Co 1"), autoSave: true);
            var customer = await GetRequiredService<IRepository<Customer, Guid>>().InsertAsync(new Customer(Guid.NewGuid(), company.Id, "DN Closed SO Cust 1"), autoSave: true);
            var item = await GetRequiredService<IRepository<Item, Guid>>().InsertAsync(new Item(Guid.NewGuid(), company.Id, "DN-ITEM-CLOSED", "DN Item Closed", ItemType.Goods), autoSave: true);
            var wh = await GetRequiredService<IRepository<Warehouse, Guid>>().InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "DN Closed SO WH 1"), autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-DN-CLOSED", DateTime.UtcNow.Date);
            so.AddItem(item.Id, "Item SO", 1, 100, 0);
            so.Submit();
            so.Close();
            await GetRequiredService<IRepository<SalesOrder, Guid>>().InsertAsync(so, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                GetRequiredService<IDeliveryNoteAppService>().CreateAsync(new CreateDeliveryNoteDto
                {
                    CompanyId = company.Id,
                    CustomerId = customer.Id,
                    WarehouseId = wh.Id,
                    SalesOrderId = so.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 100, SalesOrderItemId = so.Items[0].Id }
                    }
                }));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.LinkedSalesOrderClosed);
        });
    }

    [Fact]
    public async Task CreateAsync_DisabledCustomer_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(new Company(Guid.NewGuid(), "DN Disabled Cust Co"), autoSave: true);
            var customer = await GetRequiredService<IRepository<Customer, Guid>>().InsertAsync(new Customer(Guid.NewGuid(), company.Id, "DN Disabled Cust") { IsActive = false }, autoSave: true);
            var item = await GetRequiredService<IRepository<Item, Guid>>().InsertAsync(new Item(Guid.NewGuid(), company.Id, "DN-ITEM-DIS", "DN Item Dis", ItemType.Goods), autoSave: true);
            var wh = await GetRequiredService<IRepository<Warehouse, Guid>>().InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "DN Disabled WH"), autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                GetRequiredService<IDeliveryNoteAppService>().CreateAsync(new CreateDeliveryNoteDto
                {
                    CompanyId = company.Id,
                    CustomerId = customer.Id,
                    WarehouseId = wh.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 10 }
                    }
                }));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.PartyDisabled);
        });
    }
}
