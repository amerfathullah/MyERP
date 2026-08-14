using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IQualityInspectionTemplateAppService : IApplicationService
{
    Task<QiTemplateDto> GetAsync(Guid id);
    Task<PagedResultDto<QiTemplateDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<QiTemplateDto> CreateAsync(CreateQiTemplateDto input);
    Task ToggleAsync(Guid id);
    Task DeleteAsync(Guid id);
}
