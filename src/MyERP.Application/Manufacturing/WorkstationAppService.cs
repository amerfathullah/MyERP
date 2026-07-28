using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
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
            var f = input.Filter;
            query = query.Where(x => x.Name.Contains(f) ||
                                     (x.WorkstationType != null && x.WorkstationType.Contains(f)));
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

    /// <summary>
    /// Updates workstation settings. When hour rate changes, cascades the new rate
    /// to all BOM Operations referencing this workstation (per PR eb9afa40ea).
    /// ERPNext: workstation.update_bom_operation() sets both hour_rate AND operating_cost.
    /// </summary>
    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<WorkstationDto> UpdateAsync(Guid id, CreateWorkstationDto input)
    {
        var ws = await _repository.GetAsync(id);
        var previousHourRate = ws.HourRate;

        ws.Name = input.Name;
        ws.WorkstationType = input.WorkstationType;
        ws.ProductionCapacity = input.ProductionCapacity;
        ws.Description = input.Description;

        await _repository.UpdateAsync(ws);

        // Per upstream PR eb9afa40ea: propagate hour_rate + operating_cost to BOM Operations
        if (ws.HourRate != previousHourRate)
        {
            await PropagateHourRateToBomOperationsAsync(ws.Id, ws.HourRate);
        }

        return ObjectMapper.Map<Workstation, WorkstationDto>(ws);
    }

    /// <summary>
    /// When workstation hour_rate changes, updates all BOM Operations that reference
    /// this workstation with: hour_rate = new rate, operating_cost = rate * time_in_mins / 60.
    /// Per ERPNext workstation.py update_bom_operation() + PR eb9afa40ea fix.
    /// </summary>
    private async Task PropagateHourRateToBomOperationsAsync(Guid workstationId, decimal newHourRate)
    {
        var bomOpRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<BomOperation, Guid>>();
        var query = await bomOpRepo.GetQueryableAsync();
        var affectedOps = query.Where(op => op.WorkstationId == workstationId).ToList();

        foreach (var op in affectedOps)
        {
            op.CalculateCost(newHourRate);
        }

        if (affectedOps.Count > 0)
        {
            Logger.LogInformation(
                "Propagated hour rate {Rate} to {Count} BOM operations for workstation {WsId}",
                newHourRate, affectedOps.Count, workstationId);
        }
    }
}

