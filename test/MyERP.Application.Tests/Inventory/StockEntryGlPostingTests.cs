using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for DocumentPostingOrchestrator.PostStockEntryAsync's purpose-aware GL
/// builder (76th migration session): before this fix, every Stock Entry post threw immediately —
/// DefaultDataSeeder and CompanyAppService's per-company seeder both seed zero AccountingRule rows
/// for DocumentType == "StockEntry", and there's no AppService or UI to add them, so the generic
/// engine path was completely unreachable. These tests exercise the real, full
/// IStockEntryAppService.CreateAsync -&gt; SubmitAsync -&gt; PostAsync pipeline (not just a
/// domain-service call) to confirm GL now posts correctly for each scoped purpose.
/// </summary>
/// <remarks>
/// Deliberately calls every AppService and repository method at the top level, unwrapped by any
/// test-owned WithUnitOfWorkAsync — matching the established working pattern elsewhere in this
/// test suite (SalesInvoiceAppService_Tests, PickListAppService_Tests). Wrapping a chain of
/// sequential AppService calls in one shared unit of work was tried and produced flaky
/// "just-inserted row invisible to a later query in the same test" failures; reads go through
/// IJournalEntryAppService (a real AppService, not a raw repository query) for the same reason.
/// </remarks>
public abstract class StockEntryGlPostingTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task MaterialIssue_Debits_Expense_Credits_Stock()
    {
        var (company, item, warehouse, _) = await SeedCommonAsync("MI");
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();

        // Seed a starting balance directly (not via a prior StockEntryAppService post) so the
        // Material Issue has a non-zero valuation rate to work with — this test only exercises
        // ONE Stock Entry AppService post, matching the pattern proven reliable elsewhere in this
        // suite; chaining two sequential CreateAsync->SubmitAsync->PostAsync calls in a single
        // test was tried and produced flaky "second entry's own SLE never persists" failures
        // specific to this ABP/EF-Core-SQLite test harness (AddAlwaysDisableUnitOfWorkTransaction
        // interacting with back-to-back [UnitOfWork]-wrapped AppService calls), unrelated to the
        // production GL logic itself (already proven correct by the single-post tests here and by
        // DisassemblySourceResolutionTests).
        // Item defaults to FIFO valuation. StockValuationService.CreateLedgerEntryAsync (which the
        // Material Issue below goes through) deserializes StockQueue to consume stock — a directly-
        // inserted seed row with no StockQueue looks like an empty FIFO queue and makes the
        // consumption below throw InsufficientStock despite BalanceQuantity correctly showing 20.
        // Must seed the queue explicitly to match what a real StockEntryAppService-posted receipt
        // would have produced (round 78 finding, see also round 77's StockPostingService fix).
        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, warehouse,
            DateTime.Today.AddDays(-1), quantityChange: 20m, valuationRate: 9m,
            balanceQuantity: 20m, balanceValue: 180m)
        {
            StockQueue = "[[20,9]]",
        }, autoSave: true);

        var created = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.MaterialIssue,
            PostingDate = DateTime.Today,
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 5m, SourceWarehouseId = warehouse } },
        });
        await stockEntryAppService.SubmitAsync(created.Id);
        await stockEntryAppService.PostAsync(created.Id);

        var allJournals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journal = allJournals.Items.Single(j => j.ReferenceType == "StockEntry" && j.ReferenceId == created.Id);

        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultExpenseAccountId && l.IsDebit && l.Amount == 45m); // 5 × 9
        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultInventoryAccountId && !l.IsDebit && l.Amount == 45m);
        journal.TotalDebit.ShouldBe(journal.TotalCredit);
    }

    [Fact]
    public async Task MaterialReceipt_Debits_Stock_Credits_Adjustment()
    {
        var (company, item, warehouse, _) = await SeedCommonAsync("MR");
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();

        var created = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.MaterialReceipt,
            PostingDate = DateTime.Today,
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 8m, TargetWarehouseId = warehouse, ValuationRate = 12m } },
        });
        await stockEntryAppService.SubmitAsync(created.Id);
        await stockEntryAppService.PostAsync(created.Id);

        var allJournals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journal = allJournals.Items.Single(j => j.ReferenceType == "StockEntry" && j.ReferenceId == created.Id);

        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultInventoryAccountId && l.IsDebit && l.Amount == 96m);
        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultStockAdjustmentAccountId && !l.IsDebit && l.Amount == 96m);
    }

    [Fact]
    public async Task MaterialTransfer_Debits_Target_Credits_Source_PerWarehouseAccount()
    {
        var (company, item, sourceWarehouse, targetWarehouse) = await SeedCommonAsync("MT");
        var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();

        // Give the target warehouse its OWN stock account, distinct from the company default,
        // to prove per-warehouse resolution (not just always the same company-wide account).
        var targetStockAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "1141", "Target Warehouse Stock", AccountType.Asset), autoSave: true);
        var targetWh = await warehouseRepository.GetAsync(targetWarehouse!.Value);
        targetWh.DefaultAccountId = targetStockAccount.Id;
        await warehouseRepository.UpdateAsync(targetWh, autoSave: true);

        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();

        // Seed source-warehouse stock directly (see MaterialIssue_... for why not via a prior
        // StockEntryAppService post) so the source-side valuation rate is non-zero.
        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, sourceWarehouse,
            DateTime.Today.AddDays(-1), quantityChange: 10m, valuationRate: 7m,
            balanceQuantity: 10m, balanceValue: 70m)
        {
            StockQueue = "[[10,7]]",
        }, autoSave: true);

        var transferEntry = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.MaterialTransfer,
            PostingDate = DateTime.Today,
            // ValuationRate must match the source's current rate (7) — a transfer moves the same
            // physical stock, so both legs must value identically or the two lines won't balance.
            // A caller (Angular form) that omits this defaults to rate 0 on the incoming leg,
            // which BuildStockToStockLinesAsync correctly detects as an imbalance and plugs to the
            // Stock Adjustment account rather than posting a fabricated per-warehouse amount.
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 4m, SourceWarehouseId = sourceWarehouse, TargetWarehouseId = targetWarehouse, ValuationRate = 7m } },
        });
        await stockEntryAppService.SubmitAsync(transferEntry.Id);
        await stockEntryAppService.PostAsync(transferEntry.Id);

        var allJournals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journal = allJournals.Items.Single(j => j.ReferenceType == "StockEntry" && j.ReferenceId == transferEntry.Id);

        journal.Lines.ShouldContain(l => l.AccountId == targetStockAccount.Id && l.IsDebit && l.Amount == 28m); // 4 × 7
        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultInventoryAccountId && !l.IsDebit && l.Amount == 28m);
        journal.TotalDebit.ShouldBe(journal.TotalCredit);
    }

    [Fact]
    public async Task MaterialTransferForManufacture_SameTreatmentAsMaterialTransfer()
    {
        // MaterialTransferForManufacture (WO material transfer, source -> WIP warehouse) validates
        // identically to plain MaterialTransfer in StockEntryManager.ValidateWarehousesAsync's own
        // "isTransfer" bucket (both source and target required) — same stock-to-stock, no-P&L-impact
        // GL treatment applies.
        var (company, item, sourceWarehouse, targetWarehouse) = await SeedCommonAsync("MTFM");

        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();

        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, sourceWarehouse,
            DateTime.Today.AddDays(-1), quantityChange: 10m, valuationRate: 5m,
            balanceQuantity: 10m, balanceValue: 50m)
        {
            StockQueue = "[[10,5]]",
        }, autoSave: true);

        var transferEntry = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.MaterialTransferForManufacture,
            PostingDate = DateTime.Today,
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 3m, SourceWarehouseId = sourceWarehouse, TargetWarehouseId = targetWarehouse, ValuationRate = 5m } },
        });
        await stockEntryAppService.SubmitAsync(transferEntry.Id);
        await stockEntryAppService.PostAsync(transferEntry.Id);

        var allJournals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journal = allJournals.Items.Single(j => j.ReferenceType == "StockEntry" && j.ReferenceId == transferEntry.Id);

        journal.Lines.Count.ShouldBe(2); // source and target both resolve to the company default here, but as 2 distinct lines
        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultInventoryAccountId && l.IsDebit && l.Amount == 15m); // 3 × 5
        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultInventoryAccountId && !l.IsDebit && l.Amount == 15m);
        journal.TotalDebit.ShouldBe(journal.TotalCredit);
    }

    [Fact]
    public async Task Repack_TreatedAsStockToStock_LikeManufacture()
    {
        // Repack posts an SLE per item off that item's OWN SourceWarehouseId/TargetWarehouseId
        // (StockPostingService), identical to Manufacture/Disassemble — a source (RM) item and a
        // distinct target (FG) item, priced so the two legs balance exactly with no residual.
        var (company, rmItem, sourceWarehouse, targetWarehouse) = await SeedCommonAsync("RPK");

        var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        var fgItem = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "ITEM-RPK-FG", "Test FG Item RPK", ItemType.Goods), autoSave: true);

        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();

        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, rmItem.Id, sourceWarehouse,
            DateTime.Today.AddDays(-1), quantityChange: 10m, valuationRate: 6m,
            balanceQuantity: 10m, balanceValue: 60m)
        {
            StockQueue = "[[10,6]]",
        }, autoSave: true);

        var repackEntry = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.Repack,
            PostingDate = DateTime.Today,
            Items =
            {
                // Outgoing (source-only): 5 x 6 = 30 consumed.
                new CreateStockEntryItemDto { ItemId = rmItem.Id, Quantity = 5m, SourceWarehouseId = sourceWarehouse },
                // Incoming (target-only, FG): priced to match the outgoing cost exactly (30 / 3 = 10/unit).
                new CreateStockEntryItemDto { ItemId = fgItem.Id, Quantity = 3m, TargetWarehouseId = targetWarehouse, ValuationRate = 10m },
            },
        });
        await stockEntryAppService.SubmitAsync(repackEntry.Id);
        await stockEntryAppService.PostAsync(repackEntry.Id);

        var allJournals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journal = allJournals.Items.Single(j => j.ReferenceType == "StockEntry" && j.ReferenceId == repackEntry.Id);

        journal.Lines.Count.ShouldBe(2); // balances exactly, no Stock Adjustment plug needed
        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultInventoryAccountId && l.IsDebit && l.Amount == 30m); // FG in
        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultInventoryAccountId && !l.IsDebit && l.Amount == 30m); // RM out
        journal.TotalDebit.ShouldBe(journal.TotalCredit);
    }

    [Fact]
    public async Task SendToSubcontractor_SameTreatmentAsMaterialTransfer()
    {
        // SendToSubcontractor moves RM from own warehouse to the SCO's supplier warehouse — a
        // plain Warehouse row, resolved by WarehouseAccountService like any other. Same
        // stock-to-stock, no-P&L-impact GL treatment as MaterialTransfer applies.
        var (company, item, sourceWarehouse, supplierWarehouse) = await SeedCommonAsync("STS");

        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();

        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, sourceWarehouse,
            DateTime.Today.AddDays(-1), quantityChange: 10m, valuationRate: 4m,
            balanceQuantity: 10m, balanceValue: 40m)
        {
            StockQueue = "[[10,4]]",
        }, autoSave: true);

        var sendEntry = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.SendToSubcontractor,
            PostingDate = DateTime.Today,
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 6m, SourceWarehouseId = sourceWarehouse, TargetWarehouseId = supplierWarehouse, ValuationRate = 4m } },
        });
        await stockEntryAppService.SubmitAsync(sendEntry.Id);
        await stockEntryAppService.PostAsync(sendEntry.Id);

        var allJournals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journal = allJournals.Items.Single(j => j.ReferenceType == "StockEntry" && j.ReferenceId == sendEntry.Id);

        journal.Lines.Count.ShouldBe(2);
        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultInventoryAccountId && l.IsDebit && l.Amount == 24m); // 6 x 4
        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultInventoryAccountId && !l.IsDebit && l.Amount == 24m);
        journal.TotalDebit.ShouldBe(journal.TotalCredit);
    }

    [Fact]
    public async Task SendToSubcontractor_MissingTargetWarehouse_Throws()
    {
        // Regression guard for this session's ValidateWarehousesAsync fix: SendToSubcontractor now
        // requires both warehouses like MaterialTransfer, closing the gap where a manually-authored
        // entry (unlike CreateRmTransferStockEntryAsync's own always-both-warehouses path) could
        // previously be created with only a source warehouse.
        var (company, item, sourceWarehouse, _) = await SeedCommonAsync("STSMISS");
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();

        await Should.ThrowAsync<Volo.Abp.BusinessException>(() => stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.SendToSubcontractor,
            PostingDate = DateTime.Today,
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 6m, SourceWarehouseId = sourceWarehouse } },
        }));
    }

    [Fact]
    public async Task SubcontractingDelivery_SameTreatmentAsMaterialTransfer()
    {
        // SubcontractingDelivery/SubcontractingReturn were previously unhandled by
        // DocumentPostingOrchestrator's switch (fell to the `default:` throw) despite both being
        // real, user-selectable purposes on the Angular form — same stock-to-stock shape as
        // SendToSubcontractor above (source + target both required per the form's
        // showSourceWarehouse/showTargetWarehouse rules).
        var (company, item, sourceWarehouse, supplierWarehouse) = await SeedCommonAsync("SCD");

        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();

        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, sourceWarehouse,
            DateTime.Today.AddDays(-1), quantityChange: 10m, valuationRate: 3m,
            balanceQuantity: 10m, balanceValue: 30m)
        {
            StockQueue = "[[10,3]]",
        }, autoSave: true);

        var deliveryEntry = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.SubcontractingDelivery,
            PostingDate = DateTime.Today,
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 5m, SourceWarehouseId = sourceWarehouse, TargetWarehouseId = supplierWarehouse, ValuationRate = 3m } },
        });
        await stockEntryAppService.SubmitAsync(deliveryEntry.Id);
        await stockEntryAppService.PostAsync(deliveryEntry.Id);

        var allJournals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journal = allJournals.Items.Single(j => j.ReferenceType == "StockEntry" && j.ReferenceId == deliveryEntry.Id);

        journal.Lines.Count.ShouldBe(2);
        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultInventoryAccountId && l.IsDebit && l.Amount == 15m); // 5 x 3
        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultInventoryAccountId && !l.IsDebit && l.Amount == 15m);
        journal.TotalDebit.ShouldBe(journal.TotalCredit);
    }

    [Fact]
    public async Task SubcontractingDelivery_MissingTargetWarehouse_Throws()
    {
        var (company, item, sourceWarehouse, _) = await SeedCommonAsync("SCDMISS");
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();

        await Should.ThrowAsync<Volo.Abp.BusinessException>(() => stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.SubcontractingDelivery,
            PostingDate = DateTime.Today,
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 5m, SourceWarehouseId = sourceWarehouse } },
        }));
    }

    [Fact]
    public async Task SubcontractingReturn_MissingSourceWarehouse_Throws()
    {
        var (company, item, _, targetWarehouse) = await SeedCommonAsync("SCRMISS");
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();

        await Should.ThrowAsync<Volo.Abp.BusinessException>(() => stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.SubcontractingReturn,
            PostingDate = DateTime.Today,
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 5m, TargetWarehouseId = targetWarehouse } },
        }));
    }

    [Fact]
    public async Task Adjustment_TreatedSameAsMaterialReceipt()
    {
        // Adjustment hides source warehouse on the Angular form (only target is shown), so every
        // item is a target-only stock-in — same GL shape as Material Receipt.
        var (company, item, _, targetWarehouse) = await SeedCommonAsync("ADJ");
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();

        var created = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.Adjustment,
            PostingDate = DateTime.Today,
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 8m, TargetWarehouseId = targetWarehouse, ValuationRate = 12m } },
        });
        await stockEntryAppService.SubmitAsync(created.Id);
        await stockEntryAppService.PostAsync(created.Id);

        var allJournals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journal = allJournals.Items.Single(j => j.ReferenceType == "StockEntry" && j.ReferenceId == created.Id);

        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultInventoryAccountId && l.IsDebit && l.Amount == 96m);
        journal.Lines.ShouldContain(l => l.AccountId == company.DefaultStockAdjustmentAccountId && !l.IsDebit && l.Amount == 96m);
    }

    [Fact]
    public async Task Manufacture_ClearsWipInsteadOfStockAdjustment_WhenWorkOrderHadPriorMaterialConsumption()
    {
        // Regression for the WIP-accumulates bug documented at DocumentPostingOrchestrator's
        // MaterialConsumptionForManufacture case: a WO's Material Consumption entry debits WIP for
        // the RM it pre-consumes; the RM isn't re-issued in the later Manufacture entry (only the FG
        // cost folds that value back in), so the Manufacture entry's own SLEs run short by exactly
        // that value. The residual must clear the earlier WIP debit, not plug to Stock Adjustment.
        var (company, item, sourceWarehouse, targetWarehouse) = await SeedCommonAsync("MFGWIP");

        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var wipAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "94MFGWIP", "Test WIP", AccountType.Asset), autoSave: true);
        company.DefaultWipAccountId = wipAccount.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        var fgItem = await GetRequiredService<IRepository<Item, Guid>>().InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "ITEM-MFGWIP-FG", "Test FG MFGWIP", ItemType.Goods), autoSave: true);

        var seRepository = GetRequiredService<IRepository<StockEntry, Guid>>();
        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var stockPostingService = GetRequiredService<StockPostingService>();
        var postingOrchestrator = GetRequiredService<DocumentPostingOrchestrator>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();

        var workOrderId = Guid.NewGuid();

        // Source-out SLEs price at the warehouse's CURRENT moving-average rate (StockPostingService),
        // not the AddItem-supplied rate — seed a starting balance so both consumptions below value
        // at a known 4/unit.
        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, sourceWarehouse,
            DateTime.Today.AddDays(-1), quantityChange: 20m, valuationRate: 4m,
            balanceQuantity: 20m, balanceValue: 80m)
        {
            StockQueue = "[[20,4]]",
        }, autoSave: true);

        // Raw domain-service calls (StockPostingService/DocumentPostingOrchestrator, not AppServices)
        // need an ambient UnitOfWork to keep the same DbContext alive across the sequence below —
        // an AppService call carries its own via [UnitOfWork], these don't.
        await WithUnitOfWorkAsync(async () =>
        {
            // Prior Material Consumption entry for this WO: 5 units RM @ rate 4 = 20 → DR WIP 20, CR Stock 20.
            var consumptionEntry = new StockEntry(Guid.NewGuid(), company.Id, StockEntryType.MaterialConsumptionForManufacture, DateTime.Today, company.TenantId)
            { WorkOrderId = workOrderId };
            consumptionEntry.AddItem(item.Id, 5m, sourceWarehouseId: sourceWarehouse, targetWarehouseId: null, valuationRate: 4m);
            consumptionEntry.Submit();
            consumptionEntry.Post();
            await stockPostingService.PostStockEntryAsync(consumptionEntry);
            await postingOrchestrator.PostStockEntryAsync(consumptionEntry);
            await seRepository.InsertAsync(consumptionEntry, autoSave: true);

            // Manufacture entry for the same WO: only 2 fresh RM units issued here (8 cost) — the other
            // 5 were already consumed above — but FG is priced at 28 (8 fresh + 20 prior), same as
            // ManufacturingAppService.RecordProductionAsync's totalRmCost += GetPriorMaterialConsumptionValueAsync.
            var manufactureEntry = new StockEntry(Guid.NewGuid(), company.Id, StockEntryType.Manufacture, DateTime.Today, company.TenantId)
            { WorkOrderId = workOrderId };
            manufactureEntry.AddItem(item.Id, 2m, sourceWarehouseId: sourceWarehouse, targetWarehouseId: null, valuationRate: 4m);
            manufactureEntry.AddItem(fgItem.Id, 1m, sourceWarehouseId: null, targetWarehouseId: targetWarehouse, valuationRate: 28m);
            manufactureEntry.Submit();
            manufactureEntry.Post();
            await stockPostingService.PostStockEntryAsync(manufactureEntry);
            await postingOrchestrator.PostStockEntryAsync(manufactureEntry);
            await seRepository.InsertAsync(manufactureEntry, autoSave: true);

            var allJournals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
            var manufactureJournal = allJournals.Items.Single(j => j.ReferenceType == "StockEntry" && j.ReferenceId == manufactureEntry.Id);

            // Residual = 28 (FG in) - 8 (fresh RM out) = 20 — exactly the prior entry's WIP debit.
            manufactureJournal.Lines.ShouldContain(l => l.AccountId == wipAccount.Id && !l.IsDebit && l.Amount == 20m);
            manufactureJournal.Lines.ShouldNotContain(l => l.AccountId == company.DefaultStockAdjustmentAccountId);
            manufactureJournal.TotalDebit.ShouldBe(manufactureJournal.TotalCredit);
        });
    }

    [Fact]
    public async Task Manufacture_PlugsStockAdjustment_WhenWorkOrderHadNoPriorMaterialConsumption()
    {
        // Regression guard for the OTHER branch of the same fix: a WO with no Material Consumption
        // history must keep the ordinary Stock Adjustment plug (e.g. a genuine multi-FG Repack-style
        // valuation gap) — the WIP-clearing path must not fire unconditionally for every Manufacture
        // entry just because it references a Work Order.
        var (company, item, sourceWarehouse, targetWarehouse) = await SeedCommonAsync("MFGNOWIP");

        var fgItem = await GetRequiredService<IRepository<Item, Guid>>().InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "ITEM-MFGNOWIP-FG", "Test FG MFGNOWIP", ItemType.Goods), autoSave: true);

        var seRepository = GetRequiredService<IRepository<StockEntry, Guid>>();
        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var stockPostingService = GetRequiredService<StockPostingService>();
        var postingOrchestrator = GetRequiredService<DocumentPostingOrchestrator>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();

        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, sourceWarehouse,
            DateTime.Today.AddDays(-1), quantityChange: 20m, valuationRate: 4m,
            balanceQuantity: 20m, balanceValue: 80m)
        {
            StockQueue = "[[20,4]]",
        }, autoSave: true);

        await WithUnitOfWorkAsync(async () =>
        {
            // Deliberately mispriced FG (10 instead of the balanced 8) to force a genuine residual,
            // with no Material Consumption entry for this WorkOrderId anywhere.
            var manufactureEntry = new StockEntry(Guid.NewGuid(), company.Id, StockEntryType.Manufacture, DateTime.Today, company.TenantId)
            { WorkOrderId = Guid.NewGuid() };
            manufactureEntry.AddItem(item.Id, 2m, sourceWarehouseId: sourceWarehouse, targetWarehouseId: null, valuationRate: 4m);
            manufactureEntry.AddItem(fgItem.Id, 1m, sourceWarehouseId: null, targetWarehouseId: targetWarehouse, valuationRate: 10m);
            manufactureEntry.Submit();
            manufactureEntry.Post();
            await stockPostingService.PostStockEntryAsync(manufactureEntry);
            await postingOrchestrator.PostStockEntryAsync(manufactureEntry);
            await seRepository.InsertAsync(manufactureEntry, autoSave: true);

            var allJournals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
            var manufactureJournal = allJournals.Items.Single(j => j.ReferenceType == "StockEntry" && j.ReferenceId == manufactureEntry.Id);

            manufactureJournal.Lines.ShouldContain(l => l.AccountId == company.DefaultStockAdjustmentAccountId && !l.IsDebit && l.Amount == 2m);
            manufactureJournal.TotalDebit.ShouldBe(manufactureJournal.TotalCredit);
        });
    }

    private async Task<(Company Company, Item Item, Guid SourceWarehouse, Guid? TargetWarehouse)> SeedCommonAsync(string suffix)
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), $"SE GL Test Co {suffix}"), autoSave: true);
        var item = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, $"ITEM-{suffix}", $"Test Item {suffix}", ItemType.Goods), autoSave: true);
        var sourceWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, $"WH Source {suffix}"), autoSave: true);
        var targetWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, $"WH Target {suffix}"), autoSave: true);
        var stockAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"91{suffix}", "Test Stock", AccountType.Asset), autoSave: true);
        var expenseAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"92{suffix}", "Test Expense", AccountType.Expense), autoSave: true);
        var adjustmentAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"93{suffix}", "Test Stock Adjustment", AccountType.Equity), autoSave: true);
        var costCenterRepository = GetRequiredService<IRepository<Accounting.Entities.CostCenter, Guid>>();
        var costCenter = await costCenterRepository.InsertAsync(
            new Accounting.Entities.CostCenter(Guid.NewGuid(), company.Id, $"Test CC {suffix}"), autoSave: true);

        company.DefaultInventoryAccountId = stockAccount.Id;
        company.DefaultExpenseAccountId = expenseAccount.Id;
        company.DefaultStockAdjustmentAccountId = adjustmentAccount.Id;
        company.DefaultCostCenterId = costCenter.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        await fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, $"FY-{suffix}", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
            autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, $"SE Series {suffix}", "StockEntry", $"SE{suffix}-"), autoSave: true);

        return (company, item, sourceWarehouse.Id, targetWarehouse.Id);
    }
}
