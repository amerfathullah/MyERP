using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IDowntimeEntryAppService : IApplicationService
{
    Task<PagedResultDto<DowntimeEntryDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<DowntimeEntryDto> GetAsync(Guid id);
    Task<DowntimeEntryDto> CreateAsync(CreateUpdateDowntimeEntryDto input);
    Task<DowntimeEntryDto> UpdateAsync(Guid id, CreateUpdateDowntimeEntryDto input);
    Task DeleteAsync(Guid id);
}
