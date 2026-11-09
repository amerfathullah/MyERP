using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 20 upstream parity:
/// 1) Subcontracting Receipt Supplier Warehouse Mapping for Supplied Items (PR #64867 / commits 1bcdf9c96f &amp; 7b124a874c).
/// 2) Subcontracting Receipt Return Supplier Warehouse Propagation.
/// </summary>
public abstract class UpstreamBatch20IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubcontractingReceipt_CreateReceiptAsync_DefaultsSupplierWarehouseFromOrderAndPersists()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var scrRepo = GetRequiredService<IRepository<SubcontractingReceipt, Guid>>();
            var subAppService = GetRequiredService<ISubcontractingAppService>();
            var uowManager = GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 20 Subcontract Co"), autoSave: true);
            await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company.Id, "FY 2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCR", "SCR", "SCR-2026-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCR-RET", "SCR-RET", "SCR-RET-2026-"), autoSave: true);

            var stockAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1400", "Stock In Hand", AccountType.Asset), autoSave: true);
            var srbnbAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "2110", "SRBNB", AccountType.Liability), autoSave: true);

            await ruleRepo.InsertAsync(new AccountingRule(
                Guid.NewGuid(), company.Id, "SCR Stock Dr", "SubcontractingReceipt",
                isDebit: true, AccountSource.FixedAccount, AmountSource.NetTotal)
            {
                FixedAccountId = stockAcc.Id,
                SortOrder = 1
            }, autoSave: true);
            await ruleRepo.InsertAsync(new AccountingRule(
                Guid.NewGuid(), company.Id, "SCR SRBNB Cr", "SubcontractingReceipt",
                isDebit: false, AccountSource.FixedAccount, AmountSource.NetTotal)
            {
                FixedAccountId = srbnbAcc.Id,
                SortOrder = 2
            }, autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Subcontractor B20"), autoSave: true);
            var fgWarehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Finished Goods WH") { WarehouseCode = "WH-FG-B20" }, autoSave: true);
            var supplierWarehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Subcontractor WH") { WarehouseCode = "WH-SUB-B20" }, autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-ITEM-B20", "Finished Good B20", ItemType.Goods) { MaintainStock = true, AllowNegativeStock = true }, autoSave: true);
            var rmItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-ITEM-B20", "Raw Material B20", ItemType.Goods) { MaintainStock = true, AllowNegativeStock = true }, autoSave: true);

            var bom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-B20-001", fgItem.Id)
            {
                Quantity = 1m,
                IsActive = true,
                IsDefault = true
            };
            bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmItem.Id, rmItem.ItemName, 2m, 10m));
            await bomRepo.InsertAsync(bom, autoSave: true);

            // Create and submit SCO with SupplierWarehouseId set
            var sco = new SubcontractingOrder(Guid.NewGuid(), company.Id, "SCO-B20-001", DateTime.UtcNow.Date, supplier.Id)
            {
                SupplierWarehouseId = supplierWarehouse.Id
            };
            sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, fgItem.Id, fgItem.ItemName, 10m, 50m)
            {
                BomId = bom.Id,
                WarehouseId = fgWarehouse.Id,
                ServiceCostPerQty = 50m
            });
            sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco.Id, rmItem.Id, rmItem.ItemName, 20m)
            {
                ReserveWarehouseId = supplierWarehouse.Id
            });
            sco.Submit();
            await scoRepo.InsertAsync(sco, autoSave: true);
            await uowManager.Current!.SaveChangesAsync();

            // Create SCR without specifying SupplierWarehouseId explicitly - should default from SCO
            var scrDto = await subAppService.CreateReceiptAsync(new CreateSubcontractingReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                SubcontractingOrderId = sco.Id,
                PostingDate = DateTime.UtcNow.Date,
                WarehouseId = fgWarehouse.Id,
                Items = new List<CreateScrItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        Qty = 6m,
                        Rate = 50m,
                        WarehouseId = fgWarehouse.Id
                    }
                }
            });

            scrDto.ShouldNotBeNull();
            scrDto.SupplierWarehouseId.ShouldBe(supplierWarehouse.Id);
            scrDto.WarehouseId.ShouldBe(fgWarehouse.Id);

            await uowManager.Current!.SaveChangesAsync();

            // Read-back entity from EF Core repository
            var savedScr = await scrRepo.GetAsync(scrDto.Id);
            savedScr.SupplierWarehouseId.ShouldBe(supplierWarehouse.Id);
            savedScr.WarehouseId.ShouldBe(fgWarehouse.Id);

            // Now submit receipt and create a return receipt
            await subAppService.SubmitReceiptAsync(savedScr.Id);
            await uowManager.Current!.SaveChangesAsync();

            var returnDto = await subAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
            {
                ReturnAgainstReceiptId = savedScr.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreateScrReturnItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        Qty = 2m,
                        Rate = 50m,
                        WarehouseId = fgWarehouse.Id
                    }
                }
            });

            returnDto.ShouldNotBeNull();
            returnDto.IsReturn.ShouldBeTrue();
            returnDto.SupplierWarehouseId.ShouldBe(supplierWarehouse.Id);
            returnDto.WarehouseId.ShouldBe(fgWarehouse.Id);

            await uowManager.Current!.SaveChangesAsync();

            var savedReturn = await scrRepo.GetAsync(returnDto.Id);
            savedReturn.SupplierWarehouseId.ShouldBe(supplierWarehouse.Id);
            savedReturn.WarehouseId.ShouldBe(fgWarehouse.Id);

            // Also test GetReceiptsForOrderAsync DTO projection includes SupplierWarehouseId
            var receipts = await subAppService.GetReceiptsForOrderAsync(sco.Id);
            receipts.Count.ShouldBe(2);
            receipts.All(r => r.SupplierWarehouseId == supplierWarehouse.Id).ShouldBeTrue();
            receipts.All(r => r.WarehouseId == fgWarehouse.Id).ShouldBeTrue();
        });
    }
}
