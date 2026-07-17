using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

[Authorize(MyERPPermissions.Manufacturing.Default)]
[RemoteService(false)]
public class WorkstationAppService : ApplicationService, IWorkstationAppService
{
    private readonly IRepository<Workstation, Guid> _repository;

    public WorkstationAppService(IRepository<Workstation, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<WorkstationDto> GetAsync(Guid id)
    {
        var ws = await _repository.GetAsync(id);
        return ObjectMapper.Map<Workstation, WorkstationDto>(ws);
    }

    public async Task<PagedResultDto<WorkstationDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(f) ||
                                     (x.WorkstationType != null && x.WorkstationType.ToLower().Contains(f)));
        }

        var count = query.Count();
        var items = query.OrderBy(x => x.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<WorkstationDto>(count, items.Select(ObjectMapper.Map<Workstation, WorkstationDto>).ToList());
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<WorkstationDto> CreateAsync(CreateWorkstationDto input)
    {
        var ws = new Workstation(GuidGenerator.Create(), input.CompanyId, input.Name, CurrentTenant.Id)
        {
            WorkstationType = input.WorkstationType,
            ProductionCapacity = input.ProductionCapacity,
            Description = input.Description,
        };
        await _repository.InsertAsync(ws);
        return ObjectMapper.Map<Workstation, WorkstationDto>(ws);
    }
}
