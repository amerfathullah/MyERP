using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Shared;
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
    Task<Guid> ConvertToCustomerAsync(ConvertLeadToCustomerDto input);

    Task<List<CrmNoteDto>> GetNotesAsync(Guid id);
    Task<CrmNoteDto> AddNoteAsync(Guid id, AddCrmNoteDto input);
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

public interface IProspectAppService : IApplicationService
{
    Task<ProspectDto> GetAsync(Guid id);
    Task<PagedResultDto<ProspectDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<ProspectDto> CreateAsync(CreateProspectDto input);
    Task<ProspectDto> ConvertToCustomerAsync(Guid id, Guid customerId);
    Task<ProspectDto> AddLeadAsync(Guid id, Guid leadId, string? leadName = null, string? email = null);
    Task DeleteAsync(Guid id);
    Task<ProspectDto> UpdateAsync(Guid id, CreateProspectDto input);
}

public interface IContractAppService : IApplicationService
{
    Task<ContractDto> GetAsync(Guid id);
    Task<PagedResultDto<ContractDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<ContractDto> CreateAsync(CreateContractDto input);
    Task<ContractDto> UpdateAsync(Guid id, CreateContractDto input);
    Task<ContractDto> SignAsync(Guid id);
    Task<ContractDto> RenewAsync(Guid id, DateTime newEndDate);
    Task<ContractDto> CancelAsync(Guid id);
    Task<ContractDto> AddFulfilmentItemAsync(Guid id, string requirement);
    Task<ContractDto> SetFulfilmentItemStatusAsync(Guid id, Guid itemId, bool fulfilled, string? notes = null);
    Task<ContractDto> RemoveFulfilmentItemAsync(Guid id, Guid itemId);
}

public interface IShipmentAppService : IApplicationService
{
    Task<ShipmentDto> GetAsync(Guid id);
    Task<PagedResultDto<ShipmentDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<ShipmentDto> CreateAsync(CreateShipmentDto input);
    Task<ShipmentDto> SubmitAsync(Guid id);
    Task<ShipmentDto> MarkInTransitAsync(Guid id);
    Task<ShipmentDto> MarkDeliveredAsync(Guid id);
    Task<ShipmentDto> CancelAsync(Guid id);
    Task DeleteAsync(Guid id);
}

public interface ISalesPipelineAppService : IApplicationService
{
    Task<SalesPipelineDashboardDto> GetPipelineDataAsync(Guid? companyId = null);
    Task<List<PipelineOpportunityDto>> GetTopOpportunitiesAsync(Guid? companyId = null, int maxCount = 10);
}
