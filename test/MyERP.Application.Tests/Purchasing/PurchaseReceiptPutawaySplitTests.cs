using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Coverage for receiving one Purchase Receipt into more than one warehouse — what ERPNext's
/// "Apply Putaway Rule" produces when an item's qty exceeds a single warehouse's capacity.
///
/// Three things had to be true for this to work and none of them were before:
/// CreatePurchaseReceiptItemDto had no WarehouseId at all, submit posted every SLE/Bin movement
/// against receipt.WarehouseId regardless of the line, and the seeded "PR DR Stock" rule used
/// AccountSource.FixedAccount — so the per-warehouse account the AppService resolves and passes in
/// was discarded, and stock landing in warehouse B would have been booked to warehouse A's account.
/// </summary>
public abstract class PurchaseReceiptPutawaySplitTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubmitAsync_SplitAcrossWarehouses_MovesStockAndSplitsGlPerWarehouseAccount()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var warehouseAccountRepository = GetRequiredService<IRepository<WarehouseAccount, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var ruleRepository = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var costCenterRepository = GetRequiredService<IRepository<CostCenter, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var binRepository = GetRequiredService<IRepository<Bin, Guid>>();
            var journalRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var appService = GetRequiredService<IPurchaseReceiptAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Putaway Split Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Putaway Supplier"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PWS-1", "Putaway Split Item", ItemType.Goods), autoSave: true);

            var mainWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Putaway Main"), autoSave: true);
            var overflowWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Putaway Overflow"), autoSave: true);

            // Each warehouse gets its own stock account — the whole point of the split.
            var mainStockAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1410-PWS", "Stock - Main", AccountType.Asset), autoSave: true);
            var overflowStockAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1411-PWS", "Stock - Overflow", AccountType.Asset), autoSave: true);
            var srbnbAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "2110-PWS", "Stock Received But Not Billed", AccountType.Liability), autoSave: true);

            await warehouseAccountRepository.InsertAsync(
                new WarehouseAccount(Guid.NewGuid(), mainWarehouse.Id, company.Id, mainStockAccount.Id), autoSave: true);
            await warehouseAccountRepository.InsertAsync(
                new WarehouseAccount(Guid.NewGuid(), overflowWarehouse.Id, company.Id, overflowStockAccount.Id), autoSave: true);

            var costCenter = await costCenterRepository.InsertAsync(
                new CostCenter(Guid.NewGuid(), company.Id, "Putaway Cost Center"), autoSave: true);
            company.DefaultInventoryAccountId = mainStockAccount.Id;
            company.StockReceivedButNotBilledAccountId = srbnbAccount.Id;
            company.DefaultCostCenterId = costCenter.Id;
            await companyRepository.UpdateAsync(company, autoSave: true);

            await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY Putaway", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
                autoSave: true);
            await seriesRepository.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "PR Series", "PurchaseReceipt", "PWS-"), autoSave: true);

            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PR DR Stock", "PurchaseReceipt", true, AccountSource.WarehouseStock, AmountSource.NetTotal)
                { SortOrder = 1, FixedAccountId = mainStockAccount.Id }, autoSave: true);
            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PR CR SRBNB", "PurchaseReceipt", false, AccountSource.FixedAccount, AmountSource.NetTotal)
                { SortOrder = 2, FixedAccountId = srbnbAccount.Id }, autoSave: true);

            // 30 units into main, 10 into overflow — the shape putaway allocation produces when
            // the first warehouse fills up. Receipt-level warehouse is main.
            var created = await appService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                WarehouseId = mainWarehouse.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new() { ItemId = item.Id, Description = "Main split", Quantity = 30m, UnitPrice = 5m, Uom = "Unit", WarehouseId = mainWarehouse.Id },
                    new() { ItemId = item.Id, Description = "Overflow split", Quantity = 10m, UnitPrice = 5m, Uom = "Unit", WarehouseId = overflowWarehouse.Id },
                },
            });

            await appService.SubmitAsync(created.Id);

            // Stock physically landed in both warehouses, not all in the header one.
            var bins = (await binRepository.GetQueryableAsync())
                .Where(b => b.ItemId == item.Id)
                .ToList();
            bins.Single(b => b.WarehouseId == mainWarehouse.Id).ActualQty.ShouldBe(30m);
            bins.Single(b => b.WarehouseId == overflowWarehouse.Id).ActualQty.ShouldBe(10m);

            var journal = (await journalRepository.GetQueryableAsync())
                .Single(j => j.ReferenceType == "PurchaseReceipt" && j.ReferenceId == created.Id);
            var lines = journal.Lines.ToList();

            lines.Sum(l => l.IsDebit ? l.Amount : 0m).ShouldBe(lines.Sum(l => !l.IsDebit ? l.Amount : 0m));

            // The stock leg is split by value moved: 150 to main (30 x 5), 50 to overflow (10 x 5).
            lines.Single(l => l.AccountId == mainStockAccount.Id && l.IsDebit).Amount.ShouldBe(150m);
            lines.Single(l => l.AccountId == overflowStockAccount.Id && l.IsDebit).Amount.ShouldBe(50m);
            lines.Single(l => l.AccountId == srbnbAccount.Id && !l.IsDebit).Amount.ShouldBe(200m);
        });
    }

    [Fact]
    public async Task SubmitAsync_SingleWarehouse_PostsOneStockLineAsBefore()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var warehouseAccountRepository = GetRequiredService<IRepository<WarehouseAccount, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var ruleRepository = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var costCenterRepository = GetRequiredService<IRepository<CostCenter, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var journalRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var appService = GetRequiredService<IPurchaseReceiptAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Putaway Single Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Single Supplier"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PWS-2", "Single Warehouse Item", ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Single Main"), autoSave: true);

            var stockAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1420-PWS", "Stock - Single", AccountType.Asset), autoSave: true);
            var srbnbAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "2120-PWS", "SRBNB - Single", AccountType.Liability), autoSave: true);
            await warehouseAccountRepository.InsertAsync(
                new WarehouseAccount(Guid.NewGuid(), warehouse.Id, company.Id, stockAccount.Id), autoSave: true);

            var costCenter = await costCenterRepository.InsertAsync(
                new CostCenter(Guid.NewGuid(), company.Id, "Single Cost Center"), autoSave: true);
            company.DefaultInventoryAccountId = stockAccount.Id;
            company.StockReceivedButNotBilledAccountId = srbnbAccount.Id;
            company.DefaultCostCenterId = costCenter.Id;
            await companyRepository.UpdateAsync(company, autoSave: true);

            await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY Single", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
                autoSave: true);
            await seriesRepository.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "PR Series Single", "PurchaseReceipt", "PWSS-"), autoSave: true);
            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PR DR Stock", "PurchaseReceipt", true, AccountSource.WarehouseStock, AmountSource.NetTotal)
                { SortOrder = 1, FixedAccountId = stockAccount.Id }, autoSave: true);
            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PR CR SRBNB", "PurchaseReceipt", false, AccountSource.FixedAccount, AmountSource.NetTotal)
                { SortOrder = 2, FixedAccountId = srbnbAccount.Id }, autoSave: true);

            // Two lines, no per-item override: must still produce exactly one stock debit line.
            var created = await appService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                WarehouseId = warehouse.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new() { ItemId = item.Id, Description = "Line A", Quantity = 4m, UnitPrice = 10m, Uom = "Unit" },
                    new() { ItemId = item.Id, Description = "Line B", Quantity = 6m, UnitPrice = 10m, Uom = "Unit" },
                },
            });

            await appService.SubmitAsync(created.Id);

            var journal = (await journalRepository.GetQueryableAsync())
                .Single(j => j.ReferenceType == "PurchaseReceipt" && j.ReferenceId == created.Id);
            var debitStockLines = journal.Lines.Where(l => l.AccountId == stockAccount.Id && l.IsDebit).ToList();

            debitStockLines.Count.ShouldBe(1);
            debitStockLines[0].Amount.ShouldBe(100m);
        });
    }

    [Fact]
    public async Task CreateAsync_RejectsItemWarehouseFromAnotherCompany()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var appService = GetRequiredService<IPurchaseReceiptAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PW Scope Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PW Other Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Scope Supplier"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PWS-3", "Scope Item", ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Scope Main"), autoSave: true);
            var foreignWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), otherCompany.Id, "Foreign Warehouse"), autoSave: true);
            await seriesRepository.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "PR Series Scope", "PurchaseReceipt", "PWSC-"), autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => appService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                WarehouseId = warehouse.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new() { ItemId = item.Id, Description = "Foreign", Quantity = 1m, UnitPrice = 1m, Uom = "Unit", WarehouseId = foreignWarehouse.Id },
                },
            }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PutawayRuleWarehouseCompanyMismatch);
        });
    }
}
