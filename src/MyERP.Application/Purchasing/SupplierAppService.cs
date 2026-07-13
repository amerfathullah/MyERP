using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Permissions;
using Volo.Abp;
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

    /// <summary>
    /// Prevent deletion of suppliers with active orders or posted invoices.
    /// </summary>
    public override async Task DeleteAsync(Guid id)
    {
        var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseOrder, Guid>>();
        var poQuery = await poRepo.GetQueryableAsync();
        var hasActiveOrders = poQuery.Any(po =>
            po.SupplierId == id
            && po.Status != DocumentStatus.Draft
            && po.Status != DocumentStatus.Cancelled);

        if (hasActiveOrders)
        {
            throw new BusinessException("MyERP:04008")
                .WithData("partyType", "Supplier")
                .WithData("reason", "Supplier has active Purchase Orders.");
        }

        var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseInvoice, Guid>>();
        var piQuery = await piRepo.GetQueryableAsync();
        var hasPostedInvoices = piQuery.Any(pi =>
            pi.SupplierId == id
            && (pi.Status == DocumentStatus.Posted || pi.Status == DocumentStatus.Submitted));

        if (hasPostedInvoices)
        {
            throw new BusinessException("MyERP:04008")
                .WithData("partyType", "Supplier")
                .WithData("reason", "Supplier has posted Purchase Invoices.");
        }

        await base.DeleteAsync(id);
    }
}
