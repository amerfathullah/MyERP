using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Settings;

public interface IPrintFormatAppService : IApplicationService
{
    Task<PrintFormatDto> GetAsync(Guid id);
    Task<PagedResultDto<PrintFormatDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<PrintFormatDto> CreateAsync(CreateUpdatePrintFormatDto input);
    Task<PrintFormatDto> UpdateAsync(Guid id, CreateUpdatePrintFormatDto input);
    Task DeleteAsync(Guid id);
}
