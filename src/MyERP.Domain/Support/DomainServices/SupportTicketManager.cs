using System;
using System.Threading.Tasks;
using MyERP.Support.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Support.DomainServices;

public class SupportTicketManager : DomainService
{
    private readonly IRepository<Issue, Guid> _issueRepository;
    private readonly IRepository<ServiceLevelAgreement, Guid> _slaRepository;

    public SupportTicketManager(
        IRepository<Issue, Guid> issueRepository,
        IRepository<ServiceLevelAgreement, Guid> slaRepository)
    {
        _issueRepository = issueRepository;
        _slaRepository = slaRepository;
    }

    public async Task ApplySlaToIssueAsync(Issue issue, Guid slaId)
    {
        var sla = await _slaRepository.GetAsync(slaId);
        
        // Very basic SLA resolution date calculation logic (does not yet account for holidays/business hours perfectly)
        issue.ServiceLevelAgreementId = sla.Id;
        issue.ResolutionTime = sla.ResolutionTimeHours;
        issue.FirstResponseTime = sla.ResponseTimeHours;
        
        await _issueRepository.UpdateAsync(issue);
    }
}
