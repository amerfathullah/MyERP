using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IPaymentTermsTemplateAppService : IApplicationService
{
    Task<PagedResultDto<PaymentTermsTemplateDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<PaymentTermsTemplateDto> GetAsync(Guid id);
    Task<PaymentTermsTemplateDto> CreateAsync(CreateUpdatePaymentTermsTemplateDto input);
    Task<PaymentTermsTemplateDto> UpdateAsync(Guid id, CreateUpdatePaymentTermsTemplateDto input);
    Task DeleteAsync(Guid id);
}
