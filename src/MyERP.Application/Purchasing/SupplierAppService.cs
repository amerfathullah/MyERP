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
        GetSupplierListDto,
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

    public override async Task<PagedResultDto<SupplierDto>> GetListAsync(GetSupplierListDto input)
    {
        var filter = input.Filter;

        if (string.IsNullOrWhiteSpace(filter))
        {
            return await base.GetListAsync(input);
        }

        var queryable = await Repository.GetQueryableAsync();
        var filterLower = filter.ToLower();

        queryable = queryable.Where(s =>
            s.Name.ToLower().Contains(filterLower)
            || (s.SupplierCode != null && s.SupplierCode.ToLower().Contains(filterLower))
            || (s.Tin != null && s.Tin.Contains(filter)));

        var totalCount = queryable.Count();
        var items = queryable
            .OrderBy(s => s.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<SupplierDto>(
            totalCount,
            items.Select(ObjectMapper.Map<Supplier, SupplierDto>).ToList());
    }

    protected override Supplier MapToEntity(CreateUpdateSupplierDto input)
    {
        var supplier = new Supplier(
            GuidGenerator.Create(), input.CompanyId, input.Name, CurrentTenant.Id);
        MapToEntity(input, supplier);
        return supplier;
    }

    protected override void MapToEntity(CreateUpdateSupplierDto input, Supplier entity)
    {
        entity.SetName(input.Name);
        entity.SupplierCode = input.SupplierCode;
        entity.Tin = input.Tin;
        entity.RegistrationNumber = input.RegistrationNumber;
        entity.SstRegistrationNumber = input.SstRegistrationNumber;
        entity.IdType = input.IdType;
        entity.IdValue = input.IdValue;
        entity.ContactPerson = input.ContactPerson;
        entity.Phone = input.Phone;
        entity.Email = input.Email;
        entity.Website = input.Website;
        entity.Address = input.Address;
        entity.City = input.City;
        entity.State = input.State;
        entity.PostalCode = input.PostalCode;
        entity.Country = input.Country;
        entity.DefaultPayableAccountId = input.DefaultPayableAccountId;
        entity.IsActive = input.IsActive;
    }
}

