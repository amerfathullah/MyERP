using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

public class ContactDto : EntityDto<Guid>
{
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Designation { get; set; }
    public bool IsPrimaryContact { get; set; }
    public bool IsBillingContact { get; set; }
}

public class CreateContactDto
{
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Designation { get; set; }
    public bool IsPrimaryContact { get; set; }
    public bool IsBillingContact { get; set; }
}

[Authorize(MyERPPermissions.Customers.Default)]
public class ContactAppService : ApplicationService
{
    private readonly IRepository<Contact, Guid> _repository;
    public ContactAppService(IRepository<Contact, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<ContactDto>> GetListAsync(string partyType, Guid partyId, int skipCount = 0, int maxResultCount = 50)
    {
        var query = await _repository.GetQueryableAsync();
        var filtered = query.Where(c => c.PartyType == partyType && c.PartyId == partyId);
        var totalCount = filtered.Count();
        var items = filtered
            .OrderByDescending(c => c.IsPrimaryContact).ThenBy(c => c.FullName)
            .Skip(skipCount).Take(maxResultCount).ToList();
        return new PagedResultDto<ContactDto>(totalCount, items.Select(ObjectMapper.Map<Contact, ContactDto>).ToList());
    }

    [Authorize(MyERPPermissions.Customers.Create)]
    public async Task<ContactDto> CreateAsync(CreateContactDto input)
    {
        var contact = new Contact(GuidGenerator.Create(), input.FullName, input.PartyType,
            input.PartyId, CurrentTenant.Id)
        {
            Email = input.Email, Phone = input.Phone, Designation = input.Designation,
            IsPrimaryContact = input.IsPrimaryContact, IsBillingContact = input.IsBillingContact,
        };
        await _repository.InsertAsync(contact);
        return ObjectMapper.Map<Contact, ContactDto>(contact);
    }

    [Authorize(MyERPPermissions.Customers.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);


}
