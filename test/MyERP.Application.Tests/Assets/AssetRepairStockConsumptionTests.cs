using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Shared;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

public abstract class AssetRepairStockConsumptionTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private sealed record Fixture(
        Company Company,
        Item FifoItem,
        Warehouse Warehouse,
        Account StockAccount,
        Account FixedAssetAccount,
        Asset Asset);

    private async Task<Fixture> SeedFifoFixtureAsync(string tag)
    {
        var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
        var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
        var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
        var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
        var categoryRepo = GetRequiredService<IRepository<AssetCategory, Guid>>();
        var assetRepo = GetRequiredService<IRepository<Asset, Guid>>();
        var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var binRepo = GetRequiredService<IRepository<Bin, Guid>>();

        var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), $"AR Stock Co {tag}"), autoSave: true);
        await fyRepo.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, $"FY-AR-{tag}", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
            autoSave: true);
        await seriesRepo.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, $"JE Series AR {tag}", "JE", $"JEAR{tag}-"),
            autoSave: true);

        var stockAccount = await accountRepo.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"14AR-{tag}", "Spare Parts Inventory", AccountType.Asset), autoSave: true);
        var fixedAssetAccount = await accountRepo.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"15AR-{tag}", "Machinery Fixed Asset", AccountType.Asset), autoSave: true);
        var expenseAccount = await accountRepo.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"59AR-{tag}", "Repair Expense", AccountType.Expense), autoSave: true);

        company.DefaultExpenseAccountId = expenseAccount.Id;
        await companyRepo.UpdateAsync(company, autoSave: true);

        var warehouse = await warehouseRepo.InsertAsync(
            new Warehouse(Guid.NewGuid(), company.Id, $"Spare Parts WH {tag}") { DefaultAccountId = stockAccount.Id },
            autoSave: true);

        var item = await itemRepo.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, $"PART-{tag}", $"Spare Part {tag}", ItemType.Goods)
            {
                MaintainStock = true,
                ValuationMethod = ValuationMethod.FIFO,
                StandardBuyingPrice = 12m,
            }, autoSave: true);

        // Layer 1: 2 units @ 10 on Day -3
        var dayMinus3 = DateTime.UtcNow.Date.AddDays(-3);
        await sleRepo.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, warehouse.Id,
            dayMinus3, quantityChange: 2m, valuationRate: 10m,
            balanceQuantity: 2m, balanceValue: 20m)
        {
            StockQueue = "[[2,10]]",
            StockValueDifference = 20m,
        }, autoSave: true);

        // Layer 2: 3 units @ 20 on Day -1 -> queue [[2,10],[3,20]], balance 5 qty / 80 value
        var dayMinus1 = DateTime.UtcNow.Date.AddDays(-1);
        await sleRepo.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, warehouse.Id,
            dayMinus1, quantityChange: 3m, valuationRate: 20m,
            balanceQuantity: 5m, balanceValue: 80m)
        {
            StockQueue = "[[2,10],[3,20]]",
            StockValueDifference = 60m,
        }, autoSave: true);

        var bin = new Bin(Guid.NewGuid(), item.Id, warehouse.Id);
        bin.UpdateActualQty(5m, 80m);
        await binRepo.InsertAsync(bin, autoSave: true);

        var category = new AssetCategory(Guid.NewGuid(), $"Machines {tag}");
        category.AddAccount(Guid.NewGuid(), company.Id, fixedAssetAccount.Id);
        await categoryRepo.InsertAsync(category, autoSave: true);

        var asset = new Asset(Guid.NewGuid(), company.Id, $"AST-STK-{tag}", $"Machine {tag}", DateTime.UtcNow.Date.AddDays(-30), 1000m)
        {
            AssetCategoryId = category.Id,
        };
        asset.Submit();
        await assetRepo.InsertAsync(asset, autoSave: true);

        return new Fixture(company, item, warehouse, stockAccount, fixedAssetAccount, asset);
    }

    [Fact]
    public async Task GetConsumedItemValuationRateAsync_FifoMultiLayer_ReturnsLayerWeightedOutgoingRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedFifoFixtureAsync("RATE");
            var repairService = GetRequiredService<IAssetRepairAppService>();

            // Consuming 2 units pulls purely from oldest layer (2 @ 10), not latest receipt rate (20)
            var rateFor2 = await repairService.GetConsumedItemValuationRateAsync(new GetAssetRepairConsumedItemRateInput
            {
                ItemId = f.FifoItem.Id,
                WarehouseId = f.Warehouse.Id,
                Qty = 2m,
                PostingDate = DateTime.UtcNow.Date,
            });
            rateFor2.ShouldBe(10m);

            // Consuming 4 units spans layers: (2*10 + 2*20) / 4 = 15
            var rateFor4 = await repairService.GetConsumedItemValuationRateAsync(new GetAssetRepairConsumedItemRateInput
            {
                ItemId = f.FifoItem.Id,
                WarehouseId = f.Warehouse.Id,
                Qty = 4m,
                PostingDate = DateTime.UtcNow.Date,
            });
            rateFor4.ShouldBe(15m);

            // As of Day -2 (before Layer 2 arrived), only Layer 1 exists
            var rateBeforeLayer2 = await repairService.GetConsumedItemValuationRateAsync(new GetAssetRepairConsumedItemRateInput
            {
                ItemId = f.FifoItem.Id,
                WarehouseId = f.Warehouse.Id,
                Qty = 2m,
                PostingDate = DateTime.UtcNow.Date.AddDays(-2),
            });
            rateBeforeLayer2.ShouldBe(10m);
        });
    }

    [Fact]
    public async Task CompleteAndCancelAsync_WithStockItems_ConsumesAndReversesFifoStockAssetValueAndGl()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedFifoFixtureAsync("COMP");
            var repairService = GetRequiredService<IAssetRepairAppService>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var binRepo = GetRequiredService<IRepository<Bin, Guid>>();
            var assetRepo = GetRequiredService<IRepository<Asset, Guid>>();
            var journalAppService = GetRequiredService<IJournalEntryAppService>();

            // Enter stale manual rate 999 to prove CompleteAsync overwrites with actual FIFO outgoing rate (15)
            var created = await repairService.CreateAsync(new CreateUpdateAssetRepairDto
            {
                CompanyId = f.Company.Id,
                AssetId = f.Asset.Id,
                FailureDate = DateTime.UtcNow.Date.AddDays(-1),
                CompletionDate = DateTime.UtcNow.Date,
                CapitalizeRepairCost = false,
                StockItems = new List<CreateUpdateAssetRepairConsumedItemDto>
                {
                    new()
                    {
                        ItemId = f.FifoItem.Id,
                        ItemName = f.FifoItem.ItemName,
                        WarehouseId = f.Warehouse.Id,
                        Qty = 4m,
                        ValuationRate = 999m,
                    }
                }
            });

            var completed = await repairService.CompleteAsync(created.Id);
            completed.Status.ShouldBe(AssetRepairStatus.Completed);
            completed.StockItems.Count.ShouldBe(1);
            completed.StockItems[0].ValuationRate.ShouldBe(15m);
            completed.ConsumedItemsCost.ShouldBe(60m);
            completed.TotalRepairCost.ShouldBe(60m);

            // SLE reduced from 5 to 1 (value 20)
            var latestSle = (await sleRepo.GetListAsync(s => s.ItemId == f.FifoItem.Id && s.WarehouseId == f.Warehouse.Id))
                .OrderByDescending(s => s.PostingDateTime)
                .ThenByDescending(s => s.CreationTime)
                .First();
            latestSle.VoucherType.ShouldBe("AssetRepair");
            latestSle.VoucherId.ShouldBe(created.Id);
            latestSle.BalanceQuantity.ShouldBe(1m);
            latestSle.BalanceValue.ShouldBe(20m);

            // Bin synced to 1
            var bin = (await binRepo.GetListAsync(b => b.ItemId == f.FifoItem.Id && b.WarehouseId == f.Warehouse.Id)).Single();
            bin.ActualQty.ShouldBe(1m);

            // Asset value increased by true FIFO consumed cost (60)
            var assetAfterComplete = await assetRepo.GetAsync(f.Asset.Id);
            assetAfterComplete.ValueAfterDepreciation.ShouldBe(1060m);

            // Balanced GL posted: DR Fixed Asset 60, CR Stock Account 60
            var journals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto
            {
                CompanyId = f.Company.Id,
                MaxResultCount = 50,
            });
            var repairJe = journals.Items.Single(j => j.ReferenceType == "AssetRepair" && j.ReferenceId == created.Id);
            repairJe.TotalDebit.ShouldBe(60m);
            repairJe.TotalCredit.ShouldBe(60m);
            repairJe.Lines.ShouldContain(l => l.AccountId == f.FixedAssetAccount.Id && l.IsDebit && l.Amount == 60m);
            repairJe.Lines.ShouldContain(l => l.AccountId == f.StockAccount.Id && !l.IsDebit && l.Amount == 60m);

            // Cancel reverses stock, asset capitalization, and GL
            var cancelled = await repairService.CancelAsync(created.Id);
            cancelled.Status.ShouldBe(AssetRepairStatus.Cancelled);

            var sleAfterCancel = (await sleRepo.GetListAsync(s => s.ItemId == f.FifoItem.Id && s.WarehouseId == f.Warehouse.Id))
                .OrderByDescending(s => s.PostingDateTime)
                .ThenByDescending(s => s.CreationTime)
                .First();
            sleAfterCancel.BalanceQuantity.ShouldBe(5m);

            var binAfterCancel = (await binRepo.GetListAsync(b => b.ItemId == f.FifoItem.Id && b.WarehouseId == f.Warehouse.Id)).Single();
            binAfterCancel.ActualQty.ShouldBe(5m);

            var assetAfterCancel = await assetRepo.GetAsync(f.Asset.Id);
            assetAfterCancel.ValueAfterDepreciation.ShouldBe(1000m);

            var journalsAfterCancel = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto
            {
                CompanyId = f.Company.Id,
                MaxResultCount = 50,
            });
            journalsAfterCancel.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
        });
    }

    [Fact]
    public async Task CancelAsync_PendingRepair_DoesNotAlterAssetValueOrStock()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedFifoFixtureAsync("PEND");
            var repairService = GetRequiredService<IAssetRepairAppService>();
            var assetRepo = GetRequiredService<IRepository<Asset, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();

            var created = await repairService.CreateAsync(new CreateUpdateAssetRepairDto
            {
                CompanyId = f.Company.Id,
                AssetId = f.Asset.Id,
                FailureDate = DateTime.UtcNow.Date.AddDays(-1),
                RepairCost = 200m,
                CapitalizeRepairCost = true,
                IncreaseInAssetLife = 6,
                StockItems = new List<CreateUpdateAssetRepairConsumedItemDto>
                {
                    new()
                    {
                        ItemId = f.FifoItem.Id,
                        ItemName = f.FifoItem.ItemName,
                        WarehouseId = f.Warehouse.Id,
                        Qty = 2m,
                        ValuationRate = 10m,
                    }
                }
            });

            var cancelled = await repairService.CancelAsync(created.Id);
            cancelled.Status.ShouldBe(AssetRepairStatus.Cancelled);

            // Pending repair never capitalized onto asset or consumed stock, so cancel must leave both untouched
            var asset = await assetRepo.GetAsync(f.Asset.Id);
            asset.ValueAfterDepreciation.ShouldBe(1000m);

            var sles = await sleRepo.GetListAsync(s => s.ItemId == f.FifoItem.Id && s.WarehouseId == f.Warehouse.Id);
            sles.Count.ShouldBe(2);
        });
    }

    [Fact]
    public async Task CreateAsync_StockItemMissingWarehouseOrZeroQty_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedFifoFixtureAsync("VAL");
            var repairService = GetRequiredService<IAssetRepairAppService>();

            await Should.ThrowAsync<BusinessException>(() =>
                repairService.CreateAsync(new CreateUpdateAssetRepairDto
                {
                    CompanyId = f.Company.Id,
                    AssetId = f.Asset.Id,
                    FailureDate = DateTime.UtcNow.Date,
                    StockItems = new List<CreateUpdateAssetRepairConsumedItemDto>
                    {
                        new() { ItemId = f.FifoItem.Id, ItemName = f.FifoItem.ItemName, WarehouseId = null, Qty = 1m, ValuationRate = 10m }
                    }
                }));

            await Should.ThrowAsync<BusinessException>(() =>
                repairService.CreateAsync(new CreateUpdateAssetRepairDto
                {
                    CompanyId = f.Company.Id,
                    AssetId = f.Asset.Id,
                    FailureDate = DateTime.UtcNow.Date,
                    StockItems = new List<CreateUpdateAssetRepairConsumedItemDto>
                    {
                        new() { ItemId = f.FifoItem.Id, ItemName = f.FifoItem.ItemName, WarehouseId = f.Warehouse.Id, Qty = 0m, ValuationRate = 10m }
                    }
                }));
        });
    }
}
