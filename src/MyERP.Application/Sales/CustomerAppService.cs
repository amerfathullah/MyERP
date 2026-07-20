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
        GetCustomerListDto,
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

    public override async Task<PagedResultDto<CustomerDto>> GetListAsync(GetCustomerListDto input)
    {
        var filter = input.Filter;

        if (string.IsNullOrWhiteSpace(filter))
        {
            return await base.GetListAsync(input);
        }

        var queryable = await Repository.GetQueryableAsync();
        var filterLower = filter.ToLower();

        queryable = queryable.Where(c =>
            c.Name.ToLower().Contains(filterLower)
            || (c.CustomerCode != null && c.CustomerCode.ToLower().Contains(filterLower))
            || (c.Tin != null && c.Tin.Contains(filter)));

        var totalCount = queryable.Count();
        var items = queryable
            .OrderBy(c => c.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<CustomerDto>(
            totalCount,
            items.Select(ObjectMapper.Map<Customer, CustomerDto>).ToList());
    }

    protected override Customer MapToEntity(CreateUpdateCustomerDto input)
    {
        var customer = new Customer(
            GuidGenerator.Create(), input.CompanyId, input.Name, CurrentTenant.Id);
        MapToEntity(input, customer);
        return customer;
    }

    protected override void MapToEntity(CreateUpdateCustomerDto input, Customer entity)
    {
        entity.SetName(input.Name);
        entity.CustomerCode = input.CustomerCode;
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
        entity.DefaultReceivableAccountId = input.DefaultReceivableAccountId;
        entity.IsActive = input.IsActive;
    }
}

