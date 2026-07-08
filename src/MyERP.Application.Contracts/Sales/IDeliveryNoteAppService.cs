using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IDeliveryNoteAppService : IApplicationService
{
    Task<DeliveryNoteDto> GetAsync(Guid id);
    Task<PagedResultDto<DeliveryNoteDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<DeliveryNoteDto> CreateAsync(CreateDeliveryNoteDto input);
    Task<DeliveryNoteDto> SubmitAsync(Guid id);
    Task<DeliveryNoteDto> CancelAsync(Guid id);
}
