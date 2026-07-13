using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using Volo.Abp;
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

    /// <summary>
    /// Prevent deletion of customers with active orders or posted invoices.
    /// </summary>
    public override async Task DeleteAsync(Guid id)
    {
        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
        var soQuery = await soRepo.GetQueryableAsync();
        var hasActiveOrders = soQuery.Any(so =>
            so.CustomerId == id
            && so.Status != DocumentStatus.Draft
            && so.Status != DocumentStatus.Cancelled);

        if (hasActiveOrders)
        {
            throw new BusinessException("MyERP:03003")
                .WithData("partyType", "Customer")
                .WithData("reason", "Customer has active Sales Orders.");
        }

        var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>();
        var siQuery = await siRepo.GetQueryableAsync();
        var hasPostedInvoices = siQuery.Any(si =>
            si.CustomerId == id
            && (si.Status == DocumentStatus.Posted || si.Status == DocumentStatus.Submitted));

        if (hasPostedInvoices)
        {
            throw new BusinessException("MyERP:03003")
                .WithData("partyType", "Customer")
                .WithData("reason", "Customer has posted Sales Invoices.");
        }

        await base.DeleteAsync(id);
    }
}
