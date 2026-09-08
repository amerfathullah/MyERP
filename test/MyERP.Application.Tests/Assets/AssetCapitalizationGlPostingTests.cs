using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

/// <summary>
/// Regression coverage for a gap found while sweeping zero-UI-caller AppService methods (and
/// deferred earlier in the same session as design-sized): AssetCapitalizationAppService.SubmitAsync
/// updated the target asset's book value but never touched a StockLedgerEntry for consumed stock
/// items (the entity's own doc comment says stock items "reduce inventory") and posted zero GL for
/// any of the three source types (stock/service/consumed assets). Fixed via
/// AssetCapitalizationPostingService.
/// </summary>
public abstract class AssetCapitalizationGlPostingTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubmitAsync_ConsumesStockAndPostsBalancedGl()
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var categoryRepository = GetRequiredService<IRepository<AssetCategory, Guid>>();
        var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();
        var capAppService = GetRequiredService<IAssetCapitalizationAppService>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "AC GL Test Co"), autoSave: true);
        await fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY-AC", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
            autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "JE Series AC", "JE", "JEAC-"), autoSave: true);

        var item = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "ITEM-AC", "Test Item AC", ItemType.Goods), autoSave: true);
        var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WH AC"), autoSave: true);

        // Starting stock: 10 units @ 5 (value 50) so there's enough to consume 4 units from.
        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, warehouse.Id,
            DateTime.Today.AddDays(-1), quantityChange: 10m, valuationRate: 5m,
            balanceQuantity: 10m, balanceValue: 50m)
        {
            StockQueue = "[[10,5]]",
        }, autoSave: true);

        var stockAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "91AC", "Test Stock", AccountType.Asset), autoSave: true);
        warehouse.DefaultAccountId = stockAccount.Id;
        await warehouseRepository.UpdateAsync(warehouse, autoSave: true);

        var expenseAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "92AC", "Test Service Expense", AccountType.Expense), autoSave: true);
        var fixedAssetAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "15AC", "Test Fixed Asset", AccountType.Asset), autoSave: true);

        var category = new AssetCategory(Guid.NewGuid(), "Test Category AC");
        category.AddAccount(Guid.NewGuid(), company.Id, fixedAssetAccount.Id);
        await categoryRepository.InsertAsync(category, autoSave: true);

        var targetAsset = await assetRepository.InsertAsync(
            new Asset(Guid.NewGuid(), company.Id, "AST-AC-TARGET", "Target Asset", DateTime.Today, purchaseAmount: 0m)
            {
                AssetCategoryId = category.Id,
            }, autoSave: true);

        var created = await capAppService.CreateAsync(new CreateUpdateAssetCapitalizationDto
        {
            CompanyId = company.Id,
            PostingDate = DateTime.Today,
            TargetAssetId = targetAsset.Id,
            TargetAssetName = targetAsset.AssetName,
            StockItems =
            {
                new CreateUpdateAssetCapitalizationStockItemDto
                {
                    ItemId = item.Id, ItemName = item.ItemName, Qty = 4m, Rate = 5m, WarehouseId = warehouse.Id,
                },
            },
            ServiceItems =
            {
                new CreateUpdateAssetCapitalizationServiceItemDto
                {
                    ItemId = item.Id, ItemName = "Installation", Amount = 30m, ExpenseAccountId = expenseAccount.Id,
                },
            },
        });

        await capAppService.SubmitAsync(created.Id);

        // Stock actually left the warehouse — the core phantom-stock fix.
        var balanceAfter = (await sleRepository.GetListAsync(s => s.ItemId == item.Id && s.WarehouseId == warehouse.Id))
            .OrderByDescending(s => s.CreationTime).First();
        balanceAfter.BalanceQuantity.ShouldBe(6m); // 10 - 4

        // GL: DR target Fixed Asset for the true total (4x5 stock + 30 service = 50), balanced.
        var journals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journal = journals.Items.Single(j => j.ReferenceType == "AssetCapitalization" && j.ReferenceId == created.Id);

        journal.TotalDebit.ShouldBe(50m);
        journal.TotalCredit.ShouldBe(50m);
        journal.Lines.ShouldContain(l => l.AccountId == fixedAssetAccount.Id && l.IsDebit && l.Amount == 50m);
        journal.Lines.ShouldContain(l => l.AccountId == stockAccount.Id && !l.IsDebit && l.Amount == 20m); // 4 x 5
        journal.Lines.ShouldContain(l => l.AccountId == expenseAccount.Id && !l.IsDebit && l.Amount == 30m);

        // Target asset's own book value tracks the same true total.
        var reloadedTarget = await assetRepository.GetAsync(targetAsset.Id);
        reloadedTarget.ValueAfterDepreciation.ShouldBe(50m);
    }
}
