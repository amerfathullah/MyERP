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

[Authorize(MyERPPermissions.Leads.Default)]
public class IndustryTypeAppService : ApplicationService, IIndustryTypeAppService
{
    private readonly IRepository<IndustryType, Guid> _repository;

    public IndustryTypeAppService(IRepository<IndustryType, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<IndustryTypeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<IndustryTypeDto>> GetListAsync(GetIndustryTypeListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(i => i.Name.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderBy(i => i.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<IndustryTypeDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Leads.Create)]
    public async Task<IndustryTypeDto> CreateAsync(CreateUpdateIndustryTypeDto input)
    {
        var entity = new IndustryType(GuidGenerator.Create(), input.Name, CurrentTenant.Id);
        await _repository.InsertAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<IndustryTypeDto> UpdateAsync(Guid id, CreateUpdateIndustryTypeDto input)
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

    private static IndustryTypeDto MapToDto(IndustryType e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        CreationTime = e.CreationTime,
        LastModificationTime = e.LastModificationTime,
    };
}

[Authorize(MyERPPermissions.Leads.Default)]
public class SalesStageAppService : ApplicationService, ISalesStageAppService
{
    private readonly IRepository<SalesStage, Guid> _repository;

    public SalesStageAppService(IRepository<SalesStage, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<SalesStageDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<SalesStageDto>> GetListAsync(GetSalesStageListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(s => s.StageName.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderBy(s => s.SortOrder).ThenBy(s => s.StageName)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<SalesStageDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Leads.Create)]
    public async Task<SalesStageDto> CreateAsync(CreateUpdateSalesStageDto input)
    {
        var entity = new SalesStage(GuidGenerator.Create(), input.StageName, input.SortOrder, CurrentTenant.Id);
        await _repository.InsertAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<SalesStageDto> UpdateAsync(Guid id, CreateUpdateSalesStageDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.StageName = input.StageName;
        entity.SortOrder = input.SortOrder;
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static SalesStageDto MapToDto(SalesStage e) => new()
    {
        Id = e.Id,
        StageName = e.StageName,
        SortOrder = e.SortOrder,
        CreationTime = e.CreationTime,
        LastModificationTime = e.LastModificationTime,
    };
}
