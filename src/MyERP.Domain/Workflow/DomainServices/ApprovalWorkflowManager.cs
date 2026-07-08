using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Workflow.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Users;

namespace MyERP.Workflow.DomainServices;

/// <summary>
/// Domain service for managing configurable approval workflows.
/// Evaluates rules, creates approval requests, and checks if documents are fully approved.
/// </summary>
public class ApprovalWorkflowManager : DomainService
{
    private readonly IRepository<ApprovalRule, Guid> _ruleRepository;
    private readonly IRepository<ApprovalRequest, Guid> _requestRepository;

    public ApprovalWorkflowManager(
        IRepository<ApprovalRule, Guid> ruleRepository,
        IRepository<ApprovalRequest, Guid> requestRepository)
    {
        _ruleRepository = ruleRepository;
        _requestRepository = requestRepository;
    }

    /// <summary>
    /// Determines whether a document type requires approval and initiates requests.
    /// Returns true if approval is required (document should wait for approval).
    /// </summary>
    public async Task<bool> InitiateApprovalAsync(
        string documentType, Guid documentId, Guid requestedByUserId,
        decimal? documentAmount = null, Guid? companyId = null, Guid? tenantId = null)
    {
        var rules = await GetApplicableRulesAsync(documentType, documentAmount, companyId);
        if (!rules.Any())
            return false; // No approval needed

        // Create approval requests for level 1 (subsequent levels are created on approval of current level)
        var firstLevelRules = rules.Where(r => r.Level == rules.Min(x => x.Level)).ToList();

        foreach (var rule in firstLevelRules)
        {
            var request = new ApprovalRequest(
                GuidGenerator.Create(),
                rule.Id,
                documentType,
                documentId,
                rule.Level,
                requestedByUserId,
                tenantId);

            await _requestRepository.InsertAsync(request);
        }

        return true;
    }

    /// <summary>
    /// Checks if all required approvals for a document are complete.
    /// </summary>
    public async Task<bool> IsFullyApprovedAsync(string documentType, Guid documentId)
    {
        var requests = await _requestRepository.GetListAsync(
            r => r.DocumentType == documentType && r.DocumentId == documentId);

        if (!requests.Any())
            return true; // No approval was required

        // All requests must be approved (not just pending)
        return requests.All(r => r.Status == ApprovalStatus.Approved);
    }

    /// <summary>
    /// Gets pending approval requests for a given document.
    /// </summary>
    public async Task<List<ApprovalRequest>> GetPendingRequestsAsync(string documentType, Guid documentId)
    {
        return await _requestRepository.GetListAsync(
            r => r.DocumentType == documentType
                && r.DocumentId == documentId
                && r.Status == ApprovalStatus.Pending);
    }

    /// <summary>
    /// Gets pending approvals assigned to a user (by role or direct assignment).
    /// </summary>
    public async Task<List<ApprovalRequest>> GetPendingApprovalsForUserAsync(Guid userId)
    {
        // Get requests that are pending - filtering by user/role is done at app service level
        return await _requestRepository.GetListAsync(r => r.Status == ApprovalStatus.Pending);
    }

    /// <summary>
    /// Approves a request and creates next-level requests if needed.
    /// </summary>
    public async Task ApproveAndAdvanceAsync(Guid requestId, Guid reviewerUserId, string? remarks = null)
    {
        var request = await _requestRepository.GetAsync(requestId);
        request.Approve(reviewerUserId, remarks);
        await _requestRepository.UpdateAsync(request);

        // Check if all requests at current level are approved
        var sameLevelRequests = await _requestRepository.GetListAsync(
            r => r.DocumentType == request.DocumentType
                && r.DocumentId == request.DocumentId
                && r.Level == request.Level);

        var allCurrentLevelApproved = sameLevelRequests.All(r => r.Status == ApprovalStatus.Approved);
        if (!allCurrentLevelApproved)
            return;

        // Find next level rules and create requests
        var allRules = await GetApplicableRulesAsync(request.DocumentType, null, null);
        var nextLevelRules = allRules
            .Where(r => r.Level > request.Level)
            .GroupBy(r => r.Level)
            .OrderBy(g => g.Key)
            .FirstOrDefault();

        if (nextLevelRules == null)
            return; // All levels complete

        foreach (var rule in nextLevelRules)
        {
            var nextRequest = new ApprovalRequest(
                GuidGenerator.Create(),
                rule.Id,
                request.DocumentType,
                request.DocumentId,
                rule.Level,
                request.RequestedByUserId,
                request.TenantId);

            await _requestRepository.InsertAsync(nextRequest);
        }
    }

    private async Task<List<ApprovalRule>> GetApplicableRulesAsync(
        string documentType, decimal? documentAmount, Guid? companyId)
    {
        var rules = await _ruleRepository.GetListAsync(
            r => r.DocumentType == documentType && r.IsActive);

        return rules
            .Where(r => !r.MinimumAmount.HasValue || (documentAmount.HasValue && documentAmount >= r.MinimumAmount))
            .Where(r => !r.CompanyId.HasValue || r.CompanyId == companyId)
            .OrderBy(r => r.Level)
            .ToList();
    }
}
