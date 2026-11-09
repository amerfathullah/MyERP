using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Dtos;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 13 upstream ERPNext parity:
/// 1) Stock Reconciliation GetStockBalanceForAsync validates company on warehouse and item (PR #64855 / commit 09e0da04e8).
/// 2) Available batches calculation skips reservations of unavailable / expired batches (PR #64860 / commits 6caabeb477, 73c5f3a6b4).
/// </summary>
public abstract class UpstreamBatch13IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task StockReconciliationAppService_GetStockBalanceForAsync_ForeignWarehouseCompany_ThrowsCompanyMismatch()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var recoService = GetRequiredService<IStockReconciliationAppService>();

            var company1 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Company 1") { CurrencyCode = "MYR" }, autoSave: true);
            var company2 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Company 2") { CurrencyCode = "MYR" }, autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company1.Id, "ITEM-RECO-01", "Reco Item", ItemType.Goods) { IsActive = true }, autoSave: true);
            var warehouseInCo2 = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company2.Id, "WH-CO2") { IsActive = true }, autoSave: true);

            // Attempt to get stock balance for Company 1 using Company 2's warehouse
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                recoService.GetStockBalanceForAsync(new GetStockBalanceForInputDto
                {
                    CompanyId = company1.Id,
                    ItemId = item.Id,
                    WarehouseId = warehouseInCo2.Id,
                    PostingDate = DateTime.Today
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.CompanyMismatch);
        });
    }

    [Fact]
    public async Task StockReconciliationAppService_GetStockBalanceForAsync_ForeignItemCompany_ThrowsCompanyMismatch()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var recoService = GetRequiredService<IStockReconciliationAppService>();

            var company1 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Company 1b") { CurrencyCode = "MYR" }, autoSave: true);
            var company2 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Company 2b") { CurrencyCode = "MYR" }, autoSave: true);

            var itemInCo2 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company2.Id, "ITEM-CO2", "Co2 Item", ItemType.Goods) { IsActive = true }, autoSave: true);
            var warehouseInCo1 = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company1.Id, "WH-CO1") { IsActive = true }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                recoService.GetStockBalanceForAsync(new GetStockBalanceForInputDto
                {
                    CompanyId = company1.Id,
                    ItemId = itemInCo2.Id,
                    WarehouseId = warehouseInCo1.Id,
                    PostingDate = DateTime.Today
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.CompanyMismatch);
        });
    }

    [Fact]
    public async Task StockReconciliationAppService_GetStockBalanceForAsync_GroupWarehouse_ThrowsGroupWarehouseCannotReceiveStock()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var recoService = GetRequiredService<IStockReconciliationAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Company Group WH") { CurrencyCode = "MYR" }, autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-GRP", "Grp Item", ItemType.Goods) { IsActive = true }, autoSave: true);
            var groupWarehouse = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WH-GROUP") { IsGroup = true, IsActive = true }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                recoService.GetStockBalanceForAsync(new GetStockBalanceForInputDto
                {
                    CompanyId = company.Id,
                    ItemId = item.Id,
                    WarehouseId = groupWarehouse.Id,
                    PostingDate = DateTime.Today
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock);
        });
    }

    [Fact]
    public async Task BatchAppService_GetAvailableBatchesAsync_ExpiredBatch_ExcludedFromAvailableList()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var batchRepo = GetRequiredService<IRepository<Batch, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var batchAppService = GetRequiredService<IBatchAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch Expired Co") { CurrencyCode = "MYR" }, autoSave: true);
            var warehouse = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WH-BATCH") { IsActive = true }, autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-BATCH", "Batch Item", ItemType.Goods)
            {
                HasBatchNo = true,
                IsActive = true
            }, autoSave: true);

            // 1) Active valid batch
            var activeBatch = await batchRepo.InsertAsync(new Batch(Guid.NewGuid(), item.Id, "BATCH-VALID-01")
            {
                ExpiryDate = DateTime.Today.AddDays(30)
            }, autoSave: true);

            var sleActive = new StockLedgerEntry(Guid.NewGuid(), company.Id, item.Id, warehouse.Id, DateTime.Today, 10m, 50m, 10m, 500m)
            {
                BatchId = activeBatch.Id
            };
            await sleRepo.InsertAsync(sleActive, autoSave: true);

            // 2) Expired batch (expiry date in past)
            var expiredBatch = await batchRepo.InsertAsync(new Batch(Guid.NewGuid(), item.Id, "BATCH-EXPIRED-01")
            {
                ExpiryDate = DateTime.Today.AddDays(-5)
            }, autoSave: true);

            var sleExpired = new StockLedgerEntry(Guid.NewGuid(), company.Id, item.Id, warehouse.Id, DateTime.Today.AddDays(-10), 5m, 50m, 5m, 250m)
            {
                BatchId = expiredBatch.Id
            };
            await sleRepo.InsertAsync(sleExpired, autoSave: true);

            // Query available batches
            var availableBatches = await batchAppService.GetAvailableBatchesAsync(new GetAvailableBatchesDto
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                WarehouseId = warehouse.Id
            });

            // Must include active batch but exclude expired batch
            availableBatches.ShouldContain(b => b.BatchId == activeBatch.Id && b.AvailableQuantity == 10m);
            availableBatches.ShouldNotContain(b => b.BatchId == expiredBatch.Id);
        });
    }
}
