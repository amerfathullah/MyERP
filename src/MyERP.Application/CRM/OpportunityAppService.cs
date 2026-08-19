using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.Opportunities.Default)]
public class OpportunityAppService : ApplicationService, IOpportunityAppService
{
    private readonly IRepository<Opportunity, Guid> _repository;
    private readonly IRepository<Quotation, Guid> _quotationRepository;
    private readonly IRepository<CompetitorDetail, Guid> _competitorDetailRepository;

    public OpportunityAppService(
        IRepository<Opportunity, Guid> repository,
        IRepository<Quotation, Guid> quotationRepository,
        IRepository<CompetitorDetail, Guid> competitorDetailRepository)
    {
        _repository = repository;
        _quotationRepository = quotationRepository;
        _competitorDetailRepository = competitorDetailRepository;
    }

    public async Task<OpportunityDto> GetAsync(Guid id)
    {
        var opp = await _repository.GetAsync(id, includeDetails: true);
        var dto = ObjectMapper.Map<Opportunity, OpportunityDto>(opp);
        dto.Competitors = (await _competitorDetailRepository.GetListAsync(
                c => c.ParentType == "Opportunity" && c.ParentId == id))
            .Select(c => new CompetitorDetailDto
            {
                Id = c.Id,
                ParentType = c.ParentType,
                ParentId = c.ParentId,
                CompetitorId = c.CompetitorId,
            }).ToList();
        return dto;
    }

    // --- Competitors ---

    [Authorize(MyERPPermissions.Opportunities.Edit)]
    public async Task<CompetitorDetailDto> CreateCompetitorAsync(AddCompetitorDetailDto input)
    {
        var exists = await _competitorDetailRepository.AnyAsync(
            c => c.ParentType == input.ParentType && c.ParentId == input.ParentId && c.CompetitorId == input.CompetitorId);
        if (exists)
            throw new BusinessException(MyERPDomainErrorCodes.DuplicateRecord)
                .WithData("entity", "Competitor Detail");

        var detail = new CompetitorDetail(GuidGenerator.Create(), input.ParentType, input.ParentId, input.CompetitorId, CurrentTenant.Id);
        await _competitorDetailRepository.InsertAsync(detail);

        return new CompetitorDetailDto
        {
            Id = detail.Id,
            ParentType = detail.ParentType,
            ParentId = detail.ParentId,
            CompetitorId = detail.CompetitorId,
        };
    }

    [Authorize(MyERPPermissions.Opportunities.Edit)]
    public async Task DeleteCompetitorAsync(Guid competitorDetailId)
    {
        await _competitorDetailRepository.DeleteAsync(competitorDetailId);
    }

    public async Task<PagedResultDto<OpportunityDto>> GetListAsync(GetOpportunityListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.Status.HasValue)
            query = query.Where(o => o.Status == input.Status.Value);
        if (input.OpportunityType.HasValue)
            query = query.Where(o => o.OpportunityType == input.OpportunityType.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(o => o.CompanyId == input.CompanyId.Value);
        if (input.LeadId.HasValue)
            query = query.Where(o => o.LeadId == input.LeadId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(o =>
                o.Title.Contains(filter) ||
                (o.ContactName != null && o.ContactName.Contains(filter)) ||
                o.OpportunityNumber.Contains(filter));
        }

        var totalCount = query.Count();

        if (!string.IsNullOrWhiteSpace(input.Sorting))
            query = ApplySorting(query, input.Sorting);
        else
            query = query.OrderByDescending(o => o.CreationTime);

        var items = query.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<OpportunityDto>(totalCount, items.Select(x => ObjectMapper.Map<Opportunity, OpportunityDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.Opportunities.Create)]
    public async Task<OpportunityDto> CreateAsync(CreateOpportunityDto input)
    {
        var oppNumber = $"OPP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var opp = new Opportunity(
            GuidGenerator.Create(),
            input.CompanyId,
            oppNumber,
            input.Title,
            CurrentTenant.Id)
        {
            OpportunityType = input.OpportunityType,
            LeadId = input.LeadId,
            CustomerId = input.CustomerId,
            ContactName = input.ContactName,
            ContactEmail = input.ContactEmail,
            ContactPhone = input.ContactPhone,
            SalesStage = input.SalesStage ?? "Prospecting",
            Probability = input.Probability,
            ExpectedClosingDate = input.ExpectedClosingDate,
            OpportunityAmount = input.OpportunityAmount,
            CurrencyCode = input.CurrencyCode,
            AssignedUserId = input.AssignedUserId,
            Territory = input.Territory,
            Notes = input.Notes,
        };

        foreach (var item in input.Items)
        {
            opp.Items.Add(new OpportunityItem(
                GuidGenerator.Create(),
                opp.Id,
                item.Description,
                item.Quantity,
                item.UnitPrice)
            {
                ItemId = item.ItemId,
                Uom = item.Uom,
            });
        }
        opp.RecalculateAmount();

        await _repository.InsertAsync(opp);
        return ObjectMapper.Map<Opportunity, OpportunityDto>(opp);
    }

    [Authorize(MyERPPermissions.Opportunities.Edit)]
    public async Task<OpportunityDto> UpdateAsync(Guid id, UpdateOpportunityDto input)
    {
        var opp = await _repository.GetAsync(id, includeDetails: true);

        opp.Title = input.Title;
        opp.OpportunityType = input.OpportunityType;
        opp.ContactName = input.ContactName;
        opp.ContactEmail = input.ContactEmail;
        opp.ContactPhone = input.ContactPhone;
        opp.SalesStage = input.SalesStage;
        opp.Probability = input.Probability;
        opp.ExpectedClosingDate = input.ExpectedClosingDate;
        opp.CurrencyCode = input.CurrencyCode;
        opp.AssignedUserId = input.AssignedUserId;
        opp.Territory = input.Territory;
        opp.Notes = input.Notes;

        // Replace items
        opp.Items.Clear();
        foreach (var item in input.Items)
        {
            opp.Items.Add(new OpportunityItem(
                GuidGenerator.Create(),
                opp.Id,
                item.Description,
                item.Quantity,
                item.UnitPrice)
            {
                ItemId = item.ItemId,
                Uom = item.Uom,
            });
        }
        opp.RecalculateAmount();

        await _repository.UpdateAsync(opp);
        return ObjectMapper.Map<Opportunity, OpportunityDto>(opp);
    }

    [Authorize(MyERPPermissions.Opportunities.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.Opportunities.Edit)]
    public async Task<OpportunityDto> MarkQuotationAsync(Guid id)
    {
        var opp = await _repository.GetAsync(id);
        opp.MarkQuotation();
        await _repository.UpdateAsync(opp);
        return ObjectMapper.Map<Opportunity, OpportunityDto>(opp);
    }

    [Authorize(MyERPPermissions.Opportunities.Convert)]
    public async Task<OpportunityDto> ConvertAsync(Guid id)
    {
        var opp = await _repository.GetAsync(id);
        opp.Convert();
        await _repository.UpdateAsync(opp);
        return ObjectMapper.Map<Opportunity, OpportunityDto>(opp);
    }

    [Authorize(MyERPPermissions.Opportunities.Edit)]
    public async Task<OpportunityDto> DeclareLostAsync(Guid id, string? reason)
    {
        var opp = await _repository.GetAsync(id);

        // Per ERPNext PR #57489: block lost declaration when active quotations exist
        // Active = submitted quotations that are NOT Lost/Cancelled/Expired(Rejected)
        var quotationQuery = await _quotationRepository.GetQueryableAsync();
        var hasActiveQuotation = quotationQuery.Any(q =>
            q.OpportunityId == id &&
            q.Status != DocumentStatus.Draft &&
            q.Status != DocumentStatus.Cancelled &&
            q.Status != DocumentStatus.Rejected); // Rejected = Lost/Expired in our model

        if (hasActiveQuotation)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("reason", "Cannot declare opportunity as Lost while active quotations exist. Cancel or mark quotations as lost first.");
        }

        opp.DeclareLost(reason);
        await _repository.UpdateAsync(opp);
        return ObjectMapper.Map<Opportunity, OpportunityDto>(opp);
    }

    [Authorize(MyERPPermissions.Opportunities.Edit)]
    public async Task<OpportunityDto> CloseAsync(Guid id)
    {
        var opp = await _repository.GetAsync(id);
        opp.Close();
        await _repository.UpdateAsync(opp);
        return ObjectMapper.Map<Opportunity, OpportunityDto>(opp);
    }

    [Authorize(MyERPPermissions.Opportunities.Edit)]
    public async Task<OpportunityDto> ReopenAsync(Guid id)
    {
        var opp = await _repository.GetAsync(id);
        opp.Reopen();
        await _repository.UpdateAsync(opp);
        return ObjectMapper.Map<Opportunity, OpportunityDto>(opp);
    }

    private static IQueryable<Opportunity> ApplySorting(IQueryable<Opportunity> query, string sorting)
    {
        return sorting.ToLower() switch
        {
            "title asc" => query.OrderBy(o => o.Title),
            "title desc" => query.OrderByDescending(o => o.Title),
            "creationtime asc" => query.OrderBy(o => o.CreationTime),
            "creationtime desc" => query.OrderByDescending(o => o.CreationTime),
            "opportunityamount asc" => query.OrderBy(o => o.OpportunityAmount),
            "opportunityamount desc" => query.OrderByDescending(o => o.OpportunityAmount),
            _ => query.OrderByDescending(o => o.CreationTime),
        };
    }

    /// <summary>
    /// Update the sales stage of an opportunity (used by Kanban board drag-and-drop).
    /// Per ERPNext: Opportunity.sales_stage field drives pipeline position.
    /// </summary>
    [Authorize(MyERPPermissions.Opportunities.Edit)]
    public async Task<OpportunityDto> UpdateStageAsync(Guid id, UpdateOpportunityStageDto input)
    {
        var opp = await _repository.GetAsync(id);

        // Validate: cannot move lost/closed opportunities
        if (opp.Status == OpportunityStatus.Lost || opp.Status == OpportunityStatus.Closed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("status", opp.Status.ToString());
        }

        opp.SalesStage = input.SalesStage;

        // Auto-update probability based on stage (per ERPNext convention)
        opp.Probability = input.SalesStage switch
        {
            "Prospecting" => 10,
            "Qualification" => 25,
            "Needs Analysis" => 40,
            "Proposal" => 60,
            "Negotiation" => 75,
            "Won" => 100,
            "Lost" => 0,
            _ => opp.Probability,
        };

        // Auto-transition status for terminal stages using domain methods
        if (input.SalesStage == "Won" && opp.Status != OpportunityStatus.Converted)
        {
            opp.Convert();
        }
        else if (input.SalesStage == "Lost")
        {
            opp.DeclareLost("Moved to Lost via Kanban");
        }

        await _repository.UpdateAsync(opp);
        return ObjectMapper.Map<Opportunity, OpportunityDto>(opp);
    }
}

