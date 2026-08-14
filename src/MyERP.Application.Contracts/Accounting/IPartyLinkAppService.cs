using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IPartyLinkAppService : IApplicationService
{
    Task<PagedResultDto<PartyLinkDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<PartyLinkDto> CreateAsync(CreatePartyLinkDto input);
    Task DeleteAsync(Guid id);
}
