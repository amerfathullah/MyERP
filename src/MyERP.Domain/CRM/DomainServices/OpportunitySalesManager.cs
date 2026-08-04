using System;
using System.Threading.Tasks;
using MyERP.CRM.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.CRM.DomainServices;

/// <summary>
/// Domain service for managing Opportunity sales pipeline.
/// </summary>
public class OpportunitySalesManager : DomainService
{
    private readonly IRepository<Opportunity, Guid> _opportunityRepository;

    public OpportunitySalesManager(IRepository<Opportunity, Guid> opportunityRepository)
    {
        _opportunityRepository = opportunityRepository;
    }

    /// <summary>
    /// Updates the sales stage and probability of an opportunity.
    /// </summary>
    public async Task UpdatePipelineStageAsync(Guid opportunityId, string salesStage, int probability)
    {
        var opportunity = await _opportunityRepository.GetAsync(opportunityId);
        
        opportunity.SalesStage = salesStage;
        opportunity.Probability = probability;

        await _opportunityRepository.UpdateAsync(opportunity);
    }

    /// <summary>
    /// Marks the opportunity as lost with a reason.
    /// </summary>
    public async Task DeclareLostAsync(Guid opportunityId, string reason)
    {
        var opportunity = await _opportunityRepository.GetAsync(opportunityId);
        
        opportunity.DeclareLost(reason);
        opportunity.Probability = 0;

        await _opportunityRepository.UpdateAsync(opportunity);
    }

    /// <summary>
    /// Marks the opportunity as successfully closed (won).
    /// </summary>
    public async Task CloseAsWonAsync(Guid opportunityId)
    {
        var opportunity = await _opportunityRepository.GetAsync(opportunityId);
        
        opportunity.Close();
        opportunity.Probability = 100;

        await _opportunityRepository.UpdateAsync(opportunity);
    }
}
