using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.CRM;

public interface ILeadAppService : IApplicationService
{
    Task<LeadDto> GetAsync(Guid id);
    Task<PagedResultDto<LeadDto>> GetListAsync(GetLeadListDto input);
    Task<LeadDto> CreateAsync(CreateLeadDto input);
    Task<LeadDto> UpdateAsync(Guid id, UpdateLeadDto input);
    Task DeleteAsync(Guid id);
    Task<LeadDto> QualifyAsync(Guid id);
    Task<LeadDto> MarkLostAsync(Guid id);
    Task<OpportunityDto> ConvertToOpportunityAsync(ConvertLeadToOpportunityDto input);
}

public interface IOpportunityAppService : IApplicationService
{
    Task<OpportunityDto> GetAsync(Guid id);
    Task<PagedResultDto<OpportunityDto>> GetListAsync(GetOpportunityListDto input);
    Task<OpportunityDto> CreateAsync(CreateOpportunityDto input);
    Task<OpportunityDto> UpdateAsync(Guid id, UpdateOpportunityDto input);
    Task DeleteAsync(Guid id);
    Task<OpportunityDto> MarkQuotationAsync(Guid id);
    Task<OpportunityDto> ConvertAsync(Guid id);
    Task<OpportunityDto> DeclareLostAsync(Guid id, string? reason);
    Task<OpportunityDto> CloseAsync(Guid id);
    Task<OpportunityDto> ReopenAsync(Guid id);
}
