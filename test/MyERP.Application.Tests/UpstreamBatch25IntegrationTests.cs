using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.DomainServices;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 25 upstream parity:
/// 1) Pick different serial numbers across separate return rows against the same original row (PR #64590).
/// 2) Subcontracting return raw materials at consumed rate of batches/serials in bundle (PR #64861).
/// 3) Production Plan Download Required Materials shows UOM and merges transfer/purchase rows (PR #65049).
/// </summary>
public abstract class UpstreamBatch25IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SerialBatchBundle_DepletionAcrossReturnRows_PicksDifferentSerialsAndRemainingBatches()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Batch 25 Return Co") { CurrencyCode = "MYR" },
                autoSave: true);

            var warehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Stores B25"),
                autoSave: true);

            var batch1 = Guid.NewGuid();
            var batch2 = Guid.NewGuid();

            // 1) Serialized items: row 1 picks SN-A (qty 1), row 2 picks SN-B (qty 1)
            var serialPool = new ReturnSerialBatchPool(
                serialNos: new[] { "SN-A", "SN-B" });

            var serialPick1 = SerialBatchBundleValidationService.PickSerialBatchesForReturn(
                serialPool, requiredQty: 1m, warehouseId: warehouse.Id);

            serialPick1.SerialNos.Count.ShouldBe(1);
            serialPick1.SerialNos[0].ShouldBe("SN-A");
            serialPool.SerialNos.ShouldContain("SN-B");
            serialPool.SerialNos.ShouldNotContain("SN-A");

            var serialPick2 = SerialBatchBundleValidationService.PickSerialBatchesForReturn(
                serialPool, requiredQty: 1m, warehouseId: warehouse.Id);

            serialPick2.SerialNos.Count.ShouldBe(1);
            serialPick2.SerialNos[0].ShouldBe("SN-B");
            serialPool.SerialNos.ShouldBeEmpty();

            // 2) Batched items: row 1 picks 3 (from batch1), row 2 picks 7 (remaining 2 from batch1 + 5 from batch2)
            var batchPool = new ReturnSerialBatchPool(
                batches: new Dictionary<Guid, decimal>
                {
                    [batch1] = 5m,
                    [batch2] = 5m
                });

            var availableStock = new Dictionary<Guid, decimal>
            {
                [batch1] = 10m,
                [batch2] = 10m
            };

            var batchPick1 = SerialBatchBundleValidationService.PickSerialBatchesForReturn(
                batchPool, requiredQty: 3m, warehouseId: warehouse.Id, availableBatchQtyInWh: availableStock);

            batchPick1.Batches.Count.ShouldBe(1);
            batchPick1.Batches[batch1].ShouldBe(3m);
            batchPool.Batches[batch1].ShouldBe(2m);
            batchPool.Batches[batch2].ShouldBe(5m);

            var batchPick2 = SerialBatchBundleValidationService.PickSerialBatchesForReturn(
                batchPool, requiredQty: 7m, warehouseId: warehouse.Id, availableBatchQtyInWh: availableStock);

            batchPick2.Batches[batch1].ShouldBe(2m);
            batchPick2.Batches[batch2].ShouldBe(5m);
            batchPool.Batches.ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task Subcontracting_SuppliedItemReturnRate_CalculatesWeightedRateFromBundleEntries()
    {
        await WithUnitOfWorkAsync(() =>
        {
            var consumedRates = new Dictionary<(string? BatchNo, string? SerialNo), decimal>
            {
                [("B-01", null)] = 100m,
                [("B-02", null)] = 300m
            };

            // Return bundle with 2 units of B-01 and 8 units of B-02
            // Weighted average rate = (2 * 100 + 8 * 300) / 10 = (200 + 2400) / 10 = 260
            var returnedEntries = new List<(string? BatchNo, string? SerialNo, decimal Qty)>
            {
                ("B-01", null, 2m),
                ("B-02", null, 8m)
            };

            var weightedRate = SubcontractingManager.GetSerialBatchReturnRate(returnedEntries, consumedRates);
            weightedRate.ShouldNotBeNull();
            weightedRate.Value.ShouldBe(260m);

            var finalSuppliedRate = SubcontractingManager.CalculateSuppliedItemRate(
                isReturn: true,
                serialBatchConsumedRate: weightedRate,
                originalConsumedRate: 200m,
                standardCostRate: 50m);

            finalSuppliedRate.ShouldBe(260m);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ProductionPlanAppService_DownloadRawMaterials_ReturnsUomAndCalculatedRows()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var planRepo = GetRequiredService<IRepository<ProductionPlan, Guid>>();
            var ppAppService = GetRequiredService<IProductionPlanAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "PP Download Co B25") { CurrencyCode = "MYR" },
                autoSave: true);

            var warehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Stores PP B25"),
                autoSave: true);

            var rmItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-B25-01", "Raw Material B25", ItemType.Goods)
                {
                    Uom = "Nos"
                },
                autoSave: true);

            var plan = new ProductionPlan(
                Guid.NewGuid(), company.Id, "PP-B25-001", DateTime.UtcNow);

            var mrItem1 = new ProductionPlanMrItem(
                Guid.NewGuid(), plan.Id, rmItem.Id, rmItem.ItemName, requiredQty: 20m)
            {
                Uom = "Nos",
                WarehouseId = warehouse.Id,
                PlannedQty = 20m
            };

            plan.AddMaterialRequirement(mrItem1);
            await planRepo.InsertAsync(plan, autoSave: true);

            var downloadRows = await ppAppService.DownloadRawMaterialsAsync(plan.Id);

            downloadRows.ShouldNotBeNull();
            downloadRows.Count.ShouldBe(1);
            downloadRows[0].ItemCode.ShouldBe("RM-B25-01");
            downloadRows[0].Uom.ShouldBe("Nos");
            downloadRows[0].RequiredQty.ShouldBe(20m);
            downloadRows[0].Warehouse.ShouldBe("Stores PP B25");
        });
    }
}
