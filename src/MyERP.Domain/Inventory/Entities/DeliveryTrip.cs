using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

public class DeliveryTrip : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string? NamingSeries { get; set; }
    public string TripNumber { get; private set; } = null!;
    public string Driver { get; set; } = null!;
    public string? DriverName { get; set; }
    public string? DriverEmail { get; set; }
    public string? DriverAddress { get; set; }
    public string Vehicle { get; set; } = null!;
    public DateTime DepartureTime { get; set; }
    public Guid? EmployeeId { get; set; }

    public decimal TotalDistance { get; set; }
    public string? Uom { get; set; }
    public bool EmailNotificationSent { get; set; }

    public DeliveryTripStatus Status { get; private set; } = DeliveryTripStatus.Draft;

    public virtual ICollection<DeliveryStop> DeliveryStops { get; private set; }

    protected DeliveryTrip()
    {
        DeliveryStops = new Collection<DeliveryStop>();
    }

    public DeliveryTrip(
        Guid id,
        Guid companyId,
        string tripNumber,
        string driver,
        string vehicle,
        DateTime departureTime,
        string? namingSeries = "MAT-DT-.YYYY.-")
        : base(id)
    {
        CompanyId = companyId;
        TripNumber = tripNumber;
        Driver = driver;
        Vehicle = vehicle;
        DepartureTime = departureTime;
        NamingSeries = namingSeries;
        Status = DeliveryTripStatus.Draft;
        DeliveryStops = new Collection<DeliveryStop>();
    }

    public DeliveryStop AddStop(
        string address,
        Guid? customerId = null,
        string? customerName = null,
        Guid? deliveryNoteId = null,
        string? deliveryNoteNumber = null,
        decimal grandTotal = 0,
        DateTime? estimatedArrival = null,
        decimal distance = 0,
        string? uom = null,
        double? lat = null,
        double? lng = null,
        string? details = null)
    {
        if (Status != DeliveryTripStatus.Draft && Status != DeliveryTripStatus.Scheduled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "Cannot modify stops on a trip that is in transit, completed or cancelled.");
        }

        var stop = new DeliveryStop(Guid.NewGuid(), Id, address, customerId, customerName, deliveryNoteId, deliveryNoteNumber, grandTotal)
        {
            EstimatedArrival = estimatedArrival,
            Distance = distance,
            Uom = uom,
            Latitude = lat,
            Longitude = lng,
            Details = details,
        };

        DeliveryStops.Add(stop);
        RecalculateTotalDistance();
        return stop;
    }

    public void RemoveStop(Guid stopId)
    {
        if (Status != DeliveryTripStatus.Draft && Status != DeliveryTripStatus.Scheduled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "Cannot remove stops on a trip that is in transit, completed or cancelled.");
        }

        var stop = DeliveryStops.FirstOrDefault(s => s.Id == stopId);
        if (stop != null)
        {
            DeliveryStops.Remove(stop);
            RecalculateTotalDistance();
        }
    }

    public void Schedule()
    {
        if (Status != DeliveryTripStatus.Draft)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "Only draft trips can be scheduled.");
        }

        if (!DeliveryStops.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "Delivery trip must have at least one stop before scheduling.");
        }

        Status = DeliveryTripStatus.Scheduled;
    }

    public void StartTransit()
    {
        if (Status != DeliveryTripStatus.Scheduled && Status != DeliveryTripStatus.Draft)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "Trip must be scheduled or draft to start transit.");
        }

        Status = DeliveryTripStatus.InTransit;
    }

    public void Complete()
    {
        if (Status != DeliveryTripStatus.InTransit && Status != DeliveryTripStatus.Scheduled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "Only in-transit or scheduled trips can be completed.");
        }

        Status = DeliveryTripStatus.Completed;
    }

    public void Cancel()
    {
        if (Status == DeliveryTripStatus.Completed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "Completed trips cannot be cancelled.");
        }

        Status = DeliveryTripStatus.Cancelled;
    }

    public void RecalculateTotalDistance()
    {
        TotalDistance = DeliveryStops.Sum(s => s.Distance);
    }
}
