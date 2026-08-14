using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IContactAppService : IApplicationService
{
    Task<PagedResultDto<ContactDto>> GetListAsync(string partyType, Guid partyId, int skipCount = 0, int maxResultCount = 50);
    Task<List<ContactDto>> GetContactsForPartyAsync(string partyType, Guid partyId);
    Task<ContactDto> CreateAsync(CreateContactDto input);
    Task<ContactDto> UpdateAsync(Guid id, CreateContactDto input);
    Task DeleteAsync(Guid id);
}
