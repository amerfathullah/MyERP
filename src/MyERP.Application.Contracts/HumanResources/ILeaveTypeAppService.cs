using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface ILeaveTypeAppService : IApplicationService
{
    Task<PagedResultDto<LeaveTypeDetailDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<LeaveTypeDetailDto> GetAsync(Guid id);
    Task<LeaveTypeDetailDto> CreateAsync(CreateUpdateLeaveTypeDto input);
    Task<LeaveTypeDetailDto> UpdateAsync(Guid id, CreateUpdateLeaveTypeDto input);
    Task DeleteAsync(Guid id);
}
