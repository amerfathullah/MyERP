using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.CRM;

public interface IOpportunityLostReasonAppService : IApplicationService
{
    Task<OpportunityLostReasonDto> GetAsync(Guid id);
    Task<PagedResultDto<OpportunityLostReasonDto>> GetListAsync(GetOpportunityLostReasonListDto input);
    Task<OpportunityLostReasonDto> CreateAsync(CreateUpdateOpportunityLostReasonDto input);
    Task<OpportunityLostReasonDto> UpdateAsync(Guid id, CreateUpdateOpportunityLostReasonDto input);
    Task DeleteAsync(Guid id);
}
