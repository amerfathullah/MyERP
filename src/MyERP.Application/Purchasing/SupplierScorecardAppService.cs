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
public class SupplierScorecardAppService : ApplicationService
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
        return MapToDto(scorecard);
    }

    public async Task<ScorecardDto> GetBySupplierId(Guid supplierId)
    {
        var query = await _repository.GetQueryableAsync();
        var scorecard = query.FirstOrDefault(x => x.SupplierId == supplierId);
        return scorecard != null ? MapToDto(scorecard) : null!;
    }

    public async Task<PagedResultDto<ScorecardDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var count = query.Count();
        var list = query.OrderByDescending(x => x.Score)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<ScorecardDto>(count, list.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.SupplierScorecards.Create)]
    public async Task<ScorecardDto> CreateAsync(CreateScorecardDto input)
    {
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

        return MapToDto(scorecard);
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

        return MapToDto(scorecard);
    }

    /// <summary>
    /// Submit a scorecard period evaluation.
    /// </summary>
    [Authorize(MyERPPermissions.SupplierScorecards.Edit)]
    public async Task SubmitPeriodAsync(Guid scorecardId, CreateScorecardPeriodDto input)
    {
        var scorecard = await _repository.GetAsync(scorecardId);

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

    private static ScorecardDto MapToDto(SupplierScorecard s) => new()
    {
        Id = s.Id,
        SupplierId = s.SupplierId,
        CompanyId = s.CompanyId,
        PeriodType = s.PeriodType.ToString(),
        Score = s.Score,
        CurrentStanding = s.CurrentStanding,
        WeightingFunction = s.WeightingFunction,
        Standings = s.Standings.OrderBy(x => x.MinGrade).Select(x => new ScorecardStandingDto
        {
            Name = x.Name,
            MinScore = x.MinGrade,
            MaxScore = x.MaxGrade,
            PreventPos = x.PreventPos,
            PreventRfqs = x.PreventRfqs
        }).ToList(),
        Criteria = s.Criteria.Select(x => new ScorecardCriterionDto
        {
            Name = x.Name,
            Weight = x.Weight,
            MaxScore = x.MaxScore,
            Formula = x.Formula
        }).ToList()
    };
}

#region DTOs

public class ScorecardDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public Guid CompanyId { get; set; }
    public string PeriodType { get; set; } = null!;
    public decimal Score { get; set; }
    public string? CurrentStanding { get; set; }
    public string? WeightingFunction { get; set; }
    public List<ScorecardStandingDto> Standings { get; set; } = new();
    public List<ScorecardCriterionDto> Criteria { get; set; } = new();
}

public class ScorecardStandingDto
{
    public string Name { get; set; } = null!;
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
    public bool PreventPos { get; set; }
    public bool PreventRfqs { get; set; }
}

public class ScorecardCriterionDto
{
    public string Name { get; set; } = null!;
    public decimal Weight { get; set; }
    public decimal MaxScore { get; set; }
    public string? Formula { get; set; }
}

public class CreateScorecardDto
{
    public Guid SupplierId { get; set; }
    public Guid CompanyId { get; set; }
    public ScorecardPeriodType PeriodType { get; set; } = ScorecardPeriodType.Monthly;
    public string? WeightingFunction { get; set; }
    public List<CreateStandingDto> Standings { get; set; } = new();
    public List<CreateCriterionDto> Criteria { get; set; } = new();
}

public class CreateStandingDto
{
    public string Name { get; set; } = null!;
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
    public bool PreventPos { get; set; }
    public bool PreventRfqs { get; set; }
    public bool WarnPos { get; set; }
    public bool WarnRfqs { get; set; }
}

public class CreateCriterionDto
{
    public string Name { get; set; } = null!;
    public decimal Weight { get; set; }
    public decimal MaxScore { get; set; }
    public string? Formula { get; set; }
}

public class CreateScorecardPeriodDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Score { get; set; }
}

#endregion
