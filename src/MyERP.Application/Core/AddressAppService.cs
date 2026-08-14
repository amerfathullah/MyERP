using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

[Authorize]
public class AddressAppService : ApplicationService, IAddressAppService
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
            .Select(a => ObjectMapper.Map<Address, AddressDto>(a))
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
        return ObjectMapper.Map<Address, AddressDto>(address);
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
        return ObjectMapper.Map<Address, AddressDto>(address);
    }

    [Authorize]
    public async Task DeleteAsync(Guid id)
    {
        await _addressRepository.DeleteAsync(id);
    }
}
