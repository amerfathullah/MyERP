using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Regression coverage for BinService.UpdateIndentedQtyAsync — had zero callers anywhere, despite
/// material-request-rfq-full.md's own spec listing "Update Bin indented_qty" / "Revert Bin
/// indented_qty" as Submit/Cancel Effect #2, and Bin.IndentedQty already feeding ProjectedQty
/// (used by reorder/MRP reports) — meaning "requested but not yet fulfilled" stock was always
/// silently reported as 0 for any submitted, not-yet-ordered Material Request.
/// </summary>
public abstract class MaterialRequestIndentedQtyTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubmitAsync_StockItemWithWarehouse_IncreasesBinIndentedQty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var mrRepository = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var binRepository = GetRequiredService<IRepository<Bin, Guid>>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MR Indented Qty Test Co"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "MR Indented WH"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MR-INDENT-1", "MR Indented Item", ItemType.Goods), autoSave: true);

            var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-INDENT-001", MaterialRequestType.Purchase, DateTime.UtcNow.Date, company.TenantId);
            mr.AddItem(item.Id, "MR Indented Item", quantity: 25m, uom: "Unit", warehouseId: warehouse.Id);
            await mrRepository.InsertAsync(mr, autoSave: true);

            await mrAppService.SubmitAsync(mr.Id);

            var bin = (await binRepository.GetQueryableAsync())
                .Single(b => b.ItemId == item.Id && b.WarehouseId == warehouse.Id);
            bin.IndentedQty.ShouldBe(25m);
        });
    }

    [Fact]
    public async Task CancelAsync_RevertsBinIndentedQty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var mrRepository = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var binRepository = GetRequiredService<IRepository<Bin, Guid>>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MR Indented Qty Test Co 2"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "MR Indented WH 2"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MR-INDENT-2", "MR Indented Item 2", ItemType.Goods), autoSave: true);

            var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-INDENT-002", MaterialRequestType.MaterialTransfer, DateTime.UtcNow.Date, company.TenantId);
            mr.AddItem(item.Id, "MR Indented Item 2", quantity: 15m, uom: "Unit", warehouseId: warehouse.Id);
            await mrRepository.InsertAsync(mr, autoSave: true);

            await mrAppService.SubmitAsync(mr.Id);
            await mrAppService.CancelAsync(mr.Id);

            var bin = (await binRepository.GetQueryableAsync())
                .Single(b => b.ItemId == item.Id && b.WarehouseId == warehouse.Id);
            bin.IndentedQty.ShouldBe(0m);
        });
    }

    [Fact]
    public async Task SubmitAsync_NoWarehouseSet_DoesNotTouchBin()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var mrRepository = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var binRepository = GetRequiredService<IRepository<Bin, Guid>>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MR Indented Qty Test Co 3"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MR-INDENT-3", "MR Indented Item 3", ItemType.Goods), autoSave: true);

            var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-INDENT-003", MaterialRequestType.Purchase, DateTime.UtcNow.Date, company.TenantId);
            mr.AddItem(item.Id, "MR Indented Item 3", quantity: 10m, uom: "Unit", warehouseId: null);
            await mrRepository.InsertAsync(mr, autoSave: true);

            await mrAppService.SubmitAsync(mr.Id);

            var bins = (await binRepository.GetQueryableAsync()).Where(b => b.ItemId == item.Id).ToList();
            bins.ShouldBeEmpty();
        });
    }
}
