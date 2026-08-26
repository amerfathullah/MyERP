using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IPartyTypeAppService : IApplicationService
{
    Task<PartyTypeDto> GetAsync(Guid id);
    Task<PagedResultDto<PartyTypeDto>> GetListAsync(GetPartyTypeListDto input);
    Task<List<PartyTypeDto>> GetAllListAsync();
    Task<PartyTypeDto> CreateAsync(CreateUpdatePartyTypeDto input);
    Task<PartyTypeDto> UpdateAsync(Guid id, CreateUpdatePartyTypeDto input);
    Task DeleteAsync(Guid id);
}
