using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Coverage for internal transfer Purchase Receipts:
/// Deduct rejected qty from the in-transit warehouse alongside accepted qty (ERPNext PR #59251 / commit c0f13b01de &amp; PR #59256 / commit df3f952fac).
/// Rejected material carries stock value anchored to transfer rate.
/// Cancellation reverses all warehouse movements cleanly.
/// </summary>
public abstract class PurchaseReceiptInternalTransferTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubmitAsync_InternalTransferWithRejectedQty_DeductsBothFromInTransitAndValuesRejectedMaterial()
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
            var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var valuationService = GetRequiredService<StockValuationService>();
            var binService = GetRequiredService<BinService>();
            var appService = GetRequiredService<IPurchaseReceiptAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Transit Transfer Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Internal Supplier"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "TT-ITEM-1", "Transfer Item 1", ItemType.Goods), autoSave: true);

            var transitWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "In-Transit WH"), autoSave: true);
            var targetWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Target Stores WH"), autoSave: true);
            var rejectedWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Rejected WH"), autoSave: true);

            var transitStockAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1410-TT", "Stock - Transit", AccountType.Asset), autoSave: true);
            var targetStockAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1411-TT", "Stock - Target", AccountType.Asset), autoSave: true);
            var rejectedStockAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1412-TT", "Stock - Rejected", AccountType.Asset), autoSave: true);
            var srbnbAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "2110-TT", "Stock Received But Not Billed", AccountType.Liability), autoSave: true);

            await warehouseAccountRepository.InsertAsync(
                new WarehouseAccount(Guid.NewGuid(), transitWarehouse.Id, company.Id, transitStockAccount.Id), autoSave: true);
            await warehouseAccountRepository.InsertAsync(
                new WarehouseAccount(Guid.NewGuid(), targetWarehouse.Id, company.Id, targetStockAccount.Id), autoSave: true);
            await warehouseAccountRepository.InsertAsync(
                new WarehouseAccount(Guid.NewGuid(), rejectedWarehouse.Id, company.Id, rejectedStockAccount.Id), autoSave: true);

            var costCenter = await costCenterRepository.InsertAsync(
                new CostCenter(Guid.NewGuid(), company.Id, "Transfer Cost Center"), autoSave: true);
            company.DefaultInventoryAccountId = targetStockAccount.Id;
            company.StockReceivedButNotBilledAccountId = srbnbAccount.Id;
            company.DefaultCostCenterId = costCenter.Id;
            await companyRepository.UpdateAsync(company, autoSave: true);

            await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY Transit", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
                autoSave: true);
            await seriesRepository.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "PR Series", "PurchaseReceipt", "PR-TT-"), autoSave: true);

            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PR DR Stock", "PurchaseReceipt", true, AccountSource.WarehouseStock, AmountSource.NetTotal)
                { SortOrder = 1, FixedAccountId = targetStockAccount.Id }, autoSave: true);
            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PR CR SRBNB", "PurchaseReceipt", false, AccountSource.FixedAccount, AmountSource.NetTotal)
                { SortOrder = 2, FixedAccountId = srbnbAccount.Id }, autoSave: true);

            // Pre-seed in-transit warehouse with 10 units at rate 100 via StockValuationService and BinService
            await valuationService.CreateLedgerEntryAsync(
                company.Id, item.Id, transitWarehouse.Id,
                DateTime.UtcNow.Date.AddDays(-1), 10m, 100m,
                "StockEntry", Guid.NewGuid(), null);
            await binService.ApplyStockMovementAsync(
                item.Id, transitWarehouse.Id, 10m, 1000m, null);

            // PR for internal transfer: 7 accepted into target, 3 rejected into rejected WH, sourced from in-transit WH
            var created = await appService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                WarehouseId = targetWarehouse.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new()
                    {
                        ItemId = item.Id,
                        Description = "Transfer Line",
                        Quantity = 7m,
                        UnitPrice = 100m,
                        Uom = "Unit",
                        WarehouseId = targetWarehouse.Id,
                        FromWarehouseId = transitWarehouse.Id,
                        RejectedQty = 3m,
                        RejectedWarehouseId = rejectedWarehouse.Id
                    }
                }
            });

            await appService.SubmitAsync(created.Id);

            // In-transit warehouse had 10, now deducted 10 (7 accepted + 3 rejected) -> 0
            var transitBin = (await binRepository.GetQueryableAsync()).Single(b => b.ItemId == item.Id && b.WarehouseId == transitWarehouse.Id);
            transitBin.ActualQty.ShouldBe(0m);
            transitBin.StockValue.ShouldBe(0m);

            // Target warehouse received 7 accepted
            var targetBin = (await binRepository.GetQueryableAsync()).Single(b => b.ItemId == item.Id && b.WarehouseId == targetWarehouse.Id);
            targetBin.ActualQty.ShouldBe(7m);
            targetBin.StockValue.ShouldBe(700m);

            // Rejected warehouse received 3 rejected valued at transfer rate (100 each = 300)
            var rejectedBin = (await binRepository.GetQueryableAsync()).Single(b => b.ItemId == item.Id && b.WarehouseId == rejectedWarehouse.Id);
            rejectedBin.ActualQty.ShouldBe(3m);
            rejectedBin.StockValue.ShouldBe(300m);

            // Verify Stock Ledger Entries
            var sles = (await sleRepository.GetQueryableAsync())
                .Where(s => s.VoucherType == "PurchaseReceipt" && s.VoucherId == created.Id)
                .ToList();

            // Transit warehouse SLE: -10 qty
            var transitSle = sles.Single(s => s.WarehouseId == transitWarehouse.Id);
            transitSle.QuantityChange.ShouldBe(-10m);
            transitSle.ValuationRate.ShouldBe(100m);

            // Target warehouse SLE: +7 qty
            var targetSle = sles.Single(s => s.WarehouseId == targetWarehouse.Id);
            targetSle.QuantityChange.ShouldBe(7m);
            targetSle.ValuationRate.ShouldBe(100m);

            // Rejected warehouse SLE: +3 qty at 100m incoming rate
            var rejectedSle = sles.Single(s => s.WarehouseId == rejectedWarehouse.Id);
            rejectedSle.QuantityChange.ShouldBe(3m);
            rejectedSle.ValuationRate.ShouldBe(100m);

            // Cancel PR and verify all movements are reversed
            await appService.CancelAsync(created.Id);

            transitBin = (await binRepository.GetQueryableAsync()).Single(b => b.ItemId == item.Id && b.WarehouseId == transitWarehouse.Id);
            transitBin.ActualQty.ShouldBe(10m);
            transitBin.StockValue.ShouldBe(1000m);

            targetBin = (await binRepository.GetQueryableAsync()).Single(b => b.ItemId == item.Id && b.WarehouseId == targetWarehouse.Id);
            targetBin.ActualQty.ShouldBe(0m);
            targetBin.StockValue.ShouldBe(0m);

            rejectedBin = (await binRepository.GetQueryableAsync()).Single(b => b.ItemId == item.Id && b.WarehouseId == rejectedWarehouse.Id);
            rejectedBin.ActualQty.ShouldBe(0m);
            rejectedBin.StockValue.ShouldBe(0m);
        });
    }

    [Fact]
    public async Task SubmitAsync_InternalTransferReturn_RestoresInTransitWarehouse()
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
            var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var valuationService = GetRequiredService<StockValuationService>();
            var binService = GetRequiredService<BinService>();
            var appService = GetRequiredService<IPurchaseReceiptAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Transit Return Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Return Supplier"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "TT-ITEM-2", "Transfer Item 2", ItemType.Goods), autoSave: true);

            var transitWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "In-Transit Return WH"), autoSave: true);
            var targetWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Target Return WH"), autoSave: true);
            var rejectedWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Rejected Return WH"), autoSave: true);

            var targetStockAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1411-TR", "Stock - Target", AccountType.Asset), autoSave: true);
            var srbnbAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "2110-TR", "SRBNB", AccountType.Liability), autoSave: true);

            var costCenter = await costCenterRepository.InsertAsync(
                new CostCenter(Guid.NewGuid(), company.Id, "Return Cost Center"), autoSave: true);
            company.DefaultInventoryAccountId = targetStockAccount.Id;
            company.StockReceivedButNotBilledAccountId = srbnbAccount.Id;
            company.DefaultCostCenterId = costCenter.Id;
            await companyRepository.UpdateAsync(company, autoSave: true);

            await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY Return", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
                autoSave: true);
            await seriesRepository.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "PR Series", "PurchaseReceipt", "PR-TR-"), autoSave: true);

            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PR DR Stock", "PurchaseReceipt", true, AccountSource.WarehouseStock, AmountSource.NetTotal)
                { SortOrder = 1, FixedAccountId = targetStockAccount.Id }, autoSave: true);
            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PR CR SRBNB", "PurchaseReceipt", false, AccountSource.FixedAccount, AmountSource.NetTotal)
                { SortOrder = 2, FixedAccountId = srbnbAccount.Id }, autoSave: true);

            // Pre-seed in-transit warehouse with 10 units at rate 50 via StockValuationService and BinService
            await valuationService.CreateLedgerEntryAsync(
                company.Id, item.Id, transitWarehouse.Id,
                DateTime.UtcNow.Date.AddDays(-1), 10m, 50m,
                "StockEntry", Guid.NewGuid(), null);
            await binService.ApplyStockMovementAsync(
                item.Id, transitWarehouse.Id, 10m, 500m, null);

            // Original PR: 8 accepted into target, 2 rejected, sourced from transit
            var original = await appService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                WarehouseId = targetWarehouse.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new()
                    {
                        ItemId = item.Id,
                        Description = "Original Transfer",
                        Quantity = 8m,
                        UnitPrice = 50m,
                        Uom = "Unit",
                        WarehouseId = targetWarehouse.Id,
                        FromWarehouseId = transitWarehouse.Id,
                        RejectedQty = 2m,
                        RejectedWarehouseId = rejectedWarehouse.Id
                    }
                }
            });
            await appService.SubmitAsync(original.Id);

            // In-transit WH is now 0. Target is 8, rejected is 2.
            var transitBin = (await binRepository.GetQueryableAsync()).Single(b => b.ItemId == item.Id && b.WarehouseId == transitWarehouse.Id);
            transitBin.ActualQty.ShouldBe(0m);

            // Create Return PR: return 8 accepted and 2 rejected against original
            var returnPr = await appService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                WarehouseId = targetWarehouse.Id,
                PostingDate = DateTime.UtcNow.Date,
                IsReturn = true,
                ReturnAgainstId = original.Id,
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new()
                    {
                        ItemId = item.Id,
                        Description = "Return Transfer",
                        Quantity = -8m,
                        UnitPrice = 50m,
                        Uom = "Unit",
                        WarehouseId = targetWarehouse.Id,
                        FromWarehouseId = transitWarehouse.Id,
                        RejectedQty = -2m,
                        RejectedWarehouseId = rejectedWarehouse.Id
                    }
                }
            });
            await appService.SubmitAsync(returnPr.Id);

            // In-transit WH received back the 10 units (8 accepted return + 2 rejected return)
            transitBin = (await binRepository.GetQueryableAsync()).Single(b => b.ItemId == item.Id && b.WarehouseId == transitWarehouse.Id);
            transitBin.ActualQty.ShouldBe(10m);
            transitBin.StockValue.ShouldBe(500m);

            var targetBin = (await binRepository.GetQueryableAsync()).Single(b => b.ItemId == item.Id && b.WarehouseId == targetWarehouse.Id);
            targetBin.ActualQty.ShouldBe(0m);

            var rejectedBin = (await binRepository.GetQueryableAsync()).Single(b => b.ItemId == item.Id && b.WarehouseId == rejectedWarehouse.Id);
            rejectedBin.ActualQty.ShouldBe(0m);

            // Cancel return PR: material taken back out of in-transit WH, restoring target and rejected WHs
            await appService.CancelAsync(returnPr.Id);

            transitBin = (await binRepository.GetQueryableAsync()).Single(b => b.ItemId == item.Id && b.WarehouseId == transitWarehouse.Id);
            transitBin.ActualQty.ShouldBe(0m);

            targetBin = (await binRepository.GetQueryableAsync()).Single(b => b.ItemId == item.Id && b.WarehouseId == targetWarehouse.Id);
            targetBin.ActualQty.ShouldBe(8m);

            rejectedBin = (await binRepository.GetQueryableAsync()).Single(b => b.ItemId == item.Id && b.WarehouseId == rejectedWarehouse.Id);
            rejectedBin.ActualQty.ShouldBe(2m);
        });
    }
}
