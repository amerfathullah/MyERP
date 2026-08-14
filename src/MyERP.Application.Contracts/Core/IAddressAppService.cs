using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IAddressAppService : IApplicationService
{
    Task<List<AddressDto>> GetAddressesForPartyAsync(string partyType, Guid partyId);
    Task<AddressDto> CreateAsync(CreateUpdateAddressDto input);
    Task<AddressDto> UpdateAsync(Guid id, CreateUpdateAddressDto input);
    Task DeleteAsync(Guid id);
}
