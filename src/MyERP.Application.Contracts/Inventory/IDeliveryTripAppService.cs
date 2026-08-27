using System;
using System.Collections.Generic;
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

    /// <summary>Maps selected submitted Delivery Notes to trip stops (Gotcha #5993).</summary>
    Task<List<DeliveryStopDto>> GetStopsFromDeliveryNotesAsync(GetStopsFromDeliveryNotesInput input);

    /// <summary>Sends dispatch notifications to all customers on the trip stops (Gotcha #5993).</summary>
    Task<DeliveryTripDto> NotifyCustomersAsync(Guid id);

    /// <summary>Calculates estimated arrival times for trip stops with optional route optimization (Gotcha #5993).</summary>
    Task<DeliveryTripDto> CalculateArrivalTimesAsync(Guid id, CalculateArrivalTimesInput input);
}
