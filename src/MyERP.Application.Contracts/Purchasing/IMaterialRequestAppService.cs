using System;
using System.Threading.Tasks;
using MyERP.Purchasing.DTOs;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IMaterialRequestAppService : IApplicationService
{
    Task<MaterialRequestDto> GetAsync(Guid id);
    Task<PagedResultDto<MaterialRequestDto>> GetListAsync(GetMaterialRequestListDto input);
    Task<MaterialRequestDto> CreateAsync(CreateMaterialRequestDto input);
    Task DeleteAsync(Guid id);
    Task<MaterialRequestDto> SubmitAsync(Guid id);
    Task<MaterialRequestDto> CancelAsync(Guid id);
}
