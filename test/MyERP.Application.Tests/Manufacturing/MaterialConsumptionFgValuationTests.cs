using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Regression coverage for a valuation bug found while investigating why
/// MaterialConsumptionForManufacture stock entries throw StockEntryGlPurposeNotSupported
/// (migration backlog item): RecordProductionAsync's FG cost calc only summed
/// CalculateRawMaterialConsumption's own consumptionItems, which deliberately EXCLUDES any RM
/// already recorded via a prior CreateMaterialConsumptionAsync call (correctly, so it isn't
/// physically re-issued) — but nothing added that excluded RM's VALUE back into the FG's cost,
/// silently undervaluing every FG produced whenever Material Consumption was used ahead of
/// recording production. Same bug, independently, in CreateManufactureStockEntryAsync — which also
/// never subtracted WorkOrderItem.ConsumedQuantity at all, so it would additionally have
/// double-issued the pre-consumed RM (a physical over-consumption, not just a valuation gap).
/// </summary>
public abstract class MaterialConsumptionFgValuationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task RecordProductionAsync_JournalIsReadableAfterTheCallCompletes()
    {
        // Regression for a real, silent GL-posting failure: RecordProductionAsync called
        // stockPostingService.PostStockEntryAsync(entry) (inserts SLEs, no autoSave) immediately
        // followed by postingOrchestrator.PostStockEntryAsync(entry), whose own SLE query
        // (`_sleRepository.GetListAsync(... VoucherId == stockEntry.Id)`) ran before those SLEs were
        // ever flushed to the DB — so it always saw zero rows, the GL switch never ran, and the
        // Journal Entry was silently never built or inserted. RecordProductionAsync still returned
        // successfully every time — no exception, no error — so every "Record Production" click
        // silently failed to post its own GL journal. Confirmed with each AppService call awaited at
        // the TOP LEVEL (its own request/UnitOfWork boundary, matching how a real caller invokes it)
        // rather than chained inside one shared WithUnitOfWorkAsync test block, ruling out a test-only
        // artifact. Fixed by an explicit IUnitOfWorkManager.Current.SaveChangesAsync() flush between
        // the two calls (see RecordProductionAsync) — a plain flush, not a commit, so a later
        // exception in the same method still rolls everything back together under a real transactional
        // UnitOfWork.
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
        var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
        var settingsRepository = GetRequiredService<IRepository<ManufacturingSettings, Guid>>();
        var seRepository = GetRequiredService<IRepository<StockEntry, Guid>>();
        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.FiscalYear, Guid>>();
        var accountRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
        var manufacturingAppService = GetRequiredService<IManufacturingAppService>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Mc Fg Journal Readability Test Co"), autoSave: true);
        await SeedStockEntryGlRulesAsync(companyRepository, accountRepository, company.Id, "MCJR1");

        var fgItem = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "FG-MCJR1", "Finished Widget", ItemType.Goods), autoSave: true);
        var rmItem = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "RM-MCJR1", "Raw Bolt", ItemType.Goods), autoSave: true);
        var sourceWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "RM Store MCJR1"), autoSave: true);
        var fgWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG Store MCJR1"), autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series MCJR1", "SE", "SEMCJR1-"), autoSave: true);
        await fiscalYearRepository.InsertAsync(
            new MyERP.Accounting.Entities.FiscalYear(Guid.NewGuid(), company.Id, "FY-MCJR1",
                new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
            autoSave: true);

        await settingsRepository.InsertAsync(
            new ManufacturingSettings(Guid.NewGuid(), company.Id) { MaterialConsumption = true }, autoSave: true);

        var bom = await bomRepository.InsertAsync(
            new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-MCJR1", fgItem.Id), autoSave: true);

        var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-MCJR1", fgItem.Id, bom.Id, quantity: 100m)
        {
            SourceWarehouseId = sourceWarehouse.Id,
            FgWarehouseId = fgWarehouse.Id,
        };
        var woItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItem.Id, "Raw Bolt", requiredQuantity: 500m)
        {
            SourceWarehouseId = sourceWarehouse.Id,
            TransferredQuantity = 500m,
        };
        wo.RequiredItems.Add(woItem);
        wo.Submit();
        wo.Start();
        await woRepository.InsertAsync(wo, autoSave: true);

        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, rmItem.Id, sourceWarehouse.Id,
            DateTime.Today.AddDays(-1), quantityChange: 1000m, valuationRate: 10m,
            balanceQuantity: 1000m, balanceValue: 10000m)
        {
            StockQueue = "[[1000,10]]",
        }, autoSave: true);

        // Top-level await #1: its own request/UnitOfWork boundary, same as production.
        await manufacturingAppService.CreateMaterialConsumptionAsync(new CreateMaterialConsumptionDto
        {
            WorkOrderId = wo.Id,
            Items = { new ConsumptionItemDto { ItemId = rmItem.Id, Quantity = 200m } },
        });

        // Top-level await #2: separate boundary.
        await manufacturingAppService.RecordProductionAsync(wo.Id, quantity: 100m);

        var manufactureEntry = (await seRepository.GetListAsync(
            se => se.WorkOrderId == wo.Id && se.EntryType == StockEntryType.Manufacture, includeDetails: true))
            .Single();

        // Top-level await #3: a fresh read, same as a real caller checking the GL after the fact.
        var allJournals = await journalAppService.GetListAsync(
            new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var manufactureJournal = allJournals.Items.SingleOrDefault(j => j.ReferenceType == "StockEntry" && j.ReferenceId == manufactureEntry.Id);

        manufactureJournal.ShouldNotBeNull("RecordProductionAsync's own JournalEntry must be persisted and readable once the call has completed.");
        manufactureJournal!.TotalDebit.ShouldBe(manufactureJournal.TotalCredit);
    }

    [Fact]
    public async Task RecordProductionAsync_FoldsInPriorMaterialConsumptionValue()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var settingsRepository = GetRequiredService<IRepository<ManufacturingSettings, Guid>>();
            var seRepository = GetRequiredService<IRepository<StockEntry, Guid>>();
            var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.FiscalYear, Guid>>();
            var accountRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Mc Fg Valuation Test Co"), autoSave: true);
            await SeedStockEntryGlRulesAsync(companyRepository, accountRepository, company.Id, "MCFV1");

            var fgItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-MCFV1", "Finished Widget", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-MCFV1", "Raw Bolt", ItemType.Goods), autoSave: true);
            var sourceWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "RM Store MCFV1"), autoSave: true);
            var fgWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG Store MCFV1"), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series MCFV1", "SE", "SEMCFV1-"), autoSave: true);
            await fiscalYearRepository.InsertAsync(
                new MyERP.Accounting.Entities.FiscalYear(Guid.NewGuid(), company.Id, "FY-MCFV1",
                    new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            await settingsRepository.InsertAsync(
                new ManufacturingSettings(Guid.NewGuid(), company.Id) { MaterialConsumption = true }, autoSave: true);

            var bom = await bomRepository.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-MCFV1", fgItem.Id), autoSave: true);

            // WO for 100 FG units, requiring 5 bolts/unit (500 total), sourced from a warehouse
            // with a flat valuation rate of 10/unit throughout (a single seeded lot, no receipts in
            // between, so the rate stays 10 whether FIFO or moving-average is in effect).
            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-MCFV1", fgItem.Id, bom.Id, quantity: 100m)
            {
                SourceWarehouseId = sourceWarehouse.Id,
                FgWarehouseId = fgWarehouse.Id,
            };
            var woItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItem.Id, "Raw Bolt", requiredQuantity: 500m)
            {
                SourceWarehouseId = sourceWarehouse.Id,
                TransferredQuantity = 500m, // simulates RM already transferred, required by CreateMaterialConsumptionAsync
            };
            wo.RequiredItems.Add(woItem);
            wo.Submit();
            wo.Start();
            await woRepository.InsertAsync(wo, autoSave: true);

            await sleRepository.InsertAsync(new StockLedgerEntry(
                Guid.NewGuid(), company.Id, rmItem.Id, sourceWarehouse.Id,
                DateTime.Today.AddDays(-1), quantityChange: 1000m, valuationRate: 10m,
                balanceQuantity: 1000m, balanceValue: 10000m)
            {
                StockQueue = "[[1000,10]]",
            }, autoSave: true);

            // Record actual consumption of 200 bolts ahead of completing production — the whole
            // point of the MaterialConsumption setting. This alone used to throw
            // StockEntryGlPurposeNotSupported (MaterialConsumptionForManufacture had no GL
            // treatment at all) — confirms the new BuildMaterialConsumptionLinesAsync case works.
            var consumptionResult = await manufacturingAppService.CreateMaterialConsumptionAsync(new CreateMaterialConsumptionDto
            {
                WorkOrderId = wo.Id,
                Items = { new ConsumptionItemDto { ItemId = rmItem.Id, Quantity = 200m } },
            });

            var journalAppService = GetRequiredService<IJournalEntryAppService>();
            var consumptionJournals = await journalAppService.GetListAsync(
                new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
            var consumptionJournal = consumptionJournals.Items.Single(j =>
                j.ReferenceType == "StockEntry" && j.ReferenceId == consumptionResult.StockEntryId);
            // DR WIP (asset) 2000, CR Stock 2000 — never Expense, since the RM hasn't left the
            // company, it's been reclassified into work-in-process.
            var wipAccountId = company.DefaultWipAccountId!.Value;
            consumptionJournal.Lines.ShouldContain(l => l.AccountId == wipAccountId && l.IsDebit && l.Amount == 2000m);
            consumptionJournal.Lines.ShouldContain(l => l.AccountId == company.DefaultInventoryAccountId && !l.IsDebit && l.Amount == 2000m);

            // Act: complete production for all 100 units. CalculateRawMaterialConsumption will
            // correctly compute only 300 more bolts to consume here (500 required - 200 already
            // consumed) — but the FG's cost must still reflect the FULL 500 bolts' value (200 from
            // the Material Consumption entry above, 300 from this entry), not just 300.
            await manufacturingAppService.RecordProductionAsync(wo.Id, quantity: 100m);

            var manufactureEntry = (await seRepository.GetListAsync(
                se => se.WorkOrderId == wo.Id && se.EntryType == StockEntryType.Manufacture, includeDetails: true))
                .Single();
            var fgLine = manufactureEntry.Items.Single(i => i.ItemId == fgItem.Id);
            var rmLine = manufactureEntry.Items.Single(i => i.ItemId == rmItem.Id);

            // This entry's own RM-out line: only the 300 not already recorded via Material Consumption.
            rmLine.Quantity.ShouldBe(300m);

            // FG value must include BOTH: 300 × 10 (this entry) + 200 × 10 (prior Material
            // Consumption entry) = 5000 total → rate 50/unit. Pre-fix this was 300×10/100 = 30/unit,
            // silently dropping the pre-consumed 2000 of value.
            fgLine.ValuationRate.ShouldBe(50m);

            // GL regression: this Manufacture entry's own SLEs only cover the fresh 300×10=3000 RM
            // cost against the 5000 FG-in — a 2000 residual, exactly the earlier Material Consumption
            // entry's WIP debit. Before RecordProductionAsync's SaveChanges flush fix, this entry's
            // JournalEntry never even got created (see RecordProductionAsync_JournalIsReadableAfter...
            // above) — now it must clear the earlier WIP debit, not plug to Stock Adjustment (same
            // fix verified in isolation by StockEntryGlPostingTests.Manufacture_ClearsWipInstead...).
            var allJournalsAfterProduction = await journalAppService.GetListAsync(
                new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
            var manufactureJournal = allJournalsAfterProduction.Items
                .Single(j => j.ReferenceType == "StockEntry" && j.ReferenceId == manufactureEntry.Id);
            manufactureJournal.Lines.ShouldContain(l => l.AccountId == wipAccountId && !l.IsDebit && l.Amount == 2000m);
            manufactureJournal.Lines.ShouldNotContain(l => l.AccountId == company.DefaultStockAdjustmentAccountId);
            manufactureJournal.TotalDebit.ShouldBe(manufactureJournal.TotalCredit);
        });
    }

    [Fact]
    public async Task CreateManufactureStockEntryAsync_ExcludesAlreadyConsumedRM_AndFoldsInItsValue()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var settingsRepository = GetRequiredService<IRepository<ManufacturingSettings, Guid>>();
            var seRepository = GetRequiredService<IRepository<StockEntry, Guid>>();
            var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.FiscalYear, Guid>>();
            var accountRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Mc Fg Valuation Test Co 2"), autoSave: true);
            await SeedStockEntryGlRulesAsync(companyRepository, accountRepository, company.Id, "MCFV2");

            var fgItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-MCFV2", "Finished Widget 2", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-MCFV2", "Raw Screw", ItemType.Goods), autoSave: true);
            var sourceWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "RM Store MCFV2"), autoSave: true);
            var fgWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG Store MCFV2"), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series MCFV2", "SE", "SEMCFV2-"), autoSave: true);
            await fiscalYearRepository.InsertAsync(
                new MyERP.Accounting.Entities.FiscalYear(Guid.NewGuid(), company.Id, "FY-MCFV2",
                    new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            await settingsRepository.InsertAsync(
                new ManufacturingSettings(Guid.NewGuid(), company.Id) { MaterialConsumption = true }, autoSave: true);

            var bom = await bomRepository.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-MCFV2", fgItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-MCFV2", fgItem.Id, bom.Id, quantity: 100m)
            {
                SourceWarehouseId = sourceWarehouse.Id,
                FgWarehouseId = fgWarehouse.Id,
            };
            var woItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItem.Id, "Raw Screw", requiredQuantity: 500m)
            {
                SourceWarehouseId = sourceWarehouse.Id,
                TransferredQuantity = 500m,
            };
            wo.RequiredItems.Add(woItem);
            wo.Submit();
            wo.Start();
            await woRepository.InsertAsync(wo, autoSave: true);

            await sleRepository.InsertAsync(new StockLedgerEntry(
                Guid.NewGuid(), company.Id, rmItem.Id, sourceWarehouse.Id,
                DateTime.Today.AddDays(-1), quantityChange: 1000m, valuationRate: 10m,
                balanceQuantity: 1000m, balanceValue: 10000m)
            {
                StockQueue = "[[1000,10]]",
            }, autoSave: true);

            // Record 200 consumed ahead of time — same as the other test.
            await manufacturingAppService.CreateMaterialConsumptionAsync(new CreateMaterialConsumptionDto
            {
                WorkOrderId = wo.Id,
                Items = { new ConsumptionItemDto { ItemId = rmItem.Id, Quantity = 200m } },
            });

            // Act: use the OTHER Manufacture-entry creation path (the "Manufacture Stock Entry"
            // dialog's AppService call, distinct from RecordProductionAsync). Pre-fix, this ignored
            // WorkOrderItem.ConsumedQuantity entirely and would have tried to re-issue the FULL 500
            // required qty — 200 of which is no longer physically there to issue a second time.
            var result = await manufacturingAppService.CreateManufactureStockEntryAsync(new CreateManufactureStockEntryDto
            {
                WorkOrderId = wo.Id,
                FgQuantity = 100m,
            });

            var draftEntry = await seRepository.GetAsync(result.StockEntryId);
            var rmLine = draftEntry.Items.Single(i => i.ItemId == rmItem.Id);
            var fgLine = draftEntry.Items.Single(i => i.ItemId == fgItem.Id);

            // Only the unconsumed remainder (500 - 200 = 300), not the full 500.
            rmLine.Quantity.ShouldBe(300m);
            // FG cost folds in the prior 200×10=2000 alongside this entry's own 300×10=3000 → 5000/100 = 50.
            fgLine.ValuationRate.ShouldBe(50m);
        });
    }

    /// <summary>Mirrors DisassemblySourceResolutionTests' identically-named helper — minimal GL
    /// scaffolding so DocumentPostingOrchestrator.PostStockEntryAsync can complete without
    /// asserting anything about Stock Entry GL correctness itself.</summary>
    private static async Task SeedStockEntryGlRulesAsync(
        IRepository<Company, Guid> companyRepository,
        IRepository<MyERP.Accounting.Entities.Account, Guid> accountRepository,
        Guid companyId,
        string suffix)
    {
        var stockAccount = await accountRepository.InsertAsync(
            new MyERP.Accounting.Entities.Account(Guid.NewGuid(), companyId, $"9{suffix}01", "Test Stock", MyERP.Accounting.AccountType.Asset),
            autoSave: true);
        var adjustmentAccount = await accountRepository.InsertAsync(
            new MyERP.Accounting.Entities.Account(Guid.NewGuid(), companyId, $"9{suffix}02", "Test Stock Adjustment", MyERP.Accounting.AccountType.Equity),
            autoSave: true);
        var wipAccount = await accountRepository.InsertAsync(
            new MyERP.Accounting.Entities.Account(Guid.NewGuid(), companyId, $"9{suffix}03", "Test WIP", MyERP.Accounting.AccountType.Asset),
            autoSave: true);

        var company = await companyRepository.GetAsync(companyId);
        company.DefaultInventoryAccountId = stockAccount.Id;
        company.DefaultStockAdjustmentAccountId = adjustmentAccount.Id;
        company.DefaultWipAccountId = wipAccount.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);
    }
}
