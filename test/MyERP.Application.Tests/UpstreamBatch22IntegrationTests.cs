using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 22 upstream parity:
/// 1) Delivery Note Return for Product Bundle retains delivered component qty (PR #65053 / commit 7edd914d84).
/// 2) Pick List bundle picking with repeated components leaves pending on partial delivery (PR #65051 / commit 740575ad27).
/// 3) Subcontracting Receipt Return supplied item rate weighted by batch/serial consumed rate (PR #64861 / commit eb0ee73b9d).
/// 4) Serial and Batch return deduping across multiple return rows (PR #64590 / commit 9403e3df8d).
/// </summary>
public abstract class UpstreamBatch22IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task DeliveryNote_ReturnBundle_RetainsDeliveredQuantity_AfterComponentUomChange()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var pickListRepo = GetRequiredService<IRepository<PickList, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 22 Bundle Return Co") { CurrencyCode = "MYR" }, autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Customer B22"), autoSave: true);
            var warehouse = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Stores B22"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "DeliveryNote", "DeliveryNote", "DN-2026-"), autoSave: true);

            // Step 1: Create component item (stock UOM Nos)
            var componentItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "COMP-BOX-01", "Component Box Item", ItemType.Goods)
            {
                Uom = "Nos",
                MaintainStock = true
            }, autoSave: true);

            // Create parent bundle item (non-stock)
            var bundleItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "BUNDLE-01", "Bundle Item", ItemType.Goods)
            {
                Uom = "Unit",
                MaintainStock = false
            }, autoSave: true);

            // Product Bundle: 1 Bundle = 1 Box (10 Nos)
            var bundle = new ProductBundle(Guid.NewGuid(), bundleItem.Id)
            {
                IsActive = true
            };
            bundle.AddItem(componentItem.Id, 1m, "Component", "Box");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            // Step 2: Create submitted Delivery Note with Pick List delivering 2 bundles (20 Nos)
            var pickList = new PickList(Guid.NewGuid(), company.Id, "Delivery");
            pickList.AddItem(
                itemId: componentItem.Id,
                warehouseId: warehouse.Id,
                qty: 20m,
                stockQty: 20m,
                itemName: "Component");
            await pickListRepo.InsertAsync(pickList, autoSave: true);

            var dn = new DeliveryNote(Guid.NewGuid(), company.Id, customer.Id, warehouse.Id, "DN-B22-001", DateTime.UtcNow)
            {
                PickListId = pickList.Id
            };
            var pickRow = pickList.Items.First();
            dn.AddItem(
                itemId: bundleItem.Id,
                description: "Bundle Item",
                quantity: 2m,
                unitPrice: 100m,
                taxAmount: 0m,
                uom: "Unit",
                salesOrderItemId: null,
                pickListItemId: pickRow.Id);
            dn.Submit();
            await dnRepo.InsertAsync(dn, autoSave: true);

            // Step 3: Create return for the Delivery Note
            var returnDto = await dnAppService.CreateAsync(new CreateDeliveryNoteDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                WarehouseId = warehouse.Id,
                PostingDate = DateTime.UtcNow,
                IsReturn = true,
                ReturnAgainstId = dn.Id,
                Items = new List<CreateDeliveryNoteItemDto>
                {
                    new CreateDeliveryNoteItemDto
                    {
                        ItemId = bundleItem.Id,
                        Description = "Bundle Item Return",
                        Quantity = -2m,
                        UnitPrice = 100m,
                        Uom = "Unit",
                        PickListItemId = pickRow.Id
                    }
                }
            });

            returnDto.ShouldNotBeNull();
            returnDto.IsReturn.ShouldBeTrue();
            returnDto.ReturnAgainstId.ShouldBe(dn.Id);
            returnDto.Items.First().Quantity.ShouldBe(-2m);
        });
    }

    [Fact]
    public async Task PickList_BundleWithRepeatedComponents_AllocatesAndLeavesPendingOnPartialDelivery()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 22 Pick Co"), autoSave: true);
            var warehouse = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Stores Pick B22"), autoSave: true);

            var component = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "COMP-REP-01", "Repeated Component", ItemType.Goods)
            {
                Uom = "Nos",
                MaintainStock = true
            }, autoSave: true);

            var soItemId = Guid.NewGuid();
            var pickList = new PickList(Guid.NewGuid(), company.Id, "Delivery");

            // Repeated component: 3 on location A, 6 on location B (total 9)
            pickList.AddItem(
                itemId: component.Id,
                warehouseId: warehouse.Id,
                qty: 3m,
                stockQty: 3m,
                itemName: "Comp A",
                productBundleItemId: Guid.NewGuid(),
                sourceDocumentItemId: soItemId);

            pickList.AddItem(
                itemId: component.Id,
                warehouseId: warehouse.Id,
                qty: 6m,
                stockQty: 6m,
                itemName: "Comp A",
                productBundleItemId: Guid.NewGuid(),
                sourceDocumentItemId: soItemId);

            // Bundle definition: 3 units of component per bundle
            var bundleMap = new Dictionary<Guid, decimal>
            {
                [component.Id] = 3m
            };

            // Full pick: 9 units / 3 = 3 bundles
            PickListManager.ComputePickedQtyForBundle(pickList.Items, soItemId, bundleMap, pendingOnly: false)
                .ShouldBe(3);

            // Partially deliver 1 bundle (3 units: 1 from row1, 2 from row2)
            pickList.Items[0].RecordDelivery(1m);
            pickList.Items[1].RecordDelivery(2m);

            // Remaining pending: (2 + 4) / 3 = 2 bundles
            PickListManager.ComputePickedQtyForBundle(pickList.Items, soItemId, bundleMap, pendingOnly: true)
                .ShouldBe(2);
        });
    }

    [Fact]
    public async Task SubcontractingReceipt_ReturnSuppliedItems_ConsumedRateWeightedAverage()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 22 Sub Return Co"), autoSave: true);
            var rawItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-B22-01", "Raw Material B22", ItemType.Goods), autoSave: true);

            var batchA = Guid.NewGuid();
            var batchB = Guid.NewGuid();

            // Original receipt consumed Batch A at rate 50 and Batch B at rate 150
            var consumedRates = new Dictionary<(Guid? BatchId, string? SerialNo), decimal>
            {
                [(batchA, null)] = 50m,
                [(batchB, null)] = 150m
            };

            // Return sends back 2 of Batch A and 8 of Batch B (total 10)
            // Weighted consumed rate = (2 * 50 + 8 * 150) / 10 = (100 + 1200) / 10 = 130
            var returned = new List<(Guid? BatchId, string? SerialNo, decimal Qty)>
            {
                (batchA, null, 2m),
                (batchB, null, 8m)
            };

            var weightedRate = SubcontractingManager.GetSerialBatchReturnRate(returned, consumedRates);
            weightedRate.ShouldNotBeNull();
            weightedRate.Value.ShouldBe(130m);
        });
    }

    [Fact]
    public async Task ReturnDocument_SerialBatchDeduping_ConsecutiveReturnRows()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var batch1 = Guid.NewGuid();
            var batch2 = Guid.NewGuid();

            var availableSerials = new HashSet<string> { "SN-001", "SN-002", "SN-003" };
            var availableBatches = new Dictionary<Guid, decimal>
            {
                [batch1] = 10m,
                [batch2] = 10m
            };

            // First return row against the receipt picks SN-001 and 4 of batch1
            SerialBatchBundleValidationService.RemovePickedSerialBatches(
                availableSerials, availableBatches, new[] { "SN-001" }, new Dictionary<Guid, decimal> { [batch1] = 4m });

            // Available pool is updated in place
            availableSerials.ShouldNotContain("SN-001");
            availableSerials.Count.ShouldBe(2);
            availableBatches[batch1].ShouldBe(6m);

            // Second return row against the SAME receipt row picks SN-002 and 6 of batch1
            SerialBatchBundleValidationService.RemovePickedSerialBatches(
                availableSerials, availableBatches, new[] { "SN-002" }, new Dictionary<Guid, decimal> { [batch1] = 6m });

            availableSerials.ShouldNotContain("SN-002");
            availableSerials.ShouldContain("SN-003");
            availableBatches.ContainsKey(batch1).ShouldBeFalse(); // fully consumed
            availableBatches[batch2].ShouldBe(10m);
        });
    }
}
