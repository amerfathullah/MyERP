using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IDeliveryTripAppService :
    ICrudAppService<
        DeliveryTripDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateDeliveryTripDto>
{
    Task<DeliveryTripDto> ScheduleAsync(Guid id);
    Task<DeliveryTripDto> StartTransitAsync(Guid id);
    Task<DeliveryTripDto> CompleteAsync(Guid id);
    Task<DeliveryTripDto> CancelAsync(Guid id);
}
