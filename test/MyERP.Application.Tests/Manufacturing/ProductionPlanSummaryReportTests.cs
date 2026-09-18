using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

public abstract class ProductionPlanSummaryReportTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SummaryWithoutWorkOrder_YieldsPlannedQtyAsPending()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var planRepo = GetRequiredService<IRepository<ProductionPlan, Guid>>();
            var planAppService = GetRequiredService<IProductionPlanAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PP Summary Co 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-ITEM-1", "Finished Good 1", ItemType.Goods), autoSave: true);
            var bom = await bomRepo.InsertAsync(new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-FG-1", item.Id) { Quantity = 1, IsActive = true }, autoSave: true);

            var plan = new ProductionPlan(Guid.NewGuid(), company.Id, "PP-0001", DateTime.UtcNow);
            var planItem = new ProductionPlanItem(Guid.NewGuid(), plan.Id, item.Id, item.ItemName, bom.Id, 2m);
            plan.AddPlannedItem(planItem);
            plan.Submit();
            await planRepo.InsertAsync(plan, autoSave: true);

            var report = await planAppService.GetSummaryReportAsync(plan.Id);

            report.ShouldNotBeNull();
            report.Rows.Count.ShouldBe(1);

            var fgRow = report.Rows[0];
            fgRow.Indent.ShouldBe(0);
            fgRow.ItemCode.ShouldBe("FG-ITEM-1");
            fgRow.ItemName.ShouldBe("Finished Good 1");
            fgRow.Qty.ShouldBe(2m);
            fgRow.ProducedQty.ShouldBe(0m);
            fgRow.PendingQty.ShouldBe(2m);
            fgRow.DocumentType.ShouldBeNull();
            fgRow.DocumentName.ShouldBeNull();
        });
    }

    [Fact]
    public async Task SummaryWithPendingWorkOrder_ShowsFullQtyAsPendingAndWorkOrderRow()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var planRepo = GetRequiredService<IRepository<ProductionPlan, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var planAppService = GetRequiredService<IProductionPlanAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PP Summary Co 2"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-ITEM-2", "Finished Good 2", ItemType.Goods), autoSave: true);
            var bom = await bomRepo.InsertAsync(new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-FG-2", item.Id) { Quantity = 1, IsActive = true }, autoSave: true);

            var plan = new ProductionPlan(Guid.NewGuid(), company.Id, "PP-0002", DateTime.UtcNow);
            var planItem = new ProductionPlanItem(Guid.NewGuid(), plan.Id, item.Id, item.ItemName, bom.Id, 5m);
            plan.AddPlannedItem(planItem);
            plan.Submit();
            await planRepo.InsertAsync(plan, autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-0002", item.Id, bom.Id, 5m)
            {
                ProductionPlanId = plan.Id,
                ProductionPlanItemId = planItem.Id,
            };
            wo.Submit();
            await woRepo.InsertAsync(wo, autoSave: true);

            var report = await planAppService.GetSummaryReportAsync(plan.Id);

            report.Rows.Count.ShouldBe(2);

            var summaryRow = report.Rows.FirstOrDefault(r => r.DocumentType == null);
            summaryRow.ShouldNotBeNull();
            summaryRow.Qty.ShouldBe(5m);
            summaryRow.ProducedQty.ShouldBe(0m);
            summaryRow.PendingQty.ShouldBe(5m);

            var woRow = report.Rows.FirstOrDefault(r => r.DocumentType == "Work Order");
            woRow.ShouldNotBeNull();
            woRow.Indent.ShouldBe(1);
            woRow.DocumentName.ShouldBe("WO-0002");
            woRow.Qty.ShouldBe(5m);
            woRow.ProducedQty.ShouldBe(0m);
            woRow.PendingQty.ShouldBe(5m);
        });
    }

    [Fact]
    public async Task SummaryReflectsProducedQuantity_UpdatesPending()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var planRepo = GetRequiredService<IRepository<ProductionPlan, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var planAppService = GetRequiredService<IProductionPlanAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PP Summary Co 3"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-ITEM-3", "Finished Good 3", ItemType.Goods), autoSave: true);
            var bom = await bomRepo.InsertAsync(new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-FG-3", item.Id) { Quantity = 1, IsActive = true }, autoSave: true);

            var plan = new ProductionPlan(Guid.NewGuid(), company.Id, "PP-0003", DateTime.UtcNow);
            var planItem = new ProductionPlanItem(Guid.NewGuid(), plan.Id, item.Id, item.ItemName, bom.Id, 10m);
            plan.AddPlannedItem(planItem);
            plan.Submit();
            await planRepo.InsertAsync(plan, autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-0003", item.Id, bom.Id, 10m)
            {
                ProductionPlanId = plan.Id,
                ProductionPlanItemId = planItem.Id,
                ProducedQuantity = 4m
            };
            wo.Submit();
            await woRepo.InsertAsync(wo, autoSave: true);

            var report = await planAppService.GetSummaryReportAsync(plan.Id);

            var summaryRow = report.Rows.FirstOrDefault(r => r.DocumentType == null);
            summaryRow.ShouldNotBeNull();
            summaryRow.Qty.ShouldBe(10m);
            summaryRow.ProducedQty.ShouldBe(4m);
            summaryRow.PendingQty.ShouldBe(6m);

            var woRow = report.Rows.FirstOrDefault(r => r.DocumentType == "Work Order");
            woRow.ShouldNotBeNull();
            woRow.Qty.ShouldBe(10m);
            woRow.ProducedQty.ShouldBe(4m);
            woRow.PendingQty.ShouldBe(6m);
        });
    }

    [Fact]
    public async Task SummaryScopedToItsOwnPlan()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var planRepo = GetRequiredService<IRepository<ProductionPlan, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var planAppService = GetRequiredService<IProductionPlanAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PP Summary Co 4"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-ITEM-4", "Finished Good 4", ItemType.Goods), autoSave: true);
            var bom = await bomRepo.InsertAsync(new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-FG-4", item.Id) { Quantity = 1, IsActive = true }, autoSave: true);

            var planA = new ProductionPlan(Guid.NewGuid(), company.Id, "PP-0004A", DateTime.UtcNow);
            var planItemA = new ProductionPlanItem(Guid.NewGuid(), planA.Id, item.Id, item.ItemName, bom.Id, 2m);
            planA.AddPlannedItem(planItemA);
            planA.Submit();
            await planRepo.InsertAsync(planA, autoSave: true);

            var woA = new WorkOrder(Guid.NewGuid(), company.Id, "WO-0004A", item.Id, bom.Id, 2m)
            {
                ProductionPlanId = planA.Id,
                ProductionPlanItemId = planItemA.Id,
            };
            woA.Submit();
            await woRepo.InsertAsync(woA, autoSave: true);

            var planB = new ProductionPlan(Guid.NewGuid(), company.Id, "PP-0004B", DateTime.UtcNow);
            var planItemB = new ProductionPlanItem(Guid.NewGuid(), planB.Id, item.Id, item.ItemName, bom.Id, 3m);
            planB.AddPlannedItem(planItemB);
            planB.Submit();
            await planRepo.InsertAsync(planB, autoSave: true);

            var woB = new WorkOrder(Guid.NewGuid(), company.Id, "WO-0004B", item.Id, bom.Id, 3m)
            {
                ProductionPlanId = planB.Id,
                ProductionPlanItemId = planItemB.Id,
            };
            woB.Submit();
            await woRepo.InsertAsync(woB, autoSave: true);

            var reportA = await planAppService.GetSummaryReportAsync(planA.Id);
            var docNamesA = reportA.Rows.Where(r => r.DocumentName != null).Select(r => r.DocumentName).ToList();

            docNamesA.ShouldContain("WO-0004A");
            docNamesA.ShouldNotContain("WO-0004B");
        });
    }

    [Fact]
    public async Task SummaryWithSubAssemblyRows()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var planRepo = GetRequiredService<IRepository<ProductionPlan, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var planAppService = GetRequiredService<IProductionPlanAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PP Summary Co 5"), autoSave: true);
            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-ITEM-5", "Finished Good 5", ItemType.Goods), autoSave: true);
            var subItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SUB-ITEM-5", "Sub Assembly 5", ItemType.Goods), autoSave: true);

            var subBom = await bomRepo.InsertAsync(new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-SUB-5", subItem.Id) { Quantity = 1, IsActive = true }, autoSave: true);
            var fgBom = await bomRepo.InsertAsync(new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-FG-5", fgItem.Id) { Quantity = 1, IsActive = true }, autoSave: true);

            var plan = new ProductionPlan(Guid.NewGuid(), company.Id, "PP-0005", DateTime.UtcNow);
            var planItem = new ProductionPlanItem(Guid.NewGuid(), plan.Id, fgItem.Id, fgItem.ItemName, fgBom.Id, 1m);
            plan.AddPlannedItem(planItem);

            var mrSub = new ProductionPlanMrItem(Guid.NewGuid(), plan.Id, subItem.Id, subItem.ItemName, 2m)
            {
                PlannedQty = 2m,
                ProcurementType = SubAssemblyType.InHouseManufacturing
            };
            plan.AddMaterialRequirement(mrSub);
            plan.Submit();
            await planRepo.InsertAsync(plan, autoSave: true);

            var subWo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-SUB-0005", subItem.Id, subBom.Id, 2m)
            {
                ProductionPlanId = plan.Id,
                ProductionPlanSubAssemblyItemId = mrSub.Id,
                ProducedQuantity = 1m
            };
            subWo.Submit();
            await woRepo.InsertAsync(subWo, autoSave: true);

            var report = await planAppService.GetSummaryReportAsync(plan.Id);

            // Expect:
            // 1. FG summary row (indent = 0)
            // 2. Sub-assembly summary row (indent = 1)
            // 3. Sub-assembly Work Order doc row (indent = 2)
            report.Rows.Count.ShouldBe(3);

            var subSummaryRow = report.Rows.FirstOrDefault(r => r.ItemCode == "SUB-ITEM-5" && r.DocumentType == null);
            subSummaryRow.ShouldNotBeNull();
            subSummaryRow.Indent.ShouldBe(1);
            subSummaryRow.Qty.ShouldBe(2m);
            subSummaryRow.ProducedQty.ShouldBe(1m);
            subSummaryRow.PendingQty.ShouldBe(1m);

            var subWoRow = report.Rows.FirstOrDefault(r => r.ItemCode == "SUB-ITEM-5" && r.DocumentType == "Work Order");
            subWoRow.ShouldNotBeNull();
            subWoRow.Indent.ShouldBe(2);
            subWoRow.DocumentName.ShouldBe("WO-SUB-0005");
            subWoRow.Qty.ShouldBe(2m);
            subWoRow.ProducedQty.ShouldBe(1m);
            subWoRow.PendingQty.ShouldBe(1m);
        });
    }

    [Fact]
    public async Task GetBillableSalesOrdersAsync_OrdersByOrderDateAndCreationTime_PR59010()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Sort Co"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Customer Sort"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-SORT", "Item Sort", ItemType.Goods), autoSave: true);

            var baseDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

            // Newer order
            var newerSo = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-NEWER", baseDate.AddDays(5));
            newerSo.AddItem(item.Id, "Item", 10m, 100m, 0m);
            newerSo.Submit();
            await soRepo.InsertAsync(newerSo, autoSave: true);

            // Older order
            var olderSo = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-OLDER", baseDate.AddDays(1));
            olderSo.AddItem(item.Id, "Item", 10m, 100m, 0m);
            olderSo.Submit();
            await soRepo.InsertAsync(olderSo, autoSave: true);

            var billableOrders = await soAppService.GetBillableSalesOrdersAsync(companyId: company.Id);

            billableOrders.Count.ShouldBeGreaterThanOrEqualTo(2);
            var billableForThisCompany = billableOrders.Where(o => o.CompanyId == company.Id).ToList();
            billableForThisCompany[0].OrderNumber.ShouldBe("SO-OLDER");
            billableForThisCompany[1].OrderNumber.ShouldBe("SO-NEWER");
        });
    }
}
