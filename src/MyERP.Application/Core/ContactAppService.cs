using System;
using System.Collections.Generic;
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
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? Salutation { get; set; }
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? MobileNo { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public bool IsPrimaryContact { get; set; }
    public bool IsBillingContact { get; set; }
}

public class CreateContactDto
{
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public string? Salutation { get; set; }
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? MobileNo { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
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
            .OrderByDescending(c => c.IsPrimaryContact).ThenBy(c => c.FirstName)
            .Skip(skipCount).Take(maxResultCount).ToList();
        return new PagedResultDto<ContactDto>(totalCount, items.Select(MapToDto).ToList());
    }

    /// <summary>Get contacts for a specific party (used by Angular ContactManager component).</summary>
    public async Task<System.Collections.Generic.List<ContactDto>> GetContactsForPartyAsync(string partyType, Guid partyId)
    {
        var query = await _repository.GetQueryableAsync();
        return query.Where(c => c.PartyType == partyType && c.PartyId == partyId && c.IsActive)
            .OrderByDescending(c => c.IsPrimaryContact)
            .ThenBy(c => c.FirstName)
            .ToList()
            .Select(MapToDto)
            .ToList();
    }

    [Authorize(MyERPPermissions.Customers.Create)]
    public async Task<ContactDto> CreateAsync(CreateContactDto input)
    {
        var contact = new Contact(GuidGenerator.Create(), input.FirstName, input.PartyType,
            input.PartyId, CurrentTenant.Id)
        {
            LastName = input.LastName,
            Salutation = input.Salutation,
            Email = input.Email,
            Phone = input.Phone,
            MobileNo = input.MobileNo,
            Designation = input.Designation,
            Department = input.Department,
            IsPrimaryContact = input.IsPrimaryContact,
            IsBillingContact = input.IsBillingContact,
        };
        await _repository.InsertAsync(contact);
        return MapToDto(contact);
    }

    [Authorize(MyERPPermissions.Customers.Edit)]
    public async Task<ContactDto> UpdateAsync(Guid id, CreateContactDto input)
    {
        var contact = await _repository.GetAsync(id);
        contact.FirstName = input.FirstName;
        contact.LastName = input.LastName;
        contact.Salutation = input.Salutation;
        contact.Email = input.Email;
        contact.Phone = input.Phone;
        contact.MobileNo = input.MobileNo;
        contact.Designation = input.Designation;
        contact.Department = input.Department;
        contact.IsPrimaryContact = input.IsPrimaryContact;
        contact.IsBillingContact = input.IsBillingContact;
        await _repository.UpdateAsync(contact);
        return MapToDto(contact);
    }

    [Authorize(MyERPPermissions.Customers.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    private static ContactDto MapToDto(Contact c) => new()
    {
        Id = c.Id,
        PartyType = c.PartyType,
        PartyId = c.PartyId,
        FirstName = c.FirstName,
        LastName = c.LastName,
        Salutation = c.Salutation,
        FullName = c.FullName,
        Email = c.Email,
        Phone = c.Phone,
        MobileNo = c.MobileNo,
        Designation = c.Designation,
        Department = c.Department,
        IsPrimaryContact = c.IsPrimaryContact,
        IsBillingContact = c.IsBillingContact,
    };
}
