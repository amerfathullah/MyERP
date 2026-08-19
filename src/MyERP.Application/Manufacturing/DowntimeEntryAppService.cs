using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

[Authorize(MyERPPermissions.Manufacturing.Default)]
public class DowntimeEntryAppService : ApplicationService, IDowntimeEntryAppService
{
    private readonly IRepository<DowntimeEntry, Guid> _repository;

    public DowntimeEntryAppService(IRepository<DowntimeEntry, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<DowntimeEntryDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(d => d.CompanyId == input.CompanyId.Value);
        if (input.FromDate.HasValue)
            query = query.Where(d => d.FromTime >= input.FromDate.Value);
        if (input.ToDate.HasValue)
            query = query.Where(d => d.ToTime <= input.ToDate.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(d => d.FromTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<DowntimeEntryDto>(totalCount, items.Select(ObjectMapper.Map<DowntimeEntry, DowntimeEntryDto>).ToList());
    }

    public async Task<DowntimeEntryDto> GetAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        return ObjectMapper.Map<DowntimeEntry, DowntimeEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<DowntimeEntryDto> CreateAsync(CreateUpdateDowntimeEntryDto input)
    {
        if (input.ToTime < input.FromTime)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var entry = new DowntimeEntry(GuidGenerator.Create(), input.CompanyId, input.WorkstationId,
            input.OperatorId, input.FromTime, input.ToTime, input.StopReason, CurrentTenant.Id)
        {
            Remarks = input.Remarks,
        };
        await _repository.InsertAsync(entry);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "DowntimeEntry", entry.Id,
            "Created", entry.CompanyId,
            entry.Id.ToString()[..8], "Draft", "Active", CurrentUser.Id,
            $"Downtime entry created ({entry.DowntimeMinutes} mins, Reason: {entry.StopReason})", CurrentTenant.Id));

        return ObjectMapper.Map<DowntimeEntry, DowntimeEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<DowntimeEntryDto> UpdateAsync(Guid id, CreateUpdateDowntimeEntryDto input)
    {
        if (input.ToTime < input.FromTime)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var entry = await _repository.GetAsync(id);
        entry.WorkstationId = input.WorkstationId;
        entry.OperatorId = input.OperatorId;
        entry.SetTimeRange(input.FromTime, input.ToTime);
        entry.StopReason = input.StopReason;
        entry.Remarks = input.Remarks;
        await _repository.UpdateAsync(entry);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "DowntimeEntry", entry.Id,
            "Updated", entry.CompanyId,
            entry.Id.ToString()[..8], "Active", "Active", CurrentUser.Id,
            $"Downtime entry updated ({entry.DowntimeMinutes} mins)", CurrentTenant.Id));

        return ObjectMapper.Map<DowntimeEntry, DowntimeEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.Manufacturing.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
