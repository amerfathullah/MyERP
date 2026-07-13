using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

public class WarehouseAppService :
    CrudAppService<
        Warehouse,
        WarehouseDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateWarehouseDto>,
    IWarehouseAppService
{
    public WarehouseAppService(IRepository<Warehouse, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Warehouses.Default;
        GetListPolicyName = MyERPPermissions.Warehouses.Default;
        CreatePolicyName = MyERPPermissions.Warehouses.Create;
        UpdatePolicyName = MyERPPermissions.Warehouses.Edit;
        DeletePolicyName = MyERPPermissions.Warehouses.Delete;
    }

    /// <summary>
    /// Prevent deletion of warehouses with stock (Bin entries exist).
    /// </summary>
    public override async Task DeleteAsync(Guid id)
    {
        var binRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Bin, Guid>>();
        var binQuery = await binRepo.GetQueryableAsync();
        var hasStock = binQuery.Any(b => b.WarehouseId == id && b.ActualQty != 0);

        if (hasStock)
        {
            throw new BusinessException("MyERP:05019")
                .WithData("reason", "Warehouse has non-zero stock. Transfer or reconcile stock first.");
        }

        var sleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var sleQuery = await sleRepo.GetQueryableAsync();
        var hasHistory = sleQuery.Any(s => s.WarehouseId == id);

        if (hasHistory)
        {
            throw new BusinessException("MyERP:05019")
                .WithData("reason", "Warehouse has stock ledger history. Deactivate instead of deleting.");
        }

        await base.DeleteAsync(id);
    }
}
