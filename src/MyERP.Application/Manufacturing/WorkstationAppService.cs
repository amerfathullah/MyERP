using System;
using System.Collections.Generic;
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
        if (input.ProductionCapacity < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "ProductionCapacity");
        }

        var ws = new Workstation(GuidGenerator.Create(), input.CompanyId, input.Name, CurrentTenant.Id)
        {
            WorkstationType = input.WorkstationType,
            ProductionCapacity = input.ProductionCapacity,
            Description = input.Description,
        };
        ws.ReplaceCosts(input.Costs.Select(c => (c.Component, c.OperatingCost)));
        await _repository.InsertAsync(ws);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Workstation", ws.Id,
            "Created", ws.CompanyId,
            ws.Name, "Draft", "Active", CurrentUser.Id,
            $"Workstation '{ws.Name}' created (Capacity: {ws.ProductionCapacity})", CurrentTenant.Id));

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
        if (input.ProductionCapacity < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "ProductionCapacity");
        }

        var ws = await _repository.GetAsync(id);
        var previousHourRate = ws.HourRate;

        ws.Name = input.Name;
        ws.WorkstationType = input.WorkstationType;
        ws.ProductionCapacity = input.ProductionCapacity;
        ws.Description = input.Description;
        ws.ReplaceCosts(input.Costs.Select(c => (c.Component, c.OperatingCost)));

        await _repository.UpdateAsync(ws);

        // Per upstream PR eb9afa40ea: propagate hour_rate + operating_cost to BOM Operations
        if (ws.HourRate != previousHourRate)
        {
            await PropagateHourRateToBomOperationsAsync(ws.Id, ws.HourRate);
        }

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Workstation", ws.Id,
            "Updated", ws.CompanyId,
            ws.Name, "Active", "Active", CurrentUser.Id,
            $"Workstation '{ws.Name}' updated", CurrentTenant.Id));

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

    public async Task<List<WorkstationUtilizationDto>> GetCapacityUtilizationAsync(Guid? companyId)
    {
        var wsQuery = await _repository.GetQueryableAsync();
        if (companyId.HasValue)
            wsQuery = wsQuery.Where(w => w.CompanyId == companyId.Value);
        var workstations = wsQuery.Where(w => w.IsActive).OrderBy(w => w.Name).ToList();

        if (workstations.Count == 0)
            return new List<WorkstationUtilizationDto>();

        var jcRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<JobCard, Guid>>();
        var jcQuery = await jcRepo.GetQueryableAsync();
        var activeJcs = jcQuery.Where(jc => jc.Status == JobCardStatus.Open || jc.Status == JobCardStatus.WorkInProgress).ToList();

        var wsIds = workstations.Select(w => w.Id).ToHashSet();
        var relevantJcs = activeJcs.Where(jc => jc.WorkstationId.HasValue && wsIds.Contains(jc.WorkstationId.Value)).ToList();

        var jcsByWorkstation = relevantJcs.GroupBy(jc => jc.WorkstationId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<WorkstationUtilizationDto>();
        foreach (var ws in workstations)
        {
            var dto = new WorkstationUtilizationDto
            {
                WorkstationId = ws.Id,
                WorkstationName = ws.Name,
                WorkstationType = ws.WorkstationType,
                ProductionCapacity = ws.ProductionCapacity,
            };

            if (jcsByWorkstation.TryGetValue(ws.Id, out var wsJobs))
            {
                dto.ActiveJobCards = wsJobs.Count;
                dto.TotalPlannedMinutes = wsJobs.Sum(j => j.PlannedTimeInMins);
                dto.TotalActualMinutes = wsJobs.Sum(j => j.TotalTimeInMins);
                dto.UtilizationPercent = ws.ProductionCapacity > 0
                    ? Math.Min(100, Math.Round((decimal)wsJobs.Count / ws.ProductionCapacity * 100, 0))
                    : 0;
                dto.Status = dto.UtilizationPercent >= 100 ? "Full" : "Active";
                dto.ActiveJobs = wsJobs.Select(jc => new ActiveJobOnWorkstationDto
                {
                    JobCardId = jc.Id,
                    ForQuantity = jc.ForQuantity,
                    CompletedQty = jc.CompletedQty,
                    PlannedTimeInMins = jc.PlannedTimeInMins,
                    ActualTimeInMins = jc.TotalTimeInMins,
                    Status = (int)jc.Status,
                }).ToList();
            }
            else
            {
                dto.Status = "Idle";
            }

            result.Add(dto);
        }

        return result;
    }
}
