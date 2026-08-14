using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface ILeaveAllocationAppService : IApplicationService
{
    Task<PagedResultDto<LeaveAllocationDto>> GetListAsync(GetLeaveAllocationListDto input);
    Task<LeaveAllocationDto> GetAsync(Guid id);
    Task<decimal> GetBalanceAsync(Guid employeeId, Guid leaveTypeId, DateTime asOfDate);
    Task<LeaveAllocationDto> CreateAsync(CreateLeaveAllocationDto input);
    Task<int> BulkAllocateAsync(BulkLeaveAllocationDto input);
    Task DeleteAsync(Guid id);
}
