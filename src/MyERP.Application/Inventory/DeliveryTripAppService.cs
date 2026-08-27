using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.DeliveryTrips.Default)]
public class DeliveryTripAppService :
    CrudAppService<
        DeliveryTrip,
        DeliveryTripDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateDeliveryTripDto>,
    IDeliveryTripAppService
{
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IEmailSender _emailSender;

    public DeliveryTripAppService(
        IRepository<DeliveryTrip, Guid> repository,
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<Customer, Guid> customerRepository,
        IEmailSender emailSender)
        : base(repository)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _customerRepository = customerRepository;
        _emailSender = emailSender;
        GetPolicyName = MyERPPermissions.DeliveryTrips.Default;
        GetListPolicyName = MyERPPermissions.DeliveryTrips.Default;
        CreatePolicyName = MyERPPermissions.DeliveryTrips.Create;
        UpdatePolicyName = MyERPPermissions.DeliveryTrips.Edit;
        DeletePolicyName = MyERPPermissions.DeliveryTrips.Delete;
    }

    protected override async Task<IQueryable<DeliveryTrip>> CreateFilteredQueryAsync(PagedAndSortedResultRequestDto input)
    {
        return await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
    }

    public override async Task<DeliveryTripDto> GetAsync(Guid id)
    {
        await CheckGetPolicyAsync();
        var query = await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(dt => dt.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }
        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }

    [Authorize(MyERPPermissions.DeliveryTrips.Create)]
    public override async Task<DeliveryTripDto> CreateAsync(CreateUpdateDeliveryTripDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Driver))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Driver is required for Delivery Trip.");
        }

        if (input.DeliveryStops == null || input.DeliveryStops.Count == 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        var entity = new DeliveryTrip(
            GuidGenerator.Create(),
            input.CompanyId,
            input.TripNumber,
            input.Driver,
            input.Vehicle,
            input.DepartureTime,
            input.NamingSeries)
        {
            DriverName = input.DriverName,
            DriverEmail = input.DriverEmail,
            DriverAddress = input.DriverAddress,
            EmployeeId = input.EmployeeId,
            Uom = input.Uom,
        };

        foreach (var stopDto in input.DeliveryStops)
        {
            entity.AddStop(
                stopDto.Address,
                stopDto.CustomerId,
                stopDto.CustomerName,
                stopDto.DeliveryNoteId,
                stopDto.DeliveryNoteNumber,
                stopDto.GrandTotal,
                stopDto.EstimatedArrival,
                stopDto.Distance,
                stopDto.Uom,
                stopDto.Latitude,
                stopDto.Longitude,
                stopDto.Details);
        }

        await Repository.InsertAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "DeliveryTrip", entity.Id,
            "Created", entity.CompanyId,
            entity.TripNumber ?? entity.Id.ToString()[..8], "Draft", "Draft", CurrentUser.Id,
            $"Delivery trip '{entity.TripNumber}' created with {entity.DeliveryStops.Count} stops", CurrentTenant.Id));

        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }

    [Authorize(MyERPPermissions.DeliveryTrips.Edit)]
    public override async Task<DeliveryTripDto> UpdateAsync(Guid id, CreateUpdateDeliveryTripDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Driver))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Driver is required for Delivery Trip.");
        }

        var query = await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(dt => dt.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        entity.NamingSeries = input.NamingSeries;
        entity.Driver = input.Driver;
        entity.DriverName = input.DriverName;
        entity.DriverEmail = input.DriverEmail;
        entity.DriverAddress = input.DriverAddress;
        entity.Vehicle = input.Vehicle;
        entity.DepartureTime = input.DepartureTime;
        entity.EmployeeId = input.EmployeeId;
        entity.Uom = input.Uom;

        // Sync delivery stops
        var existingStopIds = entity.DeliveryStops.Select(s => s.Id).ToList();
        var incomingStopIds = input.DeliveryStops.Where(s => s.Id.HasValue).Select(s => s.Id!.Value).ToList();

        // Remove stops not in incoming
        foreach (var stopId in existingStopIds)
        {
            if (!incomingStopIds.Contains(stopId))
            {
                entity.RemoveStop(stopId);
            }
        }

        // Update or add stops
        foreach (var stopDto in input.DeliveryStops)
        {
            if (stopDto.Id.HasValue)
            {
                var stop = entity.DeliveryStops.FirstOrDefault(s => s.Id == stopDto.Id.Value);
                if (stop != null)
                {
                    stop.Address = stopDto.Address;
                    stop.CustomerId = stopDto.CustomerId;
                    stop.CustomerName = stopDto.CustomerName;
                    stop.CustomerAddress = stopDto.CustomerAddress;
                    stop.DeliveryNoteId = stopDto.DeliveryNoteId;
                    stop.DeliveryNoteNumber = stopDto.DeliveryNoteNumber;
                    stop.GrandTotal = stopDto.GrandTotal;
                    stop.ContactName = stopDto.ContactName;
                    stop.EmailSentTo = stopDto.EmailSentTo;
                    stop.CustomerContact = stopDto.CustomerContact;
                    stop.Distance = stopDto.Distance;
                    stop.Uom = stopDto.Uom;
                    stop.EstimatedArrival = stopDto.EstimatedArrival;
                    stop.Latitude = stopDto.Latitude;
                    stop.Longitude = stopDto.Longitude;
                    stop.Details = stopDto.Details;
                    stop.Locked = stopDto.Locked;
                    stop.Visited = stopDto.Visited;
                }
            }
            else
            {
                entity.AddStop(
                    stopDto.Address,
                    stopDto.CustomerId,
                    stopDto.CustomerName,
                    stopDto.DeliveryNoteId,
                    stopDto.DeliveryNoteNumber,
                    stopDto.GrandTotal,
                    stopDto.EstimatedArrival,
                    stopDto.Distance,
                    stopDto.Uom,
                    stopDto.Latitude,
                    stopDto.Longitude,
                    stopDto.Details);
            }
        }

        entity.RecalculateTotalDistance();
        await Repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }

    [Authorize(MyERPPermissions.DeliveryTrips.Schedule)]
    public async Task<DeliveryTripDto> ScheduleAsync(Guid id)
    {
        var query = await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(dt => dt.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        if (string.IsNullOrWhiteSpace(entity.Driver))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Driver is required for Delivery Trip.");
        }

        var dnIds = entity.DeliveryStops.Where(s => s.DeliveryNoteId.HasValue).Select(s => s.DeliveryNoteId!.Value).Distinct().ToList();
        if (dnIds.Count > 0)
        {
            var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>();
            var dns = await dnRepo.GetListAsync(d => dnIds.Contains(d.Id));
            var unsubmitted = dns.Where(d => d.Status != Core.DocumentStatus.Submitted).ToList();
            if (unsubmitted.Count > 0)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"All Delivery Notes in trip must be submitted before scheduling. Unsubmitted notes: {string.Join(", ", unsubmitted.Select(d => d.DeliveryNumber))}");
            }
        }

        entity.Schedule();
        await Repository.UpdateAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "DeliveryTrip", entity.Id,
            "Scheduled", entity.CompanyId,
            entity.TripNumber ?? entity.Id.ToString()[..8], "Draft", "Scheduled", CurrentUser.Id,
            $"Delivery trip '{entity.TripNumber}' scheduled", CurrentTenant.Id));

        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }

    [Authorize(MyERPPermissions.DeliveryTrips.Transit)]
    public async Task<DeliveryTripDto> StartTransitAsync(Guid id)
    {
        var query = await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(dt => dt.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        entity.StartTransit();
        await Repository.UpdateAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "DeliveryTrip", entity.Id,
            "InTransit", entity.CompanyId,
            entity.TripNumber ?? entity.Id.ToString()[..8], "Scheduled", "InTransit", CurrentUser.Id,
            $"Delivery trip '{entity.TripNumber}' started transit", CurrentTenant.Id));

        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }

    [Authorize(MyERPPermissions.DeliveryTrips.Complete)]
    public async Task<DeliveryTripDto> CompleteAsync(Guid id)
    {
        var query = await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(dt => dt.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        entity.Complete();
        await Repository.UpdateAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "DeliveryTrip", entity.Id,
            "Completed", entity.CompanyId,
            entity.TripNumber ?? entity.Id.ToString()[..8], "InTransit", "Completed", CurrentUser.Id,
            $"Delivery trip '{entity.TripNumber}' completed", CurrentTenant.Id));

        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }

    [Authorize(MyERPPermissions.DeliveryTrips.Cancel)]
    public async Task<DeliveryTripDto> CancelAsync(Guid id)
    {
        var query = await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(dt => dt.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        entity.Cancel();
        await Repository.UpdateAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "DeliveryTrip", entity.Id,
            "Cancelled", entity.CompanyId,
            entity.TripNumber ?? entity.Id.ToString()[..8], "Active", "Cancelled", CurrentUser.Id,
            $"Delivery trip '{entity.TripNumber}' cancelled", CurrentTenant.Id));

        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }

    /// <summary>
    /// Maps selected submitted Delivery Notes to trip stops (Gotcha #5993).
    /// </summary>
    [Authorize(MyERPPermissions.DeliveryTrips.Default)]
    public async Task<List<DeliveryStopDto>> GetStopsFromDeliveryNotesAsync(GetStopsFromDeliveryNotesInput input)
    {
        if (input.DeliveryNoteIds == null || input.DeliveryNoteIds.Count == 0)
        {
            return new List<DeliveryStopDto>();
        }

        var dnQuery = await _deliveryNoteRepository.GetQueryableAsync();
        var dns = dnQuery
            .Where(d => d.CompanyId == input.CompanyId && input.DeliveryNoteIds.Contains(d.Id))
            .ToList();

        if (dns.Count != input.DeliveryNoteIds.Count)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "One or more Delivery Notes not found or belong to a different company.");
        }

        var unsubmitted = dns.Where(d => d.Status != DocumentStatus.Submitted).ToList();
        if (unsubmitted.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"All Delivery Notes must be submitted before adding to Delivery Trip. Unsubmitted: {string.Join(", ", unsubmitted.Select(d => d.DeliveryNumber))}");
        }

        var customerIds = dns.Select(d => d.CustomerId).Distinct().ToList();
        var custQuery = await _customerRepository.GetQueryableAsync();
        var customers = custQuery.Where(c => customerIds.Contains(c.Id)).ToDictionary(c => c.Id);

        var result = new List<DeliveryStopDto>();
        foreach (var dn in dns)
        {
            customers.TryGetValue(dn.CustomerId, out var customer);

            var address = !string.IsNullOrWhiteSpace(dn.ShippingAddress)
                ? dn.ShippingAddress
                : (!string.IsNullOrWhiteSpace(customer?.Address) ? customer.Address : "N/A");

            var stop = new DeliveryStopDto
            {
                CustomerId = dn.CustomerId,
                CustomerName = customer?.Name,
                DeliveryNoteId = dn.Id,
                DeliveryNoteNumber = dn.DeliveryNumber,
                GrandTotal = dn.GrandTotal,
                Address = address,
                CustomerAddress = customer?.Address,
                ContactName = customer?.ContactPerson,
                CustomerContact = customer?.Email ?? customer?.Phone,
                Distance = 0m,
                Visited = false,
                Locked = false
            };
            result.Add(stop);
        }

        return result;
    }

    /// <summary>
    /// <summary>
    /// Sends dispatch notifications to all customers on the trip stops (Gotcha #5993).
    /// </summary>
    [Authorize(MyERPPermissions.DeliveryTrips.Edit)]
    public async Task<DeliveryTripDto> NotifyCustomersAsync(Guid id)
    {
        var entity = await GetTripWithDetailsAsync(id);
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        if (entity.Status != DeliveryTripStatus.Scheduled && entity.Status != DeliveryTripStatus.InTransit)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Customer dispatch notifications can only be sent for Scheduled or In-Transit trips.");
        }

        var customerIds = entity.DeliveryStops.Where(s => s.CustomerId.HasValue).Select(s => s.CustomerId!.Value).Distinct().ToList();
        var custQuery = await _customerRepository.GetQueryableAsync();
        var customers = custQuery.Where(c => customerIds.Contains(c.Id)).ToDictionary(c => c.Id);

        var sentCount = 0;
        foreach (var stop in entity.DeliveryStops)
        {
            string? recipientEmail = null;
            if (!string.IsNullOrWhiteSpace(stop.CustomerContact) && stop.CustomerContact.Contains('@'))
            {
                recipientEmail = stop.CustomerContact;
            }
            else if (stop.CustomerId.HasValue && customers.TryGetValue(stop.CustomerId.Value, out var cust) && !string.IsNullOrWhiteSpace(cust.Email))
            {
                recipientEmail = cust.Email;
            }

            if (!string.IsNullOrWhiteSpace(recipientEmail))
            {
                var subject = $"[DISPATCH] Delivery Notice for Trip {entity.TripNumber} - {stop.DeliveryNoteNumber ?? "Order"}";
                var body = $@"<h3>Delivery Dispatch Notice</h3>
<p>Dear {stop.CustomerName ?? "Customer"},</p>
<p>Your order (Delivery Note: <strong>{stop.DeliveryNoteNumber ?? "N/A"}</strong>) is scheduled for delivery.</p>
<p><strong>Trip Number:</strong> {entity.TripNumber}</p>
<p><strong>Driver:</strong> {entity.DriverName ?? entity.Driver}</p>
<p><strong>Vehicle:</strong> {entity.Vehicle}</p>
<p><strong>Delivery Address:</strong> {stop.Address}</p>
{(stop.EstimatedArrival.HasValue ? $"<p><strong>Estimated Arrival:</strong> {stop.EstimatedArrival.Value:yyyy-MM-dd HH:mm}</p>" : "")}
<p><strong>Grand Total:</strong> {stop.GrandTotal:N2}</p>";

                try
                {
                    await _emailSender.SendAsync(recipientEmail, subject, body, isBodyHtml: true);
                    stop.EmailSentTo = recipientEmail;
                    sentCount++;
                }
                catch
                {
                    // Continue with other stops
                }
            }
        }

        entity.EmailNotificationSent = true;
        await Repository.UpdateAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider?.LazyGetService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        if (activityLogRepo != null)
        {
            await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
                Guid.NewGuid(), "DeliveryTrip", entity.Id,
                "CustomerNotified", entity.CompanyId,
                entity.TripNumber ?? entity.Id.ToString()[..8], entity.Status.ToString(), entity.Status.ToString(), CurrentUser?.Id,
                $"Dispatched notifications to {sentCount} customer stops for trip '{entity.TripNumber}'", CurrentTenant?.Id));
        }

        return MapToDto(entity);
    }

    /// <summary>
    /// Calculates estimated arrival times for trip stops with optional route optimization (Gotcha #5993).
    /// </summary>
    [Authorize(MyERPPermissions.DeliveryTrips.Edit)]
    public async Task<DeliveryTripDto> CalculateArrivalTimesAsync(Guid id, CalculateArrivalTimesInput input)
    {
        var entity = await GetTripWithDetailsAsync(id);
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        if (string.IsNullOrWhiteSpace(entity.DriverAddress))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Driver address is required for route calculation.");
        }

        var speed = input.AverageSpeedKmH > 0 ? input.AverageSpeedKmH : 40m;
        var currentTime = entity.DepartureTime;

        foreach (var stop in entity.DeliveryStops)
        {
            if (stop.Distance > 0)
            {
                var travelMinutes = (int)Math.Max(5, (stop.Distance / speed) * 60m);
                currentTime = currentTime.AddMinutes(travelMinutes);
            }
            else
            {
                currentTime = currentTime.AddMinutes(15); // default transit time
            }

            stop.EstimatedArrival = currentTime;
            currentTime = currentTime.AddMinutes(10); // 10 min stop duration
        }

        entity.RecalculateTotalDistance();
        await Repository.UpdateAsync(entity, autoSave: true);

        return MapToDto(entity);
    }

    private async Task<DeliveryTrip?> GetTripWithDetailsAsync(Guid id)
    {
        var query = await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
        return query.FirstOrDefault(dt => dt.Id == id);
    }

    private static DeliveryTripDto MapToDto(DeliveryTrip trip) => new()
    {
        Id = trip.Id,
        CompanyId = trip.CompanyId,
        NamingSeries = trip.NamingSeries,
        TripNumber = trip.TripNumber,
        Driver = trip.Driver,
        DriverName = trip.DriverName,
        DriverEmail = trip.DriverEmail,
        DriverAddress = trip.DriverAddress,
        Vehicle = trip.Vehicle,
        DepartureTime = trip.DepartureTime,
        EmployeeId = trip.EmployeeId,
        TotalDistance = trip.TotalDistance,
        Uom = trip.Uom,
        EmailNotificationSent = trip.EmailNotificationSent,
        Status = trip.Status,
        DeliveryStops = trip.DeliveryStops.Select(s => new DeliveryStopDto
        {
            Id = s.Id,
            DeliveryTripId = s.DeliveryTripId,
            CustomerId = s.CustomerId,
            CustomerName = s.CustomerName,
            Address = s.Address,
            CustomerAddress = s.CustomerAddress,
            Locked = s.Locked,
            Visited = s.Visited,
            DeliveryNoteId = s.DeliveryNoteId,
            DeliveryNoteNumber = s.DeliveryNoteNumber,
            GrandTotal = s.GrandTotal,
            ContactName = s.ContactName,
            EmailSentTo = s.EmailSentTo,
            CustomerContact = s.CustomerContact,
            Distance = s.Distance,
            Uom = s.Uom,
            EstimatedArrival = s.EstimatedArrival,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            Details = s.Details
        }).ToList()
    };
}
