using System;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

public class CustomerAppService :
    CrudAppService<
        Customer,
        CustomerDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateCustomerDto>,
    ICustomerAppService
{
    public CustomerAppService(IRepository<Customer, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Customers.Default;
        GetListPolicyName = MyERPPermissions.Customers.Default;
        CreatePolicyName = MyERPPermissions.Customers.Create;
        UpdatePolicyName = MyERPPermissions.Customers.Edit;
        DeletePolicyName = MyERPPermissions.Customers.Delete;
    }
}
