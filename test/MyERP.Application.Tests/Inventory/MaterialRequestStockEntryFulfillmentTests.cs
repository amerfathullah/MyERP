using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Shared;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for the Material Request → Stock Entry fulfillment gap: Transfer/Issue-type
/// MRs previously had zero cap on repeated Stock Entry pulls — StockEntryItem had no
/// MaterialRequestItemId column at all, and MaterialRequestManager's fulfillment-tracking method was
/// built but never called from anywhere (the Stock Entry side had no equivalent of
/// PurchaseOrderManager.UpdateMaterialRequestOrderedQtyAsync). Exercises the real
/// IStockEntryAppService.CreateAsync -&gt; SubmitAsync/CancelAsync pipeline against a Transfer/Issue
/// MaterialRequest, mirroring StockEntryGlPostingTests' pattern of calling AppServices directly
/// rather than domain services in isolation.
/// </summary>
public abstract class MaterialRequestStockEntryFulfillmentTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task Submit_IncreasesOrderedQuantity_AndMovesPerOrdered()
    {
        var (company, item, sourceWh, targetWh, mr, mrItemId) = await SeedAsync("SUB", 10m);
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();
        var mrRepository = GetRequiredService<IRepository<MaterialRequest, Guid>>();

        var created = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.MaterialTransfer,
            PostingDate = DateTime.Today,
            Items =
            {
                new CreateStockEntryItemDto
                {
                    ItemId = item.Id, Quantity = 6m,
                    SourceWarehouseId = sourceWh, TargetWarehouseId = targetWh,
                    MaterialRequestItemId = mrItemId,
                },
            },
        });
        await stockEntryAppService.SubmitAsync(created.Id);

        var reloadedMr = await mrRepository.GetAsync(mr.Id);
        reloadedMr.Items.Single(i => i.Id == mrItemId).OrderedQuantity.ShouldBe(6m);
        reloadedMr.PerOrdered.ShouldBe(60m);
    }

    [Fact]
    public async Task Submit_ExceedingPendingQtyAcrossMultipleStockEntries_Throws()
    {
        // The core reported bug: before this fix, nothing stopped a second (or third, or
        // hundredth) Stock Entry from pulling the same already-fulfilled Material Request line.
        var (company, item, sourceWh, targetWh, _, mrItemId) = await SeedAsync("OVER", 10m);
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();

        var first = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.MaterialTransfer,
            PostingDate = DateTime.Today,
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 6m, SourceWarehouseId = sourceWh, TargetWarehouseId = targetWh, MaterialRequestItemId = mrItemId } },
        });
        await stockEntryAppService.SubmitAsync(first.Id);

        // Pending is now 10 - 6 = 4. Attempting to pull 5 more must be rejected, not silently allowed.
        var second = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.MaterialTransfer,
            PostingDate = DateTime.Today,
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 5m, SourceWarehouseId = sourceWh, TargetWarehouseId = targetWh, MaterialRequestItemId = mrItemId } },
        });

        await Should.ThrowAsync<BusinessException>(() => stockEntryAppService.SubmitAsync(second.Id));
    }

    [Fact]
    public async Task Cancel_ReversesOrderedQuantity()
    {
        var (company, item, sourceWh, _, mr, mrItemId) = await SeedAsync("CXL", 10m);
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();
        var mrRepository = GetRequiredService<IRepository<MaterialRequest, Guid>>();
        var sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();

        // Cancel requires the entry to have been Posted first (StockEntry.Cancel() guards on
        // Status == Posted) — seed a FIFO queue so the outward Material Issue post can consume it.
        await sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, sourceWh,
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
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 4m, SourceWarehouseId = sourceWh, MaterialRequestItemId = mrItemId } },
        });
        await stockEntryAppService.SubmitAsync(created.Id);

        var afterSubmitMr = await mrRepository.GetAsync(mr.Id);
        afterSubmitMr.Items.Single(i => i.Id == mrItemId).OrderedQuantity.ShouldBe(4m);

        await stockEntryAppService.PostAsync(created.Id);
        await stockEntryAppService.CancelAsync(created.Id);

        var reloadedMr = await mrRepository.GetAsync(mr.Id);
        reloadedMr.Items.Single(i => i.Id == mrItemId).OrderedQuantity.ShouldBe(0m);
    }

    [Fact]
    public async Task GetItemsFromMaterialRequest_ExcludesAlreadyFulfilledQty()
    {
        var (company, item, sourceWh, targetWh, mr, mrItemId) = await SeedAsync("PEND", 10m);
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();

        var created = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.MaterialTransfer,
            PostingDate = DateTime.Today,
            Items = { new CreateStockEntryItemDto { ItemId = item.Id, Quantity = 6m, SourceWarehouseId = sourceWh, TargetWarehouseId = targetWh, MaterialRequestItemId = mrItemId } },
        });
        await stockEntryAppService.SubmitAsync(created.Id);

        var result = await stockEntryAppService.GetItemsFromMaterialRequestAsync(mr.Id);
        result.Items.Single().Quantity.ShouldBe(4m);
    }

    private async Task<(Company Company, Item Item, Guid SourceWarehouse, Guid TargetWarehouse, MaterialRequest Mr, Guid MrItemId)> SeedAsync(string suffix, decimal mrQty)
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Accounting.Entities.Account, Guid>>();
        var costCenterRepository = GetRequiredService<IRepository<Accounting.Entities.CostCenter, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<Accounting.Entities.FiscalYear, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var mrRepository = GetRequiredService<IRepository<MaterialRequest, Guid>>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), $"MR SE Test Co {suffix}"), autoSave: true);
        var item = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, $"ITEM-{suffix}", $"Test Item {suffix}", ItemType.Goods), autoSave: true);
        var sourceWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, $"WH Source {suffix}"), autoSave: true);
        var targetWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, $"WH Target {suffix}"), autoSave: true);

        var stockAccount = await accountRepository.InsertAsync(
            new Accounting.Entities.Account(Guid.NewGuid(), company.Id, $"91{suffix}", "Test Stock", Accounting.AccountType.Asset), autoSave: true);
        var expenseAccount = await accountRepository.InsertAsync(
            new Accounting.Entities.Account(Guid.NewGuid(), company.Id, $"92{suffix}", "Test Expense", Accounting.AccountType.Expense), autoSave: true);
        var adjustmentAccount = await accountRepository.InsertAsync(
            new Accounting.Entities.Account(Guid.NewGuid(), company.Id, $"93{suffix}", "Test Stock Adjustment", Accounting.AccountType.Equity), autoSave: true);
        var costCenter = await costCenterRepository.InsertAsync(
            new Accounting.Entities.CostCenter(Guid.NewGuid(), company.Id, $"Test CC {suffix}"), autoSave: true);

        company.DefaultInventoryAccountId = stockAccount.Id;
        company.DefaultExpenseAccountId = expenseAccount.Id;
        company.DefaultStockAdjustmentAccountId = adjustmentAccount.Id;
        company.DefaultCostCenterId = costCenter.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        await fiscalYearRepository.InsertAsync(
            new Accounting.Entities.FiscalYear(Guid.NewGuid(), company.Id, $"FY-{suffix}", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
            autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, $"SE Series {suffix}", "StockEntry", $"SE{suffix}-"), autoSave: true);

        var mr = new MaterialRequest(Guid.NewGuid(), company.Id, $"MR-{suffix}", MaterialRequestType.MaterialTransfer, DateTime.Today);
        mr.AddItem(item.Id, item.ItemName, mrQty, "Unit", warehouseId: sourceWarehouse.Id);
        mr.Submit();
        await mrRepository.InsertAsync(mr, autoSave: true);

        return (company, item, sourceWarehouse.Id, targetWarehouse.Id, mr, mr.Items[0].Id);
    }
}
