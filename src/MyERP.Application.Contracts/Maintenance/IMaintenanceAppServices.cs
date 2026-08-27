using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Maintenance;

public interface IMaintenanceScheduleAppService : IApplicationService
{
    Task<MaintenanceScheduleDto> GetAsync(Guid id);
    Task<PagedResultDto<MaintenanceScheduleDto>> GetListAsync(GetMaintenanceScheduleListDto input);
    Task<MaintenanceScheduleDto> CreateAsync(CreateMaintenanceScheduleDto input);
    Task<MaintenanceScheduleDto> UpdateAsync(Guid id, CreateMaintenanceScheduleDto input);
    Task DeleteAsync(Guid id);
    Task<MaintenanceScheduleDto> GenerateScheduleAsync(Guid id);
    Task<MaintenanceScheduleDto> SubmitAsync(Guid id);
    Task<MaintenanceScheduleDto> CancelAsync(Guid id);
    Task<CreateMaintenanceVisitDto> MakeMaintenanceVisitAsync(Guid id, MakeMaintenanceVisitInput? input = null);
    Task<CreateMaintenanceScheduleDto> CreateFromSalesOrderAsync(Guid salesOrderId);
    Task<MaintenanceScheduleSummaryDto> GetSummaryAsync(Guid id);
}

public interface IMaintenanceVisitAppService : IApplicationService
{
    Task<MaintenanceVisitDto> GetAsync(Guid id);
    Task<PagedResultDto<MaintenanceVisitDto>> GetListAsync(GetMaintenanceVisitListDto input);
    Task<MaintenanceVisitDto> CreateAsync(CreateMaintenanceVisitDto input);
    Task<MaintenanceVisitDto> UpdateAsync(Guid id, CreateMaintenanceVisitDto input);
    Task DeleteAsync(Guid id);
    Task<MaintenanceVisitDto> SubmitAsync(Guid id);
    Task<MaintenanceVisitDto> CancelAsync(Guid id);
}
