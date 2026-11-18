using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.SettingManagement;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Integration tests for ERPNext PR #65180 / ReceivedStockReservation:
/// Purchase Receipts and stock-updating Purchase Invoices received against a Production Plan's
/// Material Request reserve stock for the plan, hand off to reserving Work Orders, and release on cancel.
/// </summary>
public abstract class ProductionPlanReceivedStockReservationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ReserveReceivedStock_FromPurchaseInvoice_ReservesForPlanAndHandsOffToWorkOrder_AndCancelsCleanly()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var settingManager = GetRequiredService<ISettingManager>();
            await settingManager.SetGlobalAsync(Settings.MyERPSettings.Stock.EnableStockReservation, "true");
            try
            {
                var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
                var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
                var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
                var binRepo = GetRequiredService<IRepository<Bin, Guid>>();
                var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
                var planRepo = GetRequiredService<IRepository<ProductionPlan, Guid>>();
                var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
                var sreRepo = GetRequiredService<IRepository<StockReservationEntry, Guid>>();
                var reservationManager = GetRequiredService<StockReservationManager>();

                var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PP Recv Res Co"), autoSave: true);
                var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Main Stores"), autoSave: true);
                var fg = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-RES-1", "FG Res 1", ItemType.Goods), autoSave: true);
                var rm = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-RES-1", "RM Res 1", ItemType.Goods), autoSave: true);

                var plan = new ProductionPlan(Guid.NewGuid(), company.Id, "PP-RECV-001", DateTime.UtcNow)
                {
                    ForWarehouseId = wh.Id,
                    ReserveStock = true,
                };
                plan.AddPlannedItem(new ProductionPlanItem(Guid.NewGuid(), plan.Id, fg.Id, "FG Res 1", Guid.NewGuid(), 10m));
                var mrPlanItem = new ProductionPlanMrItem(Guid.NewGuid(), plan.Id, rm.Id, "RM Res 1", 10m)
                {
                    WarehouseId = wh.Id,
                    PlannedQty = 10m,
                };
                plan.AddMaterialRequirement(mrPlanItem);
                plan.Submit();
                await planRepo.InsertAsync(plan, autoSave: true);

                var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-RECV-001", MaterialRequestType.Purchase, DateTime.UtcNow);
                mr.AddItem(
                    rm.Id, "RM Res 1", 10m, "Unit", wh.Id,
                    productionPlanId: plan.Id,
                    productionPlanMrItemId: mrPlanItem.Id);
                var mrItem = mr.Items.Single();
                mr.Submit();
                await mrRepo.InsertAsync(mr, autoSave: true);

                // Stock arrives via Purchase Invoice (UpdateStock = true)
                var piId = Guid.NewGuid();
                await binRepo.InsertAsync(
                    new Bin(Guid.NewGuid(), rm.Id, wh.Id)
                    {
                        ActualQty = 10m,
                        StockValue = 100m,
                        ValuationRate = 10m,
                    },
                    autoSave: true);

                await reservationManager.ReserveReceivedStockForProductionPlansAsync(
                    "PurchaseInvoice",
                    piId,
                    company.Id,
                    null,
                    new[] { (rm.Id, wh.Id, 10m, (Guid?)mrItem.Id) });

                var sreList = await sreRepo.GetListAsync(s => s.FromVoucherType == "PurchaseInvoice" && s.FromVoucherId == piId);
                sreList.Count.ShouldBe(1);
                sreList[0].VoucherType.ShouldBe("ProductionPlan");
                sreList[0].VoucherId.ShouldBe(plan.Id);
                sreList[0].VoucherDetailId.ShouldBe(mrPlanItem.Id);
                sreList[0].ReservedQty.ShouldBe(10m);
                sreList[0].Status.ShouldBe(DocumentStatus.Submitted);

                var bin = await binRepo.GetAsync(b => b.ItemId == rm.Id && b.WarehouseId == wh.Id);
                bin.ReservedQty.ShouldBe(10m);

                // Cancelling the Purchase Invoice releases its Production Plan reservation & bin reserved qty
                await reservationManager.CancelReservationsFromVoucherAsync("PurchaseInvoice", piId);

                var updatedSre = await sreRepo.GetAsync(sreList[0].Id);
                updatedSre.Status.ShouldBe(DocumentStatus.Cancelled);
                var updatedBin = await binRepo.GetAsync(b => b.ItemId == rm.Id && b.WarehouseId == wh.Id);
                updatedBin.ReservedQty.ShouldBe(0m);
            }
            finally
            {
                await settingManager.SetGlobalAsync(Settings.MyERPSettings.Stock.EnableStockReservation, "false");
            }
        });
    }

    [Fact]
    public async Task ReserveReceivedStock_FromPurchaseReceipt_HandsOffToActiveReservingWorkOrder()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var settingManager = GetRequiredService<ISettingManager>();
            await settingManager.SetGlobalAsync(Settings.MyERPSettings.Stock.EnableStockReservation, "true");
            try
            {
                var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
                var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
                var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
                var binRepo = GetRequiredService<IRepository<Bin, Guid>>();
                var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
                var planRepo = GetRequiredService<IRepository<ProductionPlan, Guid>>();
                var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
                var sreRepo = GetRequiredService<IRepository<StockReservationEntry, Guid>>();
                var reservationManager = GetRequiredService<StockReservationManager>();

                var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PP WO Handoff Co"), autoSave: true);
                var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Main Stores"), autoSave: true);
                var fg = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-HO-1", "FG Handoff", ItemType.Goods), autoSave: true);
                var rm = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-HO-1", "RM Handoff", ItemType.Goods), autoSave: true);

                var plan = new ProductionPlan(Guid.NewGuid(), company.Id, "PP-HO-001", DateTime.UtcNow)
                {
                    ForWarehouseId = wh.Id,
                    ReserveStock = true,
                };
                plan.AddPlannedItem(new ProductionPlanItem(Guid.NewGuid(), plan.Id, fg.Id, "FG Handoff", Guid.NewGuid(), 8m));
                var mrPlanItem = new ProductionPlanMrItem(Guid.NewGuid(), plan.Id, rm.Id, "RM Handoff", 8m)
                {
                    WarehouseId = wh.Id,
                    PlannedQty = 8m,
                };
                plan.AddMaterialRequirement(mrPlanItem);
                plan.Submit();
                await planRepo.InsertAsync(plan, autoSave: true);

                var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-HO-001", MaterialRequestType.Purchase, DateTime.UtcNow);
                mr.AddItem(
                    rm.Id, "RM Handoff", 8m, "Unit", wh.Id,
                    productionPlanId: plan.Id,
                    productionPlanMrItemId: mrPlanItem.Id);
                var mrItem = mr.Items.Single();
                mr.Submit();
                await mrRepo.InsertAsync(mr, autoSave: true);

                var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-HO-001", fg.Id, Guid.NewGuid(), 8m)
                {
                    ProductionPlanId = plan.Id,
                    ReserveStock = true,
                    FgWarehouseId = wh.Id,
                    SourceWarehouseId = wh.Id,
                };
                wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, rm.Id, "RM Handoff", 8m)
                {
                    SourceWarehouseId = wh.Id,
                });
                wo.Submit();
                await woRepo.InsertAsync(wo, autoSave: true);

                await binRepo.InsertAsync(
                    new Bin(Guid.NewGuid(), rm.Id, wh.Id)
                    {
                        ActualQty = 8m,
                        StockValue = 80m,
                        ValuationRate = 10m,
                    },
                    autoSave: true);

                var prId = Guid.NewGuid();
                await reservationManager.ReserveReceivedStockForProductionPlansAsync(
                    "PurchaseReceipt",
                    prId,
                    company.Id,
                    null,
                    new[] { (rm.Id, wh.Id, 8m, (Guid?)mrItem.Id) });

                var woSres = await sreRepo.GetListAsync(s => s.VoucherType == "WorkOrder" && s.VoucherId == wo.Id);
                woSres.Count.ShouldBe(1);
                woSres[0].ReservedQty.ShouldBe(8m);
                woSres[0].FromVoucherType.ShouldBe("ProductionPlan");
                woSres[0].FromVoucherId.ShouldBe(plan.Id);

                var updatedWo = await woRepo.GetAsync(wo.Id, includeDetails: true);
                updatedWo.RequiredItems.Single().StockReservedQty.ShouldBe(8m);
            }
            finally
            {
                await settingManager.SetGlobalAsync(Settings.MyERPSettings.Stock.EnableStockReservation, "false");
            }
        });
    }
}
