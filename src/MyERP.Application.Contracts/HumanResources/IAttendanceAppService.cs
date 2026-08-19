using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface IAttendanceAppService : IApplicationService
{
    Task<AttendanceDto> GetAsync(Guid id);
    Task<PagedResultDto<AttendanceDto>> GetListAsync(GetAttendanceListDto input);
    Task<AttendanceDto> CreateAsync(CreateAttendanceDto input);
    Task<AttendanceDto> UpdateAsync(Guid id, CreateAttendanceDto input);
    Task DeleteAsync(Guid id);
}
