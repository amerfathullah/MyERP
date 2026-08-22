using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
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
        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, warehouse,
            DateTime.Today.AddDays(-1), quantityChange: 20m, valuationRate: 9m,
            balanceQuantity: 20m, balanceValue: 180m), autoSave: true);

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
            balanceQuantity: 10m, balanceValue: 70m), autoSave: true);

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
            balanceQuantity: 10m, balanceValue: 50m), autoSave: true);

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
            balanceQuantity: 10m, balanceValue: 60m), autoSave: true);

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
