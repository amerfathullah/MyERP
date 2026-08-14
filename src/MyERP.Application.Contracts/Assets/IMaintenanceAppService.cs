using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Assets;

public interface IMaintenanceAppService : IApplicationService
{
    Task<PagedResultDto<MaintenanceScheduleDto>> GetScheduleListAsync(PagedAndSortedResultRequestDto input);
    Task<MaintenanceScheduleDto> GetScheduleAsync(Guid id);
    Task<MaintenanceScheduleDto> CreateScheduleAsync(CreateMaintenanceScheduleDto input);
    Task<MaintenanceScheduleDto> SubmitScheduleAsync(Guid id);
    Task<PagedResultDto<MaintenanceVisitDto>> GetVisitListAsync(GetMaintenanceVisitListDto input);
    Task<MaintenanceVisitDto> GetVisitAsync(Guid id);
    Task<MaintenanceVisitDto> CreateVisitAsync(CreateMaintenanceVisitDto input);
    Task<MaintenanceVisitDto> UpdateVisitAsync(Guid id, CreateMaintenanceVisitDto input);
    Task<MaintenanceVisitDto> CompleteVisitAsync(Guid id);
    Task<MaintenanceVisitDto> CancelVisitAsync(Guid id);
    Task DeleteVisitAsync(Guid id);
}
