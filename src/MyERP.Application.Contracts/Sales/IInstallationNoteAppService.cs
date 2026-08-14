using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IInstallationNoteAppService : IApplicationService
{
    Task<InstallationNoteDto> GetAsync(Guid id);
    Task<PagedResultDto<InstallationNoteDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<InstallationNoteDto> CreateAsync(CreateInstallationNoteDto input);
    Task SubmitAsync(Guid id);
    Task CancelAsync(Guid id);
}
