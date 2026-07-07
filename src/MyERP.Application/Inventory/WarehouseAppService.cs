using System;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
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
}
