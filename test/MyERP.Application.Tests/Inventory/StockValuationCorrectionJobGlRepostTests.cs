using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory.BackgroundJobs;
using MyERP.Inventory.Entities;
using MyERP.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for a gap found while sweeping for zero-UI-caller AppService methods:
/// StockValuationCorrectionJob is the job NightlyProcessingWorker actually enqueues to process
/// queued Repost Item Valuation requests, but it only recalculated StockLedgerEntry
/// balance/valuation — it never reposted GL. Its sibling, RepostItemValuationJob, does repost GL
/// correctly (advisory locking, Bin sync, GlRepostService) but is never enqueued anywhere in the
/// codebase, making it dead code. Before this fix, every valuation repost silently left GL pointing
/// at pre-repost amounts forever — invisible until someone reconciled stock value against the GL
/// stock account.
/// </summary>
public abstract class StockValuationCorrectionJobGlRepostTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ExecuteAsync_RepostsGlForVoucherAffectedByBackdatedCorrection()
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var repostRepository = GetRequiredService<IRepository<RepostItemValuation, Guid>>();
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();
        var job = GetRequiredService<StockValuationCorrectionJob>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SVC Repost Test Co"), autoSave: true);
        var item = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "ITEM-SVC-1", "Test Item SVC", ItemType.Goods), autoSave: true);
        var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WH SVC"), autoSave: true);
        var stockAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "91SVC", "Test Stock", AccountType.Asset), autoSave: true);
        var adjustmentAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "93SVC", "Test Stock Adjustment", AccountType.Equity), autoSave: true);
        var costCenterRepository = GetRequiredService<IRepository<CostCenter, Guid>>();
        var costCenter = await costCenterRepository.InsertAsync(
            new CostCenter(Guid.NewGuid(), company.Id, "Test CC SVC"), autoSave: true);

        company.DefaultInventoryAccountId = stockAccount.Id;
        company.DefaultStockAdjustmentAccountId = adjustmentAccount.Id;
        company.DefaultCostCenterId = costCenter.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        await fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY-SVC", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
            autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series SVC", "StockEntry", "SESVC-"), autoSave: true);

        // A real Material Receipt, posted today, valued at 8/unit — 5 x 8 = 40 DR Stock / CR Adjustment.
        var receipt = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.MaterialReceipt,
            PostingDate = DateTime.Today,
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 5m, TargetWarehouseId = warehouse.Id, ValuationRate = 8m } },
        });
        await stockEntryAppService.SubmitAsync(receipt.Id);
        await stockEntryAppService.PostAsync(receipt.Id);

        var journalsBeforeRepost = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        journalsBeforeRepost.Items.Count(j => j.ReferenceType == "StockEntry" && j.ReferenceId == receipt.Id).ShouldBe(1);

        // Simulate a backdated correction being discovered: a stock movement dated BEFORE the
        // receipt above that the running balance never accounted for. This is exactly the scenario
        // that auto-creates a Repost Item Valuation entry in production.
        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, warehouse.Id,
            DateTime.Today.AddDays(-1), quantityChange: 3m, valuationRate: 6m,
            balanceQuantity: 3m, balanceValue: 18m), autoSave: true);

        var repost = new RepostItemValuation(Guid.NewGuid(), company.Id, RepostMethod.ItemAndWarehouse,
            DateTime.Today.AddDays(-1), item.Id, warehouse.Id, company.TenantId);
        await repostRepository.InsertAsync(repost, autoSave: true);

        // Raw job call (not an AppService, carries no [UnitOfWork] of its own) needs an ambient
        // unit of work to keep the same DbContext alive across its sequence of repository calls —
        // matches the pattern already established in StockEntryGlPostingTests for the same reason.
        await WithUnitOfWorkAsync(async () =>
        {
            await job.ExecuteAsync(new StockValuationCorrectionJobArgs { CompanyId = company.Id, TenantId = company.TenantId });
        });

        var reloadedRepost = await repostRepository.GetAsync(repost.Id);
        reloadedRepost.Status.ShouldBe(RepostStatus.Completed);

        // The receipt's own SLE must now chain off the backdated leg's balance (3@18) instead of
        // starting from zero — proves the pre-existing SLE-correction behavior still works.
        var receiptSle = (await sleRepository.GetListAsync(s => s.VoucherType == "StockEntry" && s.VoucherId == receipt.Id)).Single();
        receiptSle.BalanceQuantity.ShouldBe(8m); // 3 (backdated) + 5 (receipt)
        receiptSle.BalanceValue.ShouldBe(58m);   // 18 (backdated) + 40 (receipt, unchanged incoming value)

        // The actual fix: GL for the receipt's voucher must have been reposted, not left untouched.
        var journalsAfterRepost = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journalsForReceipt = journalsAfterRepost.Items.Where(j => j.ReferenceType == "StockEntry" && j.ReferenceId == receipt.Id).ToList();
        journalsForReceipt.Count.ShouldBeGreaterThan(1); // original + reversal (+ repost), not just the original
    }
}
