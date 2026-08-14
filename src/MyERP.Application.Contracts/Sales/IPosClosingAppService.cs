using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IPosClosingAppService : IApplicationService
{
    Task<PosClosingDto> GetAsync(Guid id);
    Task<PagedResultDto<PosClosingDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<PosClosingDto> CreateAsync(CreatePosClosingDto input);
    Task<PosClosingDto> SubmitAsync(Guid id);
    Task<PosClosingDto> CancelAsync(Guid id);
    Task<PosClosingDto> RetryAsync(Guid id);
    Task<List<PosExpectedPaymentDto>> CalculateExpectedAmountsAsync(Guid posOpeningEntryId);
}
