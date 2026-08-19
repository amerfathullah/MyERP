using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface IShiftAssignmentAppService : IApplicationService
{
    Task<ShiftAssignmentDto> GetAsync(Guid id);
    Task<PagedResultDto<ShiftAssignmentDto>> GetListAsync(GetShiftAssignmentListDto input);
    Task<ShiftAssignmentDto> CreateAsync(CreateShiftAssignmentDto input);
    Task<ShiftAssignmentDto> UpdateAsync(Guid id, CreateShiftAssignmentDto input);
    Task DeleteAsync(Guid id);
}
