using System;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.SettingManagement;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Verifies ERPNext PR #59838 (commit ec3dbd2e37):
/// Validate sales order rows in stock reservation — cannot reserve against an item row that does not belong to the voucher.
/// </summary>
public abstract class StockReservationRowValidationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_Throws_WhenSalesOrderItemDoesNotBelongToOrder()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var settingManager = GetRequiredService<Volo.Abp.SettingManagement.ISettingManager>();
            await settingManager.SetGlobalAsync(MyERP.Settings.MyERPSettings.Stock.EnableStockReservation, "true");
            try
            {
                var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
                var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
                var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
                var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
                var appService = GetRequiredService<IStockReservationAppService>();

                var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Res Co"), autoSave: true);
                var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RES-ITEM-1", "Reservation Item", ItemType.Goods), autoSave: true);
                var warehouse = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Res WH"), autoSave: true);

                var so1 = new SalesOrder(Guid.NewGuid(), company.Id, Guid.NewGuid(), "SO-001", DateTime.UtcNow);
                so1.AddItem(item.Id, "Item Row", 10m, 100m, 0m);
                await soRepo.InsertAsync(so1, autoSave: true);

                var so2 = new SalesOrder(Guid.NewGuid(), company.Id, Guid.NewGuid(), "SO-002", DateTime.UtcNow);
                so2.AddItem(item.Id, "Item Row 2", 10m, 100m, 0m);
                await soRepo.InsertAsync(so2, autoSave: true);

                // Try to reserve for so1 using so2's item ID
                var dto = new CreateStockReservationDto
                {
                    CompanyId = company.Id,
                    ItemId = item.Id,
                    WarehouseId = warehouse.Id,
                    VoucherType = "SalesOrder",
                    VoucherId = so1.Id,
                    VoucherDetailId = so2.Items[0].Id, // Item from different SO
                    ReservedQty = 5m
                };

                var ex = await Should.ThrowAsync<BusinessException>(() => appService.CreateAsync(dto));
                ex.Code.ShouldBe(MyERPDomainErrorCodes.SalesOrderItemDoesNotBelongToOrder);
            }
            finally
            {
                await settingManager.SetGlobalAsync(MyERP.Settings.MyERPSettings.Stock.EnableStockReservation, "false");
            }
        });
    }
}
