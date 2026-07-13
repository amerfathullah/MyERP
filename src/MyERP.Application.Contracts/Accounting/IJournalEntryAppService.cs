using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IJournalEntryAppService : IApplicationService
{
    Task<JournalEntryDto> GetAsync(Guid id);
    Task<PagedResultDto<JournalEntryDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<JournalEntryDto> CreateAsync(CreateJournalEntryDto input);
    Task<JournalEntryDto> PostAsync(Guid id);
    Task<JournalEntryDto> CancelAsync(Guid id);
}
