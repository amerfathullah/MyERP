using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Resolves default values from party master data (Customer/Supplier).
/// Used during document creation to auto-fill address, payment terms, etc.
/// Per ERPNext: get_party_details → _set_missing_values pattern.
/// </summary>
public class PartyDefaultsService : DomainService
{
    private readonly IRepository<Address, Guid> _addressRepository;

    public PartyDefaultsService(IRepository<Address, Guid> addressRepository)
    {
        _addressRepository = addressRepository;
    }

    /// <summary>
    /// Gets the primary billing address for a party.
    /// Priority: IsPrimaryAddress=true > first available > null.
    /// Per gotcha #33: always ORDER BY is_primary_address DESC, creation DESC.
    /// </summary>
    public async Task<Address?> GetPrimaryAddressAsync(string partyType, Guid partyId)
    {
        var query = await _addressRepository.GetQueryableAsync();
        var address = query
            .Where(a => a.PartyType == partyType && a.PartyId == partyId && !a.IsDisabled)
            .OrderByDescending(a => a.IsPrimaryAddress)
            .ThenByDescending(a => a.CreationTime)
            .FirstOrDefault();

        return address;
    }

    /// <summary>
    /// Gets the shipping address for a party.
    /// Priority: IsShippingAddress=true > first available > null.
    /// </summary>
    public async Task<Address?> GetShippingAddressAsync(string partyType, Guid partyId)
    {
        var query = await _addressRepository.GetQueryableAsync();
        var address = query
            .Where(a => a.PartyType == partyType && a.PartyId == partyId
                     && !a.IsDisabled && a.IsShippingAddress)
            .OrderByDescending(a => a.CreationTime)
            .FirstOrDefault();

        return address ?? await GetPrimaryAddressAsync(partyType, partyId);
    }
}
