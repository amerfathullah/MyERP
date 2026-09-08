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
/// Regression coverage for ItemStandardCostAppService.SubmitAsync's auto-revaluation, a gap this
/// session originally deferred as design-sized (round-106) and picked back up once
/// StockReconciliationAppService's GL posting was confirmed already fully wired (round-114): the
/// entity's own doc comment says submit should "create an auto-revaluation Stock Reconciliation
/// for all warehouses with stock" — before this fix, changing the standard rate updated
/// Item.StandardBuyingPrice (round-106) so FUTURE movements valued correctly, but existing
/// on-hand stock's booked balance value never moved to match, silently diverging from GL.
/// </summary>
public abstract class ItemStandardCostRevaluationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubmitAsync_RateChange_RevaluesExistingStockAndPostsGl()
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();
        var costAppService = GetRequiredService<IItemStandardCostAppService>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "ISC Reval Test Co"), autoSave: true);
        await fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY-ISCR", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
            autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "JE Series ISCR", "JE", "JEISCR-"), autoSave: true);

        var stockAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "91ISCR", "Test Stock", AccountType.Asset), autoSave: true);
        var adjustmentAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "93ISCR", "Test Stock Adjustment", AccountType.Equity), autoSave: true);
        company.DefaultStockAdjustmentAccountId = adjustmentAccount.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        var item = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "ITEM-ISCR", "Test Item ISCR", ItemType.Goods)
            {
                ValuationMethod = ValuationMethod.StandardCost,
            }, autoSave: true);
        var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WH ISCR"), autoSave: true);
        warehouse.DefaultAccountId = stockAccount.Id;
        await warehouseRepository.UpdateAsync(warehouse, autoSave: true);

        // First-ever standard cost record: establishes the baseline rate, nothing to revalue yet.
        var first = await costAppService.CreateAsync(new CreateItemStandardCostDto
        {
            CompanyId = company.Id,
            ItemId = item.Id,
            StandardRate = 5m,
            EffectiveDate = DateTime.Today.AddDays(-2),
        });
        await costAppService.SubmitAsync(first.Id);

        // Stock received at the current standard rate.
        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, warehouse.Id,
            DateTime.Today.AddDays(-1), quantityChange: 10m, valuationRate: 5m,
            balanceQuantity: 10m, balanceValue: 50m), autoSave: true);

        // Rate change: this is the one that should trigger revaluation.
        var second = await costAppService.CreateAsync(new CreateItemStandardCostDto
        {
            CompanyId = company.Id,
            ItemId = item.Id,
            StandardRate = 8m,
            EffectiveDate = DateTime.Today,
        });
        await costAppService.SubmitAsync(second.Id);

        var reloaded = await costAppService.GetAsync(second.Id);
        reloaded.RevaluationStockReconciliationId.ShouldNotBeNull();

        var latestSle = (await sleRepository.GetListAsync(s => s.ItemId == item.Id && s.WarehouseId == warehouse.Id))
            .OrderByDescending(s => s.CreationTime).First();
        latestSle.BalanceQuantity.ShouldBe(10m); // unchanged — pure revaluation
        latestSle.BalanceValue.ShouldBe(80m);    // 10 x new rate 8

        var journals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journal = journals.Items.Single(j => j.ReferenceType == "StockReconciliation" && j.ReferenceId == reloaded.RevaluationStockReconciliationId);

        journal.Lines.ShouldContain(l => l.AccountId == stockAccount.Id && Math.Abs(l.Amount) == 30m); // 80 - 50
        journal.Lines.ShouldContain(l => l.AccountId == adjustmentAccount.Id && Math.Abs(l.Amount) == 30m);
        journal.TotalDebit.ShouldBe(journal.TotalCredit);
    }
}
