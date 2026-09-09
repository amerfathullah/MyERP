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
/// Coverage for a Purchase Invoice that moves stock itself (update_stock — a direct purchase with
/// no Purchase Receipt).
///
/// The seeded PI rules debit ItemExpense for the whole net total, and nothing branched on
/// update_stock. So an invoice that created real StockLedgerEntry and Bin rows expensed the cost
/// immediately: inventory value never appeared on the books, and the same cost was recognised twice
/// — once here, again as COGS when the stock was later sold. ERPNext's PI gl_composer debits the
/// warehouse's inventory account for stock items and leaves only non-stock lines on expense.
/// </summary>
public abstract class PurchaseInvoiceUpdateStockGlTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private sealed record Fixture(
        Company Company,
        Supplier Supplier,
        Account StockAccount,
        Account SecondStockAccount,
        Account ExpenseAccount,
        Account PayableAccount,
        Warehouse Warehouse,
        Warehouse SecondWarehouse);

    private async Task<Fixture> SeedAsync(string tag)
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
        var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        var warehouseAccountRepository = GetRequiredService<IRepository<WarehouseAccount, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var ruleRepository = GetRequiredService<IRepository<AccountingRule, Guid>>();
        var costCenterRepository = GetRequiredService<IRepository<CostCenter, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), $"PI Stock Co {tag}"), autoSave: true);
        var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, $"PI Stock Supplier {tag}"), autoSave: true);

        var warehouse = await warehouseRepository.InsertAsync(
            new Warehouse(Guid.NewGuid(), company.Id, $"PI Main {tag}"), autoSave: true);
        var secondWarehouse = await warehouseRepository.InsertAsync(
            new Warehouse(Guid.NewGuid(), company.Id, $"PI Overflow {tag}"), autoSave: true);

        var stockAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"1410-{tag}", "Stock In Hand", AccountType.Asset), autoSave: true);
        var secondStockAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"1411-{tag}", "Stock In Hand 2", AccountType.Asset), autoSave: true);
        var expenseAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"5010-{tag}", "Purchase Expense", AccountType.Expense), autoSave: true);
        var payableAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"2010-{tag}", "Creditors", AccountType.Liability), autoSave: true);

        await warehouseAccountRepository.InsertAsync(
            new WarehouseAccount(Guid.NewGuid(), warehouse.Id, company.Id, stockAccount.Id), autoSave: true);
        await warehouseAccountRepository.InsertAsync(
            new WarehouseAccount(Guid.NewGuid(), secondWarehouse.Id, company.Id, secondStockAccount.Id), autoSave: true);

        var costCenter = await costCenterRepository.InsertAsync(
            new CostCenter(Guid.NewGuid(), company.Id, $"PI Cost Center {tag}"), autoSave: true);
        company.DefaultInventoryAccountId = stockAccount.Id;
        company.DefaultExpenseAccountId = expenseAccount.Id;
        company.DefaultPayableAccountId = payableAccount.Id;
        company.DefaultCostCenterId = costCenter.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        await fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, $"FY {tag}", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
            autoSave: true);
        await seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, $"PI Series {tag}", "PurchaseInvoice", $"PIS{tag}-"), autoSave: true);

        // The rules a real company is seeded with: expense debit, payable credit.
        await ruleRepository.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, "PI DR Expense", "PurchaseInvoice", true, AccountSource.ItemExpense, AmountSource.NetTotal)
            { SortOrder = 1 }, autoSave: true);
        await ruleRepository.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, "PI CR Payable", "PurchaseInvoice", false, AccountSource.SupplierPayable, AmountSource.GrandTotal)
            { SortOrder = 2 }, autoSave: true);

        return new Fixture(company, supplier, stockAccount, secondStockAccount, expenseAccount, payableAccount, warehouse, secondWarehouse);
    }

    [Fact]
    public async Task UpdateStockInvoice_DebitsInventoryAccount_NotExpense()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("A");
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var journalRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var appService = GetRequiredService<IPurchaseInvoiceAppService>();

            var stockItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), f.Company.Id, "PIS-A1", "Stock Item", ItemType.Goods), autoSave: true);

            var created = await appService.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = f.Company.Id,
                SupplierId = f.Supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(30),
                UpdateStock = true,
                WarehouseId = f.Warehouse.Id,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = stockItem.Id, Description = "Stock Item", Quantity = 10m, UnitPrice = 8m, Uom = "Unit" },
                },
            });

            await appService.SubmitAsync(created.Id);
            await appService.PostAsync(created.Id);

            var journal = (await journalRepository.GetQueryableAsync())
                .Single(j => j.ReferenceType == "PurchaseInvoice" && j.ReferenceId == created.Id);
            var lines = journal.Lines.ToList();

            lines.Sum(l => l.IsDebit ? l.Amount : 0m).ShouldBe(lines.Sum(l => !l.IsDebit ? l.Amount : 0m));

            // The goods are in a warehouse, so the debit belongs on inventory, not expense.
            lines.Single(l => l.AccountId == f.StockAccount.Id && l.IsDebit).Amount.ShouldBe(80m);
            lines.Any(l => l.AccountId == f.ExpenseAccount.Id).ShouldBeFalse();
        });
    }

    [Fact]
    public async Task UpdateStockInvoice_LeavesServiceItemsOnExpense()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("B");
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var journalRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var appService = GetRequiredService<IPurchaseInvoiceAppService>();

            var stockItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), f.Company.Id, "PIS-B1", "Stock Item", ItemType.Goods), autoSave: true);
            var serviceItem = new Item(Guid.NewGuid(), f.Company.Id, "PIS-B2", "Freight Service", ItemType.Service)
            {
                MaintainStock = false,
            };
            await itemRepository.InsertAsync(serviceItem, autoSave: true);

            var created = await appService.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = f.Company.Id,
                SupplierId = f.Supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(30),
                UpdateStock = true,
                WarehouseId = f.Warehouse.Id,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = stockItem.Id, Description = "Stock Item", Quantity = 10m, UnitPrice = 8m, Uom = "Unit" },
                    new() { ItemId = serviceItem.Id, Description = "Freight", Quantity = 1m, UnitPrice = 25m, Uom = "Unit" },
                },
            });

            await appService.SubmitAsync(created.Id);
            await appService.PostAsync(created.Id);

            var journal = (await journalRepository.GetQueryableAsync())
                .Single(j => j.ReferenceType == "PurchaseInvoice" && j.ReferenceId == created.Id);
            var lines = journal.Lines.ToList();

            lines.Sum(l => l.IsDebit ? l.Amount : 0m).ShouldBe(lines.Sum(l => !l.IsDebit ? l.Amount : 0m));

            // Goods to inventory, freight stays expensed — the split ERPNext's composer makes.
            lines.Single(l => l.AccountId == f.StockAccount.Id && l.IsDebit).Amount.ShouldBe(80m);
            lines.Single(l => l.AccountId == f.ExpenseAccount.Id && l.IsDebit).Amount.ShouldBe(25m);
        });
    }

    [Fact]
    public async Task UpdateStockInvoice_SplitAcrossWarehouses_MovesStockAndSplitsGl()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("C");
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var binRepository = GetRequiredService<IRepository<Bin, Guid>>();
            var journalRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var appService = GetRequiredService<IPurchaseInvoiceAppService>();

            var stockItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), f.Company.Id, "PIS-C1", "Stock Item", ItemType.Goods), autoSave: true);

            var created = await appService.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = f.Company.Id,
                SupplierId = f.Supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(30),
                UpdateStock = true,
                WarehouseId = f.Warehouse.Id,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = stockItem.Id, Description = "Main", Quantity = 30m, UnitPrice = 5m, Uom = "Unit", WarehouseId = f.Warehouse.Id },
                    new() { ItemId = stockItem.Id, Description = "Overflow", Quantity = 10m, UnitPrice = 5m, Uom = "Unit", WarehouseId = f.SecondWarehouse.Id },
                },
            });

            await appService.SubmitAsync(created.Id);
            await appService.PostAsync(created.Id);

            var bins = (await binRepository.GetQueryableAsync()).Where(b => b.ItemId == stockItem.Id).ToList();
            bins.Single(b => b.WarehouseId == f.Warehouse.Id).ActualQty.ShouldBe(30m);
            bins.Single(b => b.WarehouseId == f.SecondWarehouse.Id).ActualQty.ShouldBe(10m);

            var journal = (await journalRepository.GetQueryableAsync())
                .Single(j => j.ReferenceType == "PurchaseInvoice" && j.ReferenceId == created.Id);
            var lines = journal.Lines.ToList();

            lines.Sum(l => l.IsDebit ? l.Amount : 0m).ShouldBe(lines.Sum(l => !l.IsDebit ? l.Amount : 0m));
            lines.Single(l => l.AccountId == f.StockAccount.Id && l.IsDebit).Amount.ShouldBe(150m);
            lines.Single(l => l.AccountId == f.SecondStockAccount.Id && l.IsDebit).Amount.ShouldBe(50m);
        });
    }

    [Fact]
    public async Task PlainInvoice_WithoutUpdateStock_StillDebitsExpense()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("D");
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var journalRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var appService = GetRequiredService<IPurchaseInvoiceAppService>();

            var stockItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), f.Company.Id, "PIS-D1", "Stock Item", ItemType.Goods), autoSave: true);

            var created = await appService.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = f.Company.Id,
                SupplierId = f.Supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(30),
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = stockItem.Id, Description = "Stock Item", Quantity = 10m, UnitPrice = 8m, Uom = "Unit" },
                },
            });

            await appService.SubmitAsync(created.Id);
            await appService.PostAsync(created.Id);

            var journal = (await journalRepository.GetQueryableAsync())
                .Single(j => j.ReferenceType == "PurchaseInvoice" && j.ReferenceId == created.Id);
            var lines = journal.Lines.ToList();

            // No stock moved, so nothing is carved out — the ordinary expense posting is untouched.
            lines.Single(l => l.AccountId == f.ExpenseAccount.Id && l.IsDebit).Amount.ShouldBe(80m);
            lines.Any(l => l.AccountId == f.StockAccount.Id).ShouldBeFalse();
        });
    }
}
