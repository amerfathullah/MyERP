using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface ISupplierQuotationAppService : IApplicationService
{
    Task<PagedResultDto<SupplierQuotationDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<SupplierQuotationDto> GetAsync(Guid id);
    Task<SupplierQuotationDto> CreateAsync(CreateSupplierQuotationDto input);
    Task<SupplierQuotationDto> SubmitAsync(Guid id);
    Task<SupplierQuotationDto> CancelAsync(Guid id);
}
