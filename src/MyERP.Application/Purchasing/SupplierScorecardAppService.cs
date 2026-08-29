using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.SupplierScorecards.Default)]
public class SupplierScorecardAppService : ApplicationService, ISupplierScorecardAppService
{
    private readonly IRepository<SupplierScorecard, Guid> _repository;
    private readonly IRepository<ScorecardPeriod, Guid> _periodRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;

    public SupplierScorecardAppService(
        IRepository<SupplierScorecard, Guid> repository,
        IRepository<ScorecardPeriod, Guid> periodRepository,
        IRepository<Supplier, Guid> supplierRepository)
    {
        _repository = repository;
        _periodRepository = periodRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<ScorecardDto> GetAsync(Guid id)
    {
        var scorecard = await _repository.GetAsync(id);
        return ObjectMapper.Map<SupplierScorecard, ScorecardDto>(scorecard);
    }

    public async Task<ScorecardDto> GetBySupplierId(Guid supplierId)
    {
        var query = await _repository.GetQueryableAsync();
        var scorecard = query.FirstOrDefault(x => x.SupplierId == supplierId);
        return scorecard != null ? ObjectMapper.Map<SupplierScorecard, ScorecardDto>(scorecard) : null!;
    }

    public async Task<PagedResultDto<ScorecardDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var count = query.Count();
        var list = query.OrderByDescending(x => x.Score)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<ScorecardDto>(count, list.Select(ObjectMapper.Map<SupplierScorecard, ScorecardDto>).ToList());
    }

    [Authorize(MyERPPermissions.SupplierScorecards.Create)]
    public async Task<ScorecardDto> CreateAsync(CreateScorecardDto input)
    {
        if (input.Standings == null || input.Standings.Count == 0 || input.Criteria == null || input.Criteria.Count == 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        var scorecard = new SupplierScorecard(
            GuidGenerator.Create(),
            input.SupplierId,
            input.CompanyId,
            input.PeriodType,
            CurrentTenant.Id);

        scorecard.WeightingFunction = input.WeightingFunction;

        foreach (var standing in input.Standings)
        {
            scorecard.AddStanding(standing.Name, standing.MinScore, standing.MaxScore,
                standing.PreventPos, standing.PreventRfqs, standing.WarnPos, standing.WarnRfqs);
        }

        foreach (var criterion in input.Criteria)
        {
            scorecard.AddCriterion(criterion.Name, criterion.Weight, criterion.MaxScore, criterion.Formula);
        }

        scorecard.Validate();
        await _repository.InsertAsync(scorecard);

        // Sync enforcement flags to supplier
        await SyncEnforcementFlagsAsync(scorecard);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SupplierScorecard", scorecard.Id,
            "Created", scorecard.CompanyId,
            scorecard.SupplierId.ToString()[..8], "Draft", "Active", CurrentUser.Id,
            $"Supplier scorecard created for supplier {scorecard.SupplierId.ToString()[..8]}", CurrentTenant.Id));

        return ObjectMapper.Map<SupplierScorecard, ScorecardDto>(scorecard);
    }

    /// <summary>
    /// Manually update the supplier's score and recalculate standing.
    /// </summary>
    [Authorize(MyERPPermissions.SupplierScorecards.Edit)]
    public async Task<ScorecardDto> UpdateScoreAsync(Guid id, decimal newScore)
    {
        var scorecard = await _repository.GetAsync(id);
        scorecard.UpdateScore(newScore);
        await _repository.UpdateAsync(scorecard);

        // Sync enforcement flags to supplier
        await SyncEnforcementFlagsAsync(scorecard);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SupplierScorecard", scorecard.Id,
            "ScoreUpdated", scorecard.CompanyId,
            scorecard.SupplierId.ToString()[..8], "Active", "Active", CurrentUser.Id,
            $"Supplier scorecard score updated to {newScore:F2}", CurrentTenant.Id));

        return ObjectMapper.Map<SupplierScorecard, ScorecardDto>(scorecard);
    }

    /// <summary>
    /// Submit a scorecard period evaluation.
    /// </summary>
    [Authorize(MyERPPermissions.SupplierScorecards.Edit)]
    public async Task SubmitPeriodAsync(Guid scorecardId, CreateScorecardPeriodDto input)
    {
        if (input.EndDate < input.StartDate)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var scorecard = await _repository.GetAsync(scorecardId);

        // Deduplicate overlapping submitted scorecard periods (ERPNext PR #57115)
        var existingPeriods = await _periodRepository.GetQueryableAsync();
        var exists = existingPeriods.Any(p =>
            p.SupplierScorecardId == scorecardId &&
            p.IsSubmitted &&
            p.StartDate <= input.EndDate &&
            p.EndDate >= input.StartDate);

        if (exists)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DuplicateRecord)
                .WithData("reason", "A scorecard period overlapping these dates has already been submitted");
        }

        var period = new ScorecardPeriod(
            GuidGenerator.Create(),
            scorecardId,
            scorecard.SupplierId,
            input.StartDate,
            input.EndDate,
            CurrentTenant.Id);

        period.Submit(input.Score);
        await _periodRepository.InsertAsync(period);

        // Update main scorecard with latest period score
        scorecard.UpdateScore(input.Score);
        await _repository.UpdateAsync(scorecard);
        await SyncEnforcementFlagsAsync(scorecard);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SupplierScorecard", scorecard.Id,
            "PeriodSubmitted", scorecard.CompanyId,
            scorecard.SupplierId.ToString()[..8], "Active", "Active", CurrentUser.Id,
            $"Scorecard period ({input.StartDate:yyyy-MM-dd} to {input.EndDate:yyyy-MM-dd}) submitted with score {input.Score:F2}", CurrentTenant.Id));
    }

    /// <summary>
    /// Sync prevent_pos/prevent_rfqs flags to the Supplier entity.
    /// </summary>
    private async Task SyncEnforcementFlagsAsync(SupplierScorecard scorecard)
    {
        var (preventPos, preventRfqs, _, _) = scorecard.GetEnforcementFlags();
        var supplier = await _supplierRepository.GetAsync(scorecard.SupplierId);
        supplier.PreventPurchaseOrders = preventPos;
        supplier.PreventRfqs = preventRfqs;
        await _supplierRepository.UpdateAsync(supplier);
    }

    [Authorize(MyERPPermissions.SupplierScorecards.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
