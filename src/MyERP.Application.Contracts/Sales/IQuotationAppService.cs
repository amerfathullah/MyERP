using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IQuotationAppService : IApplicationService
{
    Task<QuotationDto> GetAsync(Guid id);
    Task<PagedResultDto<QuotationDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<QuotationDto> CreateAsync(CreateQuotationDto input);
    Task<QuotationDto> SubmitAsync(Guid id);
    Task<QuotationDto> CancelAsync(Guid id);
}
