using System;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Dtos;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for a gap found via ERPNext validate() parity: ERPNext's
/// stock_reconciliation.py validate_reserved_stock() blocks submitting (or cancelling) a
/// reconciliation row that changes an item/warehouse's on-hand qty while a Stock Reservation Entry
/// is still outstanding against that same item/warehouse — otherwise the reservation's basis is
/// silently invalidated. StockReconciliationAppService had no such check at all (unlike Pick List's
/// analogous, but voucher-scoped, SRE conflict check per gotcha #3533).
/// </summary>
public abstract class StockReconciliationReservationGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private async Task<(Company company, MyERP.Inventory.Entities.Warehouse warehouse, MyERP.Inventory.Entities.Item item, Account expenseAccount)> SeedCompanyAsync(string suffix)
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var warehouseRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
        var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), $"SR SRE Guard Co {suffix}"), autoSave: true);
        await fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, $"FY-SRSRE-{suffix}", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
            autoSave: true);
        await seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, $"JE Series SRSRE {suffix}", "JE", $"JESRSRE{suffix}-"), autoSave: true);

        var stockAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"91SRSRE{suffix}", "Test Stock", AccountType.Asset), autoSave: true);
        var expenseAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"93SRSRE{suffix}", "Test Stock Adjustment", AccountType.Equity), autoSave: true);

        var item = await itemRepository.InsertAsync(
            new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, $"SRSRE-ITEM-{suffix}", $"SR SRE Guard Item {suffix}", MyERP.Inventory.ItemType.Goods),
            autoSave: true);
        var warehouse = await warehouseRepository.InsertAsync(
            new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, $"SR SRE Guard WH {suffix}"), autoSave: true);
        warehouse.DefaultAccountId = stockAccount.Id;
        await warehouseRepository.UpdateAsync(warehouse, autoSave: true);

        return (company, warehouse, item, expenseAccount);
    }

    private async Task<StockReservationEntry> InsertActiveReservationAsync(
        Company company, MyERP.Inventory.Entities.Item item, MyERP.Inventory.Entities.Warehouse warehouse, decimal reservedQty)
    {
        var sreRepository = GetRequiredService<IRepository<StockReservationEntry, Guid>>();
        var sre = new StockReservationEntry(Guid.NewGuid(), company.Id, item.Id, warehouse.Id,
            "SalesOrder", Guid.NewGuid(), reservedQty);
        sre.Submit();
        return await sreRepository.InsertAsync(sre, autoSave: true);
    }

    [Fact]
    public async Task SubmitAsync_QuantityUnchangedRateChanged_WithActiveReservation_Succeeds()
    {
        var (company, warehouse, item, expenseAccount) = await SeedCompanyAsync("A");
        await InsertActiveReservationAsync(company, item, warehouse, reservedQty: 5m);

        var srAppService = GetRequiredService<IStockReconciliationAppService>();
        var created = await srAppService.CreateAsync(new CreateStockReconciliationDto
        {
            CompanyId = company.Id,
            PostingDate = DateTime.UtcNow.Date,
            Purpose = "Stock Reconciliation",
            ExpenseAccountId = expenseAccount.Id,
            Items = new[]
            {
                new CreateStockReconciliationItemDto
                {
                    ItemId = item.Id, WarehouseId = warehouse.Id,
                    CurrentQuantity = 10m, CurrentValuationRate = 5m,
                    NewQuantity = 10m, NewValuationRate = 8m,
                }
            }
        });

        var submitted = await srAppService.SubmitAsync(created.Id);
        submitted.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public async Task SubmitAsync_QuantityChanged_WithActiveReservationSameItemWarehouse_Throws()
    {
        var (company, warehouse, item, expenseAccount) = await SeedCompanyAsync("B");
        await InsertActiveReservationAsync(company, item, warehouse, reservedQty: 5m);

        var srAppService = GetRequiredService<IStockReconciliationAppService>();
        var created = await srAppService.CreateAsync(new CreateStockReconciliationDto
        {
            CompanyId = company.Id,
            PostingDate = DateTime.UtcNow.Date,
            Purpose = "Stock Reconciliation",
            ExpenseAccountId = expenseAccount.Id,
            Items = new[]
            {
                new CreateStockReconciliationItemDto
                {
                    ItemId = item.Id, WarehouseId = warehouse.Id,
                    CurrentQuantity = 10m, CurrentValuationRate = 5m,
                    NewQuantity = 15m, NewValuationRate = 5m,
                }
            }
        });

        var ex = await Should.ThrowAsync<BusinessException>(() => srAppService.SubmitAsync(created.Id));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.StockReconciliationActiveReservation);
    }

    [Fact]
    public async Task CancelAsync_QuantityChanged_WithActiveReservationSameItemWarehouse_Throws()
    {
        var (company, warehouse, item, expenseAccount) = await SeedCompanyAsync("C");

        var srAppService = GetRequiredService<IStockReconciliationAppService>();
        var created = await srAppService.CreateAsync(new CreateStockReconciliationDto
        {
            CompanyId = company.Id,
            PostingDate = DateTime.UtcNow.Date,
            Purpose = "Stock Reconciliation",
            ExpenseAccountId = expenseAccount.Id,
            Items = new[]
            {
                new CreateStockReconciliationItemDto
                {
                    ItemId = item.Id, WarehouseId = warehouse.Id,
                    CurrentQuantity = 10m, CurrentValuationRate = 5m,
                    NewQuantity = 15m, NewValuationRate = 5m,
                }
            }
        });

        // No reservation exists yet — submit succeeds.
        await srAppService.SubmitAsync(created.Id);

        // A reservation is placed against the same item/warehouse after submission; cancelling
        // the reconciliation would revert its balance and invalidate that reservation's basis.
        await InsertActiveReservationAsync(company, item, warehouse, reservedQty: 5m);

        var ex = await Should.ThrowAsync<BusinessException>(() => srAppService.CancelAsync(created.Id));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.StockReconciliationActiveReservation);
    }
}
