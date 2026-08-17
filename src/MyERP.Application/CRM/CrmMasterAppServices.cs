using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.Leads.Default)]
public class CompetitorAppService : ApplicationService, ICompetitorAppService
{
    private readonly IRepository<Competitor, Guid> _repository;

    public CompetitorAppService(IRepository<Competitor, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CompetitorDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<CompetitorDto>> GetListAsync(GetCompetitorListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(c => c.Name.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderBy(c => c.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<CompetitorDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Leads.Create)]
    public async Task<CompetitorDto> CreateAsync(CreateUpdateCompetitorDto input)
    {
        var entity = new Competitor(GuidGenerator.Create(), input.Name, CurrentTenant.Id)
        {
            Website = input.Website,
        };
        await _repository.InsertAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<CompetitorDto> UpdateAsync(Guid id, CreateUpdateCompetitorDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Name = input.Name;
        entity.Website = input.Website;
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static CompetitorDto MapToDto(Competitor e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Website = e.Website,
        CreationTime = e.CreationTime,
        LastModificationTime = e.LastModificationTime,
    };
}

[Authorize(MyERPPermissions.Leads.Default)]
public class MarketSegmentAppService : ApplicationService, IMarketSegmentAppService
{
    private readonly IRepository<MarketSegment, Guid> _repository;

    public MarketSegmentAppService(IRepository<MarketSegment, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<MarketSegmentDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<MarketSegmentDto>> GetListAsync(GetMarketSegmentListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(m => m.Name.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderBy(m => m.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<MarketSegmentDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Leads.Create)]
    public async Task<MarketSegmentDto> CreateAsync(CreateUpdateMarketSegmentDto input)
    {
        var entity = new MarketSegment(GuidGenerator.Create(), input.Name, CurrentTenant.Id);
        await _repository.InsertAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<MarketSegmentDto> UpdateAsync(Guid id, CreateUpdateMarketSegmentDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Name = input.Name;
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static MarketSegmentDto MapToDto(MarketSegment e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        CreationTime = e.CreationTime,
        LastModificationTime = e.LastModificationTime,
    };
}
