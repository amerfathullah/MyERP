using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

[Authorize]
public class AddressAppService : ApplicationService
{
    private readonly IRepository<Address, Guid> _addressRepository;

    public AddressAppService(IRepository<Address, Guid> addressRepository)
    {
        _addressRepository = addressRepository;
    }

    public async Task<List<AddressDto>> GetAddressesForPartyAsync(string partyType, Guid partyId)
    {
        var query = await _addressRepository.GetQueryableAsync();
        return query.Where(a => a.PartyType == partyType && a.PartyId == partyId && !a.IsDisabled)
            .OrderByDescending(a => a.IsPrimaryAddress)
            .ThenByDescending(a => a.CreationTime)
            .Select(a => MapToDto(a))
            .ToList();
    }

    public async Task<AddressDto> CreateAsync(CreateUpdateAddressDto input)
    {
        var address = new Address(
            GuidGenerator.Create(), input.Title, input.PartyType, input.PartyId,
            input.AddressLine1, input.Country, CurrentTenant.Id)
        {
            AddressType = input.AddressType ?? "Billing",
            AddressLine2 = input.AddressLine2,
            City = input.City,
            State = input.State,
            PostalCode = input.PostalCode,
            Phone = input.Phone,
            Email = input.Email,
            IsPrimaryAddress = input.IsPrimaryAddress,
            IsShippingAddress = input.IsShippingAddress,
        };
        await _addressRepository.InsertAsync(address);
        return MapToDto(address);
    }

    public async Task<AddressDto> UpdateAsync(Guid id, CreateUpdateAddressDto input)
    {
        var address = await _addressRepository.GetAsync(id);
        address.Title = input.Title;
        address.AddressType = input.AddressType ?? "Billing";
        address.AddressLine1 = input.AddressLine1;
        address.AddressLine2 = input.AddressLine2;
        address.City = input.City;
        address.State = input.State;
        address.PostalCode = input.PostalCode;
        address.Country = input.Country;
        address.Phone = input.Phone;
        address.Email = input.Email;
        address.IsPrimaryAddress = input.IsPrimaryAddress;
        address.IsShippingAddress = input.IsShippingAddress;
        await _addressRepository.UpdateAsync(address);
        return MapToDto(address);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _addressRepository.DeleteAsync(id);
    }

    private static AddressDto MapToDto(Address a) => new()
    {
        Id = a.Id,
        Title = a.Title,
        AddressType = a.AddressType,
        AddressLine1 = a.AddressLine1,
        AddressLine2 = a.AddressLine2,
        City = a.City,
        State = a.State,
        PostalCode = a.PostalCode,
        Country = a.Country,
        Phone = a.Phone,
        Email = a.Email,
        PartyType = a.PartyType,
        PartyId = a.PartyId,
        IsPrimaryAddress = a.IsPrimaryAddress,
        IsShippingAddress = a.IsShippingAddress,
    };
}

public class AddressDto : EntityDto<Guid>
{
    public string Title { get; set; } = null!;
    public string AddressType { get; set; } = null!;
    public string AddressLine1 { get; set; } = null!;
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public bool IsPrimaryAddress { get; set; }
    public bool IsShippingAddress { get; set; }
}

public class CreateUpdateAddressDto
{
    [Required][StringLength(200)] public string Title { get; set; } = null!;
    [StringLength(50)] public string? AddressType { get; set; }
    [Required][StringLength(300)] public string AddressLine1 { get; set; } = null!;
    [StringLength(300)] public string? AddressLine2 { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? State { get; set; }
    [StringLength(20)] public string? PostalCode { get; set; }
    [Required][StringLength(100)] public string Country { get; set; } = "Malaysia";
    [StringLength(50)] public string? Phone { get; set; }
    [StringLength(200)] public string? Email { get; set; }
    [Required] public string PartyType { get; set; } = null!;
    [Required] public Guid PartyId { get; set; }
    public bool IsPrimaryAddress { get; set; }
    public bool IsShippingAddress { get; set; }
}
