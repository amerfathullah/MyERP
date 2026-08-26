using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Telephony;

public interface ICallLogAppService : ICrudAppService<CallLogDto, Guid, GetCallLogListDto, CreateCallLogDto, UpdateCallLogDto>
{
    Task<CallLogDto> StartCallAsync(Guid id);
    Task<CallLogDto> CompleteCallAsync(Guid id, int durationSeconds, string? recordingUrl = null);
    Task<CallLogDto> FailCallAsync(Guid id, CallStatus failureStatus);
}
