using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

public abstract class MrpLeadTimeAndOrderCreationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public void ItemLeadTime_ManufactureLeadTime_PreservesFractionalDays_PR59007()
    {
        // Per ERPNext PR #59007 (commit 2ad4a8c4a4):
        // test_manufacture_lead_time_preserves_fractional_days:
        // manufacturing_time_in_mins = 7, buffer_time = 2
        // Lead time must retain fractional days: 7.0 / 1440.0 + 2 = 2.00486111...
        var itemLeadTime = new ItemLeadTime(
            Guid.NewGuid(),
            Guid.NewGuid(),
            manufacturingTimeInMins: 7,
            bufferTimeDays: 2);

        var leadTime = itemLeadTime.GetLeadTimeInDays(qty: 1, isManufacture: true);
        leadTime.ShouldBe(7.0 / 1440.0 + 2.0, tolerance: 0.00000001);

        // Ceiling rounding
        var roundedDays = itemLeadTime.CalculateLeadTimeDays(qty: 1, isManufacture: true);
        roundedDays.ShouldBe(3);
    }

    [Theory]
    [InlineData(30, 48, 0, 1)]
    [InlineData(30, 49, 0, 2)]
    [InlineData(30, 96, 0, 2)]
    [InlineData(31, 47, 0, 2)]
    [InlineData(3000, 1, 0, 3)]
    [InlineData(30, 0.5, 1, 2)]
    [InlineData(30, 0, 1, 0)]
    public void ItemLeadTime_ManufacturingDuration_Boundaries_PR59007(
        int minutes, decimal qty, int bufferDays, int expectedDays)
    {
        // Per ERPNext PR #59007 test_manufacturing_duration_boundaries_and_missing_operation_time
        var itemLeadTime = new ItemLeadTime(
            Guid.NewGuid(),
            Guid.NewGuid(),
            manufacturingTimeInMins: minutes,
            bufferTimeDays: bufferDays);

        var days = itemLeadTime.CalculateLeadTimeDays(qty, isManufacture: true);
        days.ShouldBe(expectedDays);
    }

    [Fact]
    public async Task CreateOrdersAsync_ManufacturedItemWithoutBom_ThrowsValidationFailed_PR58510_PR58511()
    {
        // Per ERPNext PR #58510 & PR #58511:
        // When user makes work order from MRP row marked as Manufacture without a BOM,
        // it must throw validation error stating default BOM not found.
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var mrpAppService = GetRequiredService<IMaterialRequirementsPlanningAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MRP Co 1"), autoSave: true);
            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MRP-FG-NO-BOM", "FG No BOM", ItemType.Goods), autoSave: true);

            var input = new CreateOrdersFromMrpInput
            {
                CompanyId = company.Id,
                SelectedRows = new List<MrpOrderRowInputDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemCode = fgItem.ItemCode,
                        ItemName = fgItem.ItemName,
                        TypeOfMaterial = "Manufacture",
                        BomId = null,
                        Quantity = 10,
                        DeliveryDate = DateTime.UtcNow.AddDays(30),
                        ReleaseDate = DateTime.UtcNow.AddDays(25)
                    }
                }
            };

            var ex = await Should.ThrowAsync<BusinessException>(() => mrpAppService.CreateOrdersAsync(input));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Default BOM for MRP-FG-NO-BOM not found");
        });
    }

    [Fact]
    public async Task CreateOrdersAsync_CreatesDraftWorkOrder_And_PurchaseOrder_Successfully()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var mrpAppService = GetRequiredService<IMaterialRequirementsPlanningAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MRP Co 2"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "WO Series", "WO", "WO-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PO Series", "PurchaseOrder", "PO-"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), company.Id, "MRP Supplier 1") { SupplierCode = "SUP-MRP-1" }, autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MRP-FG-001", "FG Item 1", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MRP-RM-001", "RM Item 1", ItemType.Goods) { StandardBuyingPrice = 15m }, autoSave: true);

            var bom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-MRP-001", fgItem.Id)
            {
                Quantity = 1,
                IsActive = true,
                IsDefault = true
            };
            bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmItem.Id, rmItem.ItemName, 2, 15m));
            await bomRepo.InsertAsync(bom, autoSave: true);

            var deliveryDate = DateTime.UtcNow.Date.AddDays(30);
            var releaseDate = DateTime.UtcNow.Date.AddDays(20);

            var input = new CreateOrdersFromMrpInput
            {
                CompanyId = company.Id,
                SelectedRows = new List<MrpOrderRowInputDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemCode = fgItem.ItemCode,
                        ItemName = fgItem.ItemName,
                        TypeOfMaterial = "Manufacture",
                        BomId = bom.Id,
                        Quantity = 5,
                        DeliveryDate = deliveryDate,
                        ReleaseDate = releaseDate
                    },
                    new()
                    {
                        ItemId = rmItem.Id,
                        ItemCode = rmItem.ItemCode,
                        ItemName = rmItem.ItemName,
                        TypeOfMaterial = "Purchase",
                        DefaultSupplierId = supplier.Id,
                        Quantity = 20,
                        DeliveryDate = releaseDate,
                        ReleaseDate = releaseDate.AddDays(-5)
                    }
                }
            };

            var result = await mrpAppService.CreateOrdersAsync(input);

            result.WorkOrdersCount.ShouldBe(1);
            result.PurchaseOrdersCount.ShouldBe(1);

            var wo = await woRepo.GetAsync(result.WorkOrderIds[0]);
            wo.ItemId.ShouldBe(fgItem.Id);
            wo.BomId.ShouldBe(bom.Id);
            wo.Quantity.ShouldBe(5);
            wo.PlannedStartDate?.Date.ShouldBe(releaseDate);
            wo.PlannedEndDate?.Date.ShouldBe(deliveryDate);

            var po = await poRepo.GetAsync(result.PurchaseOrderIds[0]);
            po.SupplierId.ShouldBe(supplier.Id);
            po.Items.Count.ShouldBe(1);
            po.Items[0].ItemId.ShouldBe(rmItem.Id);
            po.Items[0].Quantity.ShouldBe(20);
        });
    }

    [Fact]
    public async Task CreateOrdersAsync_PurchaseRowMissingSupplier_ThrowsValidationFailed_PR59349()
    {
        // Per ERPNext PR #59349 (commit 43913d5c2a):
        // If an item in purchase rows has no configured supplier (no row override, no item default, no item group default),
        // throw BusinessException(ValidationFailed) with "Default Supplier for {items} not found".
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var mrpAppService = GetRequiredService<IMaterialRequirementsPlanningAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MRP Co Missing Sup"), autoSave: true);
            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MRP-NO-SUP-01", "Item With No Supplier", ItemType.Goods), autoSave: true);

            var input = new CreateOrdersFromMrpInput
            {
                CompanyId = company.Id,
                SelectedRows = new List<MrpOrderRowInputDto>
                {
                    new()
                    {
                        ItemId = item.Id,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName,
                        TypeOfMaterial = "Purchase",
                        DefaultSupplierId = null,
                        Quantity = 10,
                        DeliveryDate = DateTime.UtcNow.Date.AddDays(15),
                        ReleaseDate = DateTime.UtcNow.Date.AddDays(10)
                    }
                }
            };

            var ex = await Should.ThrowAsync<BusinessException>(() => mrpAppService.CreateOrdersAsync(input));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Default Supplier for MRP-NO-SUP-01 not found");
        });
    }

    [Fact]
    public async Task CreateOrdersAsync_PurchaseRowFallsBackToItemGroupSupplier_PR59349()
    {
        // Per ERPNext PR #59349 (commit 43913d5c2a):
        // Fall back to ItemGroup.DefaultSupplierId if row and item default have no supplier.
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var itemGroupRepo = GetRequiredService<IRepository<ItemGroup, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var mrpAppService = GetRequiredService<IMaterialRequirementsPlanningAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MRP Co Group Sup"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PO Series 3", "PurchaseOrder", "PO-"), autoSave: true);

            var groupSupplier = await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), company.Id, "Group Fallback Supplier"), autoSave: true);

            var group = await itemGroupRepo.InsertAsync(
                new ItemGroup(Guid.NewGuid(), "Hardware Group", false)
                {
                    DefaultSupplierId = groupSupplier.Id
                }, autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MRP-GRP-SUP-01", "Group Sourced Item", ItemType.Goods)
                {
                    ItemGroupId = group.Id,
                    StandardBuyingPrice = 25m
                }, autoSave: true);

            var deliveryDate = DateTime.UtcNow.Date.AddDays(20);
            var releaseDate = DateTime.UtcNow.Date.AddDays(15);

            var input = new CreateOrdersFromMrpInput
            {
                CompanyId = company.Id,
                SelectedRows = new List<MrpOrderRowInputDto>
                {
                    new()
                    {
                        ItemId = item.Id,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName,
                        TypeOfMaterial = "Purchase",
                        DefaultSupplierId = null, // No row supplier
                        Quantity = 15,
                        DeliveryDate = deliveryDate,
                        ReleaseDate = releaseDate
                    }
                }
            };

            var result = await mrpAppService.CreateOrdersAsync(input);
            result.PurchaseOrdersCount.ShouldBe(1);

            var po = await poRepo.GetAsync(result.PurchaseOrderIds[0]);
            po.SupplierId.ShouldBe(groupSupplier.Id);
            po.Items.Count.ShouldBe(1);
            po.Items[0].ItemId.ShouldBe(item.Id);
            po.Items[0].Quantity.ShouldBe(15);
        });
    }

    [Fact]
    public async Task GetReportAsync_PurchaseLeadTime_UsesSupplierLeadTimeAndItemFallback_e03a0ac44b_1cae8b6982()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemDefaultRepo = GetRequiredService<IRepository<ItemDefault, Guid>>();
            var itemLeadTimeRepo = GetRequiredService<IRepository<ItemLeadTime, Guid>>();
            var mpsRepo = GetRequiredService<IRepository<MasterProductionSchedule, Guid>>();
            var mrpAppService = GetRequiredService<IMaterialRequirementsPlanningAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MRP Lead Co"), autoSave: true);
            var supA = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Sup A") { SupplierCode = "SUP-A" }, autoSave: true);
            var supB = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Sup B") { SupplierCode = "SUP-B" }, autoSave: true);

            // RM-A: ItemLeadTime has default supplier supA (20+1 = 21d), item LeadTimeDays = 4
            var rmA = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MRP-LT-RM-A", "RM A", ItemType.Goods) { LeadTimeDays = 4, MaintainStock = true }, autoSave: true);
            var ltA = new ItemLeadTime(Guid.NewGuid(), rmA.Id, purchaseTimeDays: 2, bufferTimeDays: 0);
            ltA.AddSupplier(supA.Id, purchaseTimeDays: 20, bufferTimeDays: 1, isDefault: true);
            await itemLeadTimeRepo.InsertAsync(ltA, autoSave: true);

            // RM-B: No ItemLeadTime -> falls back to Item.LeadTimeDays (9d)
            var rmB = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MRP-LT-RM-B", "RM B", ItemType.Goods) { LeadTimeDays = 9, MaintainStock = true }, autoSave: true);

            // RM-C: ItemDefault selects supB; ItemLeadTime has item purchase_time=3 and non-default supA (20d) + supB (10+1=11d)
            var rmC = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MRP-LT-RM-C", "RM C", ItemType.Goods) { LeadTimeDays = 2, MaintainStock = true }, autoSave: true);
            await itemDefaultRepo.InsertAsync(new ItemDefault(Guid.NewGuid(), rmC.Id, company.Id) { DefaultSupplierId = supB.Id }, autoSave: true);
            var ltC = new ItemLeadTime(Guid.NewGuid(), rmC.Id, purchaseTimeDays: 3, bufferTimeDays: 0);
            ltC.AddSupplier(supA.Id, purchaseTimeDays: 20, bufferTimeDays: 0, isDefault: false);
            ltC.AddSupplier(supB.Id, purchaseTimeDays: 10, bufferTimeDays: 1, isDefault: false);
            await itemLeadTimeRepo.InsertAsync(ltC, autoSave: true);

            var deliveryDate = new DateTime(2026, 12, 15);
            var mps = new MasterProductionSchedule(Guid.NewGuid(), company.Id, "MPS-LT-01", new DateTime(2026, 12, 1), new DateTime(2026, 12, 1));
            mps.SetItems(new[]
            {
                new MasterProductionScheduleItem(Guid.NewGuid(), mps.Id, rmA.Id, rmA.ItemName, deliveryDate, 10m, 0),
                new MasterProductionScheduleItem(Guid.NewGuid(), mps.Id, rmB.Id, rmB.ItemName, deliveryDate, 10m, 0),
                new MasterProductionScheduleItem(Guid.NewGuid(), mps.Id, rmC.Id, rmC.ItemName, deliveryDate, 10m, 0),
            });
            mps.Submit();
            await mpsRepo.InsertAsync(mps, autoSave: true);

            var report = await mrpAppService.GetReportAsync(new MaterialRequirementsPlanningFilterDto
            {
                CompanyId = company.Id,
                FromDate = new DateTime(2026, 12, 1),
                ToDate = new DateTime(2026, 12, 31),
                BucketSize = MrpBucketSize.Monthly
            });

            var reqA = report.Requirements.Single(r => r.ItemId == rmA.Id);
            var reqB = report.Requirements.Single(r => r.ItemId == rmB.Id);
            var reqC = report.Requirements.Single(r => r.ItemId == rmC.Id);

            reqA.LeadTimeDays.ShouldBe(21);
            reqB.LeadTimeDays.ShouldBe(9);
            reqC.DefaultSupplierId.ShouldBe(supB.Id);
            reqC.LeadTimeDays.ShouldBe(11);
        });
    }

    [Fact]
    public async Task CreateBomFromWorkOrderAsync_IncludesMaterialConsumptionEntries_82153f0458()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var seRepo = GetRequiredService<IRepository<StockEntry, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "BOM From WO MC Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "BOM Series", "BOM", "BOM-"), autoSave: true);

            var wipWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WIP-MC"), autoSave: true);
            var fgWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG-MC"), autoSave: true);
            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-BOM-MC", "FG BOM MC", ItemType.Goods), autoSave: true);
            var rm1 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-BOM-MC-1", "RM Consumed Early", ItemType.Goods), autoSave: true);
            var rm2 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-BOM-MC-2", "RM Consumed At Mfg", ItemType.Goods), autoSave: true);

            var srcBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-SRC-MC", fgItem.Id)
            {
                Quantity = 1,
                IsActive = true,
                IsDefault = true
            };
            srcBom.Items.Add(new BomItem(Guid.NewGuid(), srcBom.Id, rm1.Id, rm1.ItemName, 2, 10m));
            srcBom.Items.Add(new BomItem(Guid.NewGuid(), srcBom.Id, rm2.Id, rm2.ItemName, 3, 20m));
            await bomRepo.InsertAsync(srcBom, autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-BOM-MC", fgItem.Id, srcBom.Id, 2m)
            {
                WipWarehouseId = wipWh.Id,
                FgWarehouseId = fgWh.Id
            };
            wo.Submit();
            wo.Start();
            wo.RecordProduction(2m);
            await woRepo.InsertAsync(wo, autoSave: true);

            // Posted MaterialConsumptionForManufacture entry consuming RM-1 (qty 4)
            var mcSe = new StockEntry(Guid.NewGuid(), company.Id, StockEntryType.MaterialConsumptionForManufacture, DateTime.UtcNow.Date)
            {
                EntryNumber = "SE-MC-01",
                WorkOrderId = wo.Id
            };
            mcSe.AddItem(rm1.Id, 4m, sourceWarehouseId: wipWh.Id, targetWarehouseId: null, valuationRate: 12m);
            mcSe.Submit();
            mcSe.Post();
            await seRepo.InsertAsync(mcSe, autoSave: true);

            // Posted Manufacture entry consuming RM-2 (qty 6) and producing FG (qty 2)
            var mfgSe = new StockEntry(Guid.NewGuid(), company.Id, StockEntryType.Manufacture, DateTime.UtcNow.Date)
            {
                EntryNumber = "SE-MFG-01",
                WorkOrderId = wo.Id
            };
            mfgSe.AddItem(rm2.Id, 6m, sourceWarehouseId: wipWh.Id, targetWarehouseId: null, valuationRate: 25m);
            mfgSe.AddItem(fgItem.Id, 2m, sourceWarehouseId: null, targetWarehouseId: fgWh.Id, valuationRate: 100m, isFinishedItem: true);
            mfgSe.Submit();
            mfgSe.Post();
            await seRepo.InsertAsync(mfgSe, autoSave: true);

            var newBom = await mfgAppService.CreateBomFromWorkOrderAsync(wo.Id);
            newBom.Items.Count.ShouldBe(2);
            newBom.Items.Single(i => i.ItemId == rm1.Id).Quantity.ShouldBe(4m);
            newBom.Items.Single(i => i.ItemId == rm2.Id).Quantity.ShouldBe(6m);
        });
    }
}
