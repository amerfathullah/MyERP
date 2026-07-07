using System;
using MyERP.Purchasing.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

public class SupplierAppService :
    CrudAppService<
        Supplier,
        SupplierDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateSupplierDto>,
    ISupplierAppService
{
    public SupplierAppService(IRepository<Supplier, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Suppliers.Default;
        GetListPolicyName = MyERPPermissions.Suppliers.Default;
        CreatePolicyName = MyERPPermissions.Suppliers.Create;
        UpdatePolicyName = MyERPPermissions.Suppliers.Edit;
        DeletePolicyName = MyERPPermissions.Suppliers.Delete;
    }
}
