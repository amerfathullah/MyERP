using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IPosOpeningAppService : IApplicationService
{
    Task<PosOpeningDto> GetAsync(Guid id);
    Task<PagedResultDto<PosOpeningDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<PosOpeningDto> CreateAsync(CreatePosOpeningDto input);
    Task<PosOpeningDto?> GetCurrentOpenAsync(Guid userId);
    Task<PosOpeningDto> CancelAsync(Guid id);
}
