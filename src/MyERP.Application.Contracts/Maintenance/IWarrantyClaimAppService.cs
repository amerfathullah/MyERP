using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Maintenance;

public interface IWarrantyClaimAppService : IApplicationService
{
    Task<PagedResultDto<WarrantyClaimDto>> GetListAsync(GetWarrantyClaimListDto input);
    Task<WarrantyClaimDto> GetAsync(Guid id);
    Task<WarrantyClaimDto> CreateAsync(CreateWarrantyClaimDto input);
    Task StartWorkAsync(Guid id);
    Task CloseAsync(Guid id, string? resolution);
    Task CancelAsync(Guid id);
    Task<Guid> CreateMaintenanceVisitAsync(Guid id);
}
