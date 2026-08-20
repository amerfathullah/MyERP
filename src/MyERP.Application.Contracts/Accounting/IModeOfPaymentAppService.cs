using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IModeOfPaymentAppService : IApplicationService
{
    Task<PagedResultDto<ModeOfPaymentDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<ModeOfPaymentDto> GetAsync(Guid id);
    Task<ModeOfPaymentDto> CreateAsync(CreateUpdateModeOfPaymentDto input);
    Task<ModeOfPaymentDto> UpdateAsync(Guid id, CreateUpdateModeOfPaymentDto input);
    Task DeleteAsync(Guid id);
}
