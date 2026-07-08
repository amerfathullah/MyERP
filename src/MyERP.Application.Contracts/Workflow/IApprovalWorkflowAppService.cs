using System;
using System.Threading.Tasks;
using MyERP.Workflow.DTOs;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Workflow;

public interface IApprovalWorkflowAppService : IApplicationService
{
    // Rule management (admin)
    Task<ApprovalRuleDto> CreateRuleAsync(CreateApprovalRuleDto input);
    Task<ApprovalRuleDto> UpdateRuleAsync(Guid id, UpdateApprovalRuleDto input);
    Task DeleteRuleAsync(Guid id);
    Task<PagedResultDto<ApprovalRuleDto>> GetRulesAsync(PagedAndSortedResultRequestDto input);
    Task<ApprovalRuleDto> GetRuleAsync(Guid id);

    // Approval actions
    Task<ApprovalRequestDto> ApproveAsync(ReviewApprovalDto input);
    Task<ApprovalRequestDto> RejectAsync(ReviewApprovalDto input);

    // Queries
    Task<PagedResultDto<ApprovalRequestDto>> GetPendingApprovalsAsync(PagedAndSortedResultRequestDto input);
    Task<PagedResultDto<ApprovalRequestDto>> GetDocumentApprovalsAsync(string documentType, Guid documentId);
}
