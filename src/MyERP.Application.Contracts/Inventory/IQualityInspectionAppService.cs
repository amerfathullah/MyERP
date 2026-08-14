using System;
using System.Threading.Tasks;
using MyERP.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IQualityInspectionAppService : IApplicationService
{
    Task<PagedResultDto<QualityInspectionDto>> GetListAsync(GetQualityInspectionListDto input);
    Task<QualityInspectionDto> GetAsync(Guid id);
    Task<QualityInspectionDto> CreateAsync(CreateQualityInspectionDto input);
    Task<QualityInspectionDto> SubmitAsync(Guid id);
    Task<QualityInspectionDto> CancelAsync(Guid id);
}
