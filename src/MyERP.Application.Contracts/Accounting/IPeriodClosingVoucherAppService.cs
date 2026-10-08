using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IPeriodClosingVoucherAppService : IApplicationService
{
    Task<PagedResultDto<PeriodClosingVoucherDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<PeriodClosingVoucherDto> GetAsync(Guid id);
    Task<PcvGlEntryDto[]> GetGlEntriesAsync(Guid id);
    Task<PeriodClosingStockDifferenceDto> GetStockValueDifferenceAsync(Guid id);
    Task<PeriodClosingVoucherDto> CreateAsync(CreatePeriodClosingVoucherDto input);
    Task<PeriodClosingVoucherDto> SubmitAsync(Guid id, SubmitPeriodClosingVoucherDto? input = null);
    Task<PeriodClosingVoucherDto> CancelAsync(Guid id);
}
