using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IQuotationLostReasonAppService : IApplicationService
{
    Task<QuotationLostReasonDto> GetAsync(Guid id);
    Task<PagedResultDto<QuotationLostReasonDto>> GetListAsync(GetQuotationLostReasonListDto input);
    Task<List<QuotationLostReasonDto>> GetAllListAsync();
    Task<QuotationLostReasonDto> CreateAsync(CreateUpdateQuotationLostReasonDto input);
    Task<QuotationLostReasonDto> UpdateAsync(Guid id, CreateUpdateQuotationLostReasonDto input);
    Task DeleteAsync(Guid id);
}
