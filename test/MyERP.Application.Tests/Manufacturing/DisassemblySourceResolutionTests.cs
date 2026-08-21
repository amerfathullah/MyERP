using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Regression coverage for CreateDisassemblyStockEntryAsync's 3-tier RM source resolution
/// (76th migration session): a real posted Manufacture Stock Entry must win over the WO's
/// planned RequiredItems whenever one exists, since actual consumption can differ from plan.
/// </summary>
public abstract class DisassemblySourceResolutionTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SingleManufactureEntry_AutoSelected_UsesActualConsumedQtyNotPlannedQty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var seRepository = GetRequiredService<IRepository<StockEntry, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.FiscalYear, Guid>>();
            var accountRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
            var accountingRuleRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.AccountingRule, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Disassembly Tier Test Co"), autoSave: true);
            await SeedStockEntryGlRulesAsync(accountRepository, accountingRuleRepository, company.Id, suffix: "1");
            var fgItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-001", "Finished Widget", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-001", "Raw Bolt", ItemType.Goods), autoSave: true);
            var fgWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG Store"), autoSave: true);
            var sourceWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "RM Store"), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series", "SE", "SE-"), autoSave: true);
            await fiscalYearRepository.InsertAsync(
                new MyERP.Accounting.Entities.FiscalYear(Guid.NewGuid(), company.Id, "FY-Test",
                    new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            // WO planned 100 units, requiring 5 bolts/unit → 500 planned. Actual production
            // (via the Manufacture Stock Entry below) only consumed 3 bolts/unit — a real-world
            // process-loss/substitution divergence from plan.
            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-0001", fgItem.Id, Guid.NewGuid(), quantity: 100m)
            {
                FgWarehouseId = fgWarehouse.Id,
                SourceWarehouseId = sourceWarehouse.Id,
            };
            wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItem.Id, "Raw Bolt", requiredQuantity: 500m)
            {
                SourceWarehouseId = sourceWarehouse.Id,
            });
            wo.Submit();
            wo.Start();
            wo.RecordProduction(100m);
            await woRepository.InsertAsync(wo, autoSave: true);

            // The actual Manufacture Stock Entry: 100 FG produced, only 300 RM actually consumed
            // (3/unit, not the planned 5/unit), at a real rate of 2.00/unit.
            var manufactureEntry = new StockEntry(Guid.NewGuid(), company.Id, StockEntryType.Manufacture, DateTime.Today, company.TenantId)
            {
                WorkOrderId = wo.Id,
                EntryNumber = "SE-MFG-0001",
                FgCompletedQty = 100m,
            };
            manufactureEntry.AddItem(rmItem.Id, 300m, sourceWarehouseId: sourceWarehouse.Id, targetWarehouseId: null, valuationRate: 2.00m);
            manufactureEntry.AddItem(fgItem.Id, 100m, sourceWarehouseId: null, targetWarehouseId: fgWarehouse.Id, valuationRate: 6.00m);
            manufactureEntry.Submit();
            manufactureEntry.Post();
            await seRepository.InsertAsync(manufactureEntry, autoSave: true);

            // Act: disassemble 10 of the 100 produced units (10% of actual production).
            var result = await manufacturingAppService.CreateDisassemblyStockEntryAsync(new CreateDisassemblyDto
            {
                WorkOrderId = wo.Id,
                Quantity = 10m,
            });

            var disassembleEntry = await seRepository.GetAsync(result.StockEntryId);
            var rmLine = disassembleEntry.Items.Single(i => i.ItemId == rmItem.Id);

            // Correct (Tier 1, from the real Manufacture SE): 300 × (10/100) = 30, at rate 2.00.
            // Wrong (old behavior, from wo.RequiredItems): 500 × (10/100) = 50.
            rmLine.Quantity.ShouldBe(30m);
            rmLine.ValuationRate.ShouldBe(2.00m);
        });
    }

    [Fact]
    public async Task MultipleManufactureEntries_AggregatesQtyWeightedAverage()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var seRepository = GetRequiredService<IRepository<StockEntry, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.FiscalYear, Guid>>();
            var accountRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
            var accountingRuleRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.AccountingRule, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Disassembly Tier Test Co 2"), autoSave: true);
            await SeedStockEntryGlRulesAsync(accountRepository, accountingRuleRepository, company.Id, suffix: "2");
            var fgItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-002", "Finished Widget 2", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-002", "Raw Screw", ItemType.Goods), autoSave: true);
            var fgWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG Store 2"), autoSave: true);
            var sourceWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "RM Store 2"), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series 2", "SE", "SE2-"), autoSave: true);
            await fiscalYearRepository.InsertAsync(
                new MyERP.Accounting.Entities.FiscalYear(Guid.NewGuid(), company.Id, "FY-Test-2",
                    new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-0002", fgItem.Id, Guid.NewGuid(), quantity: 100m)
            {
                FgWarehouseId = fgWarehouse.Id,
                SourceWarehouseId = sourceWarehouse.Id,
            };
            wo.Submit();
            wo.Start();
            wo.RecordProduction(100m);
            await woRepository.InsertAsync(wo, autoSave: true);

            // Two separate production runs at different costs: 60 units @ rate 3.00, 40 units @ rate 4.50.
            var run1 = new StockEntry(Guid.NewGuid(), company.Id, StockEntryType.Manufacture, DateTime.Today, company.TenantId)
            { WorkOrderId = wo.Id, EntryNumber = "SE-MFG-0002A", FgCompletedQty = 60m };
            run1.AddItem(rmItem.Id, 120m, sourceWarehouseId: sourceWarehouse.Id, targetWarehouseId: null, valuationRate: 3.00m);
            run1.AddItem(fgItem.Id, 60m, sourceWarehouseId: null, targetWarehouseId: fgWarehouse.Id, valuationRate: 6.00m);
            run1.Submit();
            run1.Post();
            await seRepository.InsertAsync(run1, autoSave: true);

            var run2 = new StockEntry(Guid.NewGuid(), company.Id, StockEntryType.Manufacture, DateTime.Today, company.TenantId)
            { WorkOrderId = wo.Id, EntryNumber = "SE-MFG-0002B", FgCompletedQty = 40m };
            run2.AddItem(rmItem.Id, 80m, sourceWarehouseId: sourceWarehouse.Id, targetWarehouseId: null, valuationRate: 4.50m);
            run2.AddItem(fgItem.Id, 40m, sourceWarehouseId: null, targetWarehouseId: fgWarehouse.Id, valuationRate: 6.00m);
            run2.Submit();
            run2.Post();
            await seRepository.InsertAsync(run2, autoSave: true);

            // Act: disassemble 20 of the 100 total produced (20% of the aggregated 100 FG qty).
            var result = await manufacturingAppService.CreateDisassemblyStockEntryAsync(new CreateDisassemblyDto
            {
                WorkOrderId = wo.Id,
                Quantity = 20m,
            });

            var disassembleEntry = await seRepository.GetAsync(result.StockEntryId);
            var rmLine = disassembleEntry.Items.Single(i => i.ItemId == rmItem.Id);

            // Aggregated RM qty = 120 + 80 = 200, scale factor = 20/100 = 0.2 → 40.
            // Qty-weighted avg rate = (120×3.00 + 80×4.50) / 200 = (360+360)/200 = 3.60.
            rmLine.Quantity.ShouldBe(40m);
            rmLine.ValuationRate.ShouldBe(3.60m);
        });
    }

    /// <summary>
    /// Test-only GL scaffolding: DefaultDataSeeder seeds no "StockEntry" AccountingRule rows at
    /// all (Stock Entry GL is left to per-company configuration since account resolution varies
    /// by purpose) — without SOME rule pair, DocumentPostingOrchestrator.PostStockEntryAsync
    /// throws "no rules configured" before this test ever reaches the RM-item resolution logic
    /// under test. Both rules use AmountSource.GrandTotal so the resulting JE is trivially
    /// balanced regardless of which dummy accounts are used — this is not asserting anything
    /// about Stock Entry GL correctness, only unblocking the posting pipeline.
    /// </summary>
    private static async Task SeedStockEntryGlRulesAsync(
        IRepository<MyERP.Accounting.Entities.Account, Guid> accountRepository,
        IRepository<MyERP.Accounting.Entities.AccountingRule, Guid> accountingRuleRepository,
        Guid companyId,
        string suffix)
    {
        var drAccount = await accountRepository.InsertAsync(
            new MyERP.Accounting.Entities.Account(Guid.NewGuid(), companyId, $"9{suffix}01", "Test Stock Clearing", MyERP.Accounting.AccountType.Asset),
            autoSave: true);
        var crAccount = await accountRepository.InsertAsync(
            new MyERP.Accounting.Entities.Account(Guid.NewGuid(), companyId, $"9{suffix}02", "Test Stock Adjustment", MyERP.Accounting.AccountType.Asset),
            autoSave: true);

        await accountingRuleRepository.InsertAsync(
            new MyERP.Accounting.Entities.AccountingRule(Guid.NewGuid(), companyId, $"SE Test DR {suffix}",
                "StockEntry", true, MyERP.Accounting.AccountSource.FixedAccount, MyERP.Accounting.AmountSource.GrandTotal)
            { FixedAccountId = drAccount.Id },
            autoSave: true);
        await accountingRuleRepository.InsertAsync(
            new MyERP.Accounting.Entities.AccountingRule(Guid.NewGuid(), companyId, $"SE Test CR {suffix}",
                "StockEntry", false, MyERP.Accounting.AccountSource.FixedAccount, MyERP.Accounting.AmountSource.GrandTotal)
            { FixedAccountId = crAccount.Id },
            autoSave: true);
    }
}
