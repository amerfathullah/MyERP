using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface ILeaveAppService : IApplicationService
{
    Task<List<LeaveTypeDto>> GetLeaveTypesAsync();
    Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto input);
    Task<LeaveApplicationDto> GetAsync(Guid id);
    Task<PagedResultDto<LeaveApplicationDto>> GetListAsync(GetLeaveListDto input);
    Task<LeaveApplicationDto> ApplyAsync(CreateLeaveApplicationDto input);
    Task<LeaveApplicationDto> ApproveAsync(Guid id);
    Task<LeaveApplicationDto> RejectAsync(Guid id);
    Task<LeaveApplicationDto> CancelAsync(Guid id);
}
