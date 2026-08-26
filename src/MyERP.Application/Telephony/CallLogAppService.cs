using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Telephony.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Telephony;

[Authorize(MyERPPermissions.CallLogs.Default)]
public class CallLogAppService : MyERPAppService, ICallLogAppService
{
    private readonly IRepository<CallLog, Guid> _repository;

    public CallLogAppService(IRepository<CallLog, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CallLogDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new CallLogMapper().Map(entity);
    }

    public async Task<PagedResultDto<CallLogDto>> GetListAsync(GetCallLogListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CallDirection.HasValue)
        {
            query = query.Where(x => x.CallDirection == input.CallDirection.Value);
        }

        if (input.Status.HasValue)
        {
            query = query.Where(x => x.Status == input.Status.Value);
        }

        if (input.TelephonyCallTypeId.HasValue)
        {
            query = query.Where(x => x.TelephonyCallTypeId == input.TelephonyCallTypeId.Value);
        }

        if (input.CustomerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == input.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.CallId.ToLower().Contains(filter) ||
                                     x.From.ToLower().Contains(filter) ||
                                     x.To.ToLower().Contains(filter));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.StartTime ?? x.CreationTime)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new CallLogMapper().Map).ToList();
        return new PagedResultDto<CallLogDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.CallLogs.Create)]
    public async Task<CallLogDto> CreateAsync(CreateCallLogDto input)
    {
        var entity = new CallLog(
            GuidGenerator.Create(),
            input.CallId.Trim(),
            input.From.Trim(),
            input.To.Trim(),
            input.CallDirection,
            input.Status,
            input.StartTime,
            input.Medium?.Trim(),
            input.CustomerId,
            input.EmployeeUserId,
            input.CallReceivedByEmployeeId,
            input.TelephonyCallTypeId,
            CurrentTenant.Id);

        entity.SetSummary(input.Summary);

        await _repository.InsertAsync(entity);
        return new CallLogMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CallLogs.Edit)]
    public async Task<CallLogDto> UpdateAsync(Guid id, UpdateCallLogDto input)
    {
        var entity = await _repository.GetAsync(id);

        entity.Status = input.Status;
        entity.Duration = input.Duration;
        entity.EndTime = input.EndTime;
        entity.RecordingUrl = input.RecordingUrl;
        entity.CustomerId = input.CustomerId;
        entity.EmployeeUserId = input.EmployeeUserId;
        entity.CallReceivedByEmployeeId = input.CallReceivedByEmployeeId;
        entity.TelephonyCallTypeId = input.TelephonyCallTypeId;
        entity.SetSummary(input.Summary);

        await _repository.UpdateAsync(entity);
        return new CallLogMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CallLogs.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    public async Task<CallLogDto> StartCallAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.StartCall();
        await _repository.UpdateAsync(entity);
        return new CallLogMapper().Map(entity);
    }

    public async Task<CallLogDto> CompleteCallAsync(Guid id, int durationSeconds, string? recordingUrl = null)
    {
        var entity = await _repository.GetAsync(id);
        entity.CompleteCall(durationSeconds, recordingUrl);
        await _repository.UpdateAsync(entity);
        return new CallLogMapper().Map(entity);
    }

    public async Task<CallLogDto> FailCallAsync(Guid id, CallStatus failureStatus)
    {
        var entity = await _repository.GetAsync(id);
        entity.FailCall(failureStatus);
        await _repository.UpdateAsync(entity);
        return new CallLogMapper().Map(entity);
    }
}
