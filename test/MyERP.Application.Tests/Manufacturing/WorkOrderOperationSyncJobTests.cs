using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.BackgroundJobs;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Verifies WorkOrderOperationSyncJob correctly records production, absorbs BOM operating cost,
/// and sets isFinishedItem on the manufactured Stock Entry.
/// Per ERPNext PR #59804 / commit 27a344c438 & PR #60595 / commit da8343617a.
/// </summary>
public abstract class WorkOrderOperationSyncJobTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ExecuteAsync_WhenJobCardsCompleted_CreatesManufactureEntryWithOperatingCostAndUpdatesWorkOrder()
    {
        Guid companyId = default, fgItemId = default, rmItemId = default, woId = default, sourceWhId = default, fgWhId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var (company, fgItem, rmItem, wo, jc, sourceWh, fgWh) = await SeedAsync("SYNC1", forQty: 10m, requiredQty: 20m, unitOperatingCost: 5m);
            companyId = company.Id;
            fgItemId = fgItem.Id;
            rmItemId = rmItem.Id;
            woId = wo.Id;
            sourceWhId = sourceWh;
            fgWhId = fgWh;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var job = GetRequiredService<WorkOrderOperationSyncJob>();
            await job.ExecuteAsync(new WorkOrderOperationSyncJobArgs
            {
                CompanyId = companyId
            });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var reloadedWo = await woRepo.GetAsync(woId);
            reloadedWo.ProducedQuantity.ShouldBe(10m);
            reloadedWo.Status.ShouldBe(WorkOrderStatus.Completed);
            reloadedWo.ActualStartDate.ShouldNotBeNull();
            reloadedWo.ActualEndDate.ShouldNotBeNull();

            var seRepo = GetRequiredService<IRepository<StockEntry, Guid>>();
            var seList = (await seRepo.GetQueryableAsync())
                .Where(s => s.WorkOrderId == woId && s.EntryType == StockEntryType.Manufacture)
                .ToList();

            seList.Count.ShouldBe(1);
            var entry = seList[0];
            entry.FgCompletedQty.ShouldBe(10m);
            entry.TotalAdditionalCosts.ShouldBe(50m); // 10 units * $5 BOM operating cost

            var fgItemRow = entry.Items.SingleOrDefault(i => i.ItemId == fgItemId && i.TargetWarehouseId == fgWhId);
            fgItemRow.ShouldNotBeNull();
            fgItemRow.IsFinishedItem.ShouldBeTrue();
            fgItemRow.Quantity.ShouldBe(10m);
            fgItemRow.AdditionalCost.ShouldBe(50m);
            // RM was 20 units * 10 = 200 raw material cost + 50 operating cost = 250 / 10 = 25 rate
            fgItemRow.ValuationRate.ShouldBe(25m);

            var rmItemRow = entry.Items.SingleOrDefault(i => i.ItemId == rmItemId && i.SourceWarehouseId == sourceWhId);
            rmItemRow.ShouldNotBeNull();
            rmItemRow.Quantity.ShouldBe(20m);
            rmItemRow.ValuationRate.ShouldBe(10m);
        });
    }

    private async Task<(Company Company, Item FgItem, Item RmItem, WorkOrder Wo, JobCard Jc, Guid SourceWh, Guid FgWh)> SeedAsync(
        string suffix, decimal forQty, decimal requiredQty, decimal unitOperatingCost)
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Accounting.Entities.Account, Guid>>();
        var costCenterRepository = GetRequiredService<IRepository<Accounting.Entities.CostCenter, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<Accounting.Entities.FiscalYear, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
        var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
        var jcRepository = GetRequiredService<IRepository<JobCard, Guid>>();
        var stockEntryRepository = GetRequiredService<IRepository<StockEntry, Guid>>();
        var stockPostingService = GetRequiredService<StockPostingService>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), $"Sync Co {suffix}"), autoSave: true);
        var fgItem = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, $"FG-SYNC-{suffix}", $"Finished Good Sync {suffix}", ItemType.Goods), autoSave: true);
        var rmItem = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, $"RM-SYNC-{suffix}", $"Raw Material Sync {suffix}", ItemType.Goods), autoSave: true);

        var sourceWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, $"Source WH {suffix}"), autoSave: true);
        var wipWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, $"WIP WH {suffix}"), autoSave: true);
        var fgWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, $"FG WH {suffix}"), autoSave: true);

        var stockAccount = await accountRepository.InsertAsync(
            new Accounting.Entities.Account(Guid.NewGuid(), company.Id, $"11S{suffix}", "Stock In Hand", Accounting.AccountType.Asset), autoSave: true);
        var expenseAccount = await accountRepository.InsertAsync(
            new Accounting.Entities.Account(Guid.NewGuid(), company.Id, $"51S{suffix}", "Cost of Goods Sold", Accounting.AccountType.Expense), autoSave: true);
        var adjustmentAccount = await accountRepository.InsertAsync(
            new Accounting.Entities.Account(Guid.NewGuid(), company.Id, $"12S{suffix}", "Stock Adjustment", Accounting.AccountType.Equity), autoSave: true);
        var costCenter = await costCenterRepository.InsertAsync(
            new Accounting.Entities.CostCenter(Guid.NewGuid(), company.Id, $"CC Sync {suffix}"), autoSave: true);

        company.DefaultInventoryAccountId = stockAccount.Id;
        company.DefaultExpenseAccountId = expenseAccount.Id;
        company.DefaultStockAdjustmentAccountId = adjustmentAccount.Id;
        company.DefaultCostCenterId = costCenter.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        await fiscalYearRepository.InsertAsync(
            new Accounting.Entities.FiscalYear(Guid.NewGuid(), company.Id, $"FYS-{suffix}", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
            autoSave: true);

        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, $"SE Sync {suffix}", "StockEntry", $"SES{suffix}-"), autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, $"SE Short Sync {suffix}", "SE", $"SES2{suffix}-"), autoSave: true);

        // Seed RM into source warehouse (100 units @ $10)
        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var binRepository = GetRequiredService<IRepository<Bin, Guid>>();
        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, rmItem.Id, sourceWarehouse.Id,
            DateTime.UtcNow.Date.AddDays(-1), quantityChange: 100m, valuationRate: 10m,
            balanceQuantity: 100m, balanceValue: 1000m)
        {
            StockQueue = "[[100,10]]",
        }, autoSave: true);
        await binRepository.InsertAsync(new Bin(Guid.NewGuid(), rmItem.Id, sourceWarehouse.Id)
        {
            ActualQty = 100m
        }, autoSave: true);

        var bom = new BillOfMaterials(Guid.NewGuid(), company.Id, $"BOM-SYNC-{suffix}", fgItem.Id)
        {
            Quantity = 1,
            IsActive = true,
            OperatingCost = unitOperatingCost,
            CostAllocationPercentage = 100m,
        };
        await bomRepository.InsertAsync(bom, autoSave: true);

        var wo = new WorkOrder(Guid.NewGuid(), company.Id, $"WO-SYNC-{suffix}", fgItem.Id, bom.Id, quantity: forQty)
        {
            WipWarehouseId = wipWarehouse.Id,
            SourceWarehouseId = sourceWarehouse.Id,
            FgWarehouseId = fgWarehouse.Id,
        };
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItem.Id, rmItem.ItemName, requiredQty)
        {
            SourceWarehouseId = sourceWarehouse.Id,
        });
        wo.Submit();
        wo.Start();
        await woRepository.InsertAsync(wo, autoSave: true);

        var jc = new JobCard(Guid.NewGuid(), company.Id, wo.Id, Guid.NewGuid(), forQuantity: forQty, sequenceId: 1)
        {
            WipWarehouseId = wipWarehouse.Id,
        };
        jc.Start();
        jc.AddTimeLog(DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), forQty);
        jc.Complete();
        await jcRepository.InsertAsync(jc, autoSave: true);

        return (company, fgItem, rmItem, wo, jc, sourceWarehouse.Id, fgWarehouse.Id);
    }
}
