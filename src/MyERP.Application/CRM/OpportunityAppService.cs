using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.Opportunities.Default)]
public class OpportunityAppService : ApplicationService, IOpportunityAppService
{
    private readonly IRepository<Opportunity, Guid> _repository;

    public OpportunityAppService(IRepository<Opportunity, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<OpportunityDto> GetAsync(Guid id)
    {
        var opp = await _repository.GetAsync(id, includeDetails: true);
        return MapToDto(opp);
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
            var filter = input.Filter.ToLower();
            query = query.Where(o =>
                o.Title.ToLower().Contains(filter) ||
                (o.ContactName != null && o.ContactName.ToLower().Contains(filter)) ||
                o.OpportunityNumber.ToLower().Contains(filter));
        }

        var totalCount = query.Count();

        if (!string.IsNullOrWhiteSpace(input.Sorting))
            query = ApplySorting(query, input.Sorting);
        else
            query = query.OrderByDescending(o => o.CreationTime);

        var items = query.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<OpportunityDto>(totalCount, items.Select(MapToDto).ToList());
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
        return MapToDto(opp);
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
        return MapToDto(opp);
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
        return MapToDto(opp);
    }

    [Authorize(MyERPPermissions.Opportunities.Convert)]
    public async Task<OpportunityDto> ConvertAsync(Guid id)
    {
        var opp = await _repository.GetAsync(id);
        opp.Convert();
        await _repository.UpdateAsync(opp);
        return MapToDto(opp);
    }

    [Authorize(MyERPPermissions.Opportunities.Edit)]
    public async Task<OpportunityDto> DeclareLostAsync(Guid id, string? reason)
    {
        var opp = await _repository.GetAsync(id);
        opp.DeclareLost(reason);
        await _repository.UpdateAsync(opp);
        return MapToDto(opp);
    }

    [Authorize(MyERPPermissions.Opportunities.Edit)]
    public async Task<OpportunityDto> CloseAsync(Guid id)
    {
        var opp = await _repository.GetAsync(id);
        opp.Close();
        await _repository.UpdateAsync(opp);
        return MapToDto(opp);
    }

    [Authorize(MyERPPermissions.Opportunities.Edit)]
    public async Task<OpportunityDto> ReopenAsync(Guid id)
    {
        var opp = await _repository.GetAsync(id);
        opp.Reopen();
        await _repository.UpdateAsync(opp);
        return MapToDto(opp);
    }

    private static OpportunityDto MapToDto(Opportunity opp) => new()
    {
        Id = opp.Id,
        OpportunityNumber = opp.OpportunityNumber,
        Title = opp.Title,
        Status = opp.Status,
        OpportunityType = opp.OpportunityType,
        LeadId = opp.LeadId,
        CustomerId = opp.CustomerId,
        ContactName = opp.ContactName,
        ContactEmail = opp.ContactEmail,
        ContactPhone = opp.ContactPhone,
        SalesStage = opp.SalesStage,
        Probability = opp.Probability,
        ExpectedClosingDate = opp.ExpectedClosingDate,
        OpportunityAmount = opp.OpportunityAmount,
        CurrencyCode = opp.CurrencyCode,
        CompanyId = opp.CompanyId,
        AssignedUserId = opp.AssignedUserId,
        Territory = opp.Territory,
        LostReason = opp.LostReason,
        Notes = opp.Notes,
        CreationTime = opp.CreationTime,
        LastModificationTime = opp.LastModificationTime,
        Items = opp.Items.Select(i => new OpportunityItemDto
        {
            Id = i.Id,
            ItemId = i.ItemId,
            Description = i.Description,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Amount = i.Amount,
            Uom = i.Uom,
        }).ToList(),
    };

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
}
