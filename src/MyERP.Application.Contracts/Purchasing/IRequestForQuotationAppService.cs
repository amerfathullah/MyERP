using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IRequestForQuotationAppService : IApplicationService
{
    Task<RfqDto> GetAsync(Guid id);
    Task<PagedResultDto<RfqDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<RfqDto> CreateAsync(CreateRfqDto input);
    Task<RfqDto> SubmitAsync(Guid id);
    Task<RfqDto> CancelAsync(Guid id);
}
