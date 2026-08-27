using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class DeliveryStopDto : FullAuditedEntityDto<Guid>
{
    public Guid DeliveryTripId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string Address { get; set; } = null!;
    public string? CustomerAddress { get; set; }
    public bool Locked { get; set; }
    public bool Visited { get; set; }
    public Guid? DeliveryNoteId { get; set; }
    public string? DeliveryNoteNumber { get; set; }
    public decimal GrandTotal { get; set; }
    public string? ContactName { get; set; }
    public string? EmailSentTo { get; set; }
    public string? CustomerContact { get; set; }
    public decimal Distance { get; set; }
    public string? Uom { get; set; }
    public DateTime? EstimatedArrival { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Details { get; set; }
}

public class CreateUpdateDeliveryStopDto
{
    public Guid? Id { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }

    [Required]
    [StringLength(DeliveryTripConsts.MaxAddressLength)]
    public string Address { get; set; } = null!;

    public string? CustomerAddress { get; set; }
    public bool Locked { get; set; }
    public bool Visited { get; set; }
    public Guid? DeliveryNoteId { get; set; }
    public string? DeliveryNoteNumber { get; set; }
    public decimal GrandTotal { get; set; }
    public string? ContactName { get; set; }
    public string? EmailSentTo { get; set; }
    public string? CustomerContact { get; set; }
    public decimal Distance { get; set; }
    public string? Uom { get; set; }
    public DateTime? EstimatedArrival { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    [StringLength(DeliveryTripConsts.MaxDetailsLength)]
    public string? Details { get; set; }
}

public class DeliveryTripDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string? NamingSeries { get; set; }
    public string TripNumber { get; set; } = null!;
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
    public DeliveryTripStatus Status { get; set; }
    public List<DeliveryStopDto> DeliveryStops { get; set; } = new();
}

public class CreateUpdateDeliveryTripDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [StringLength(DeliveryTripConsts.MaxNamingSeriesLength)]
    public string? NamingSeries { get; set; }

    [Required]
    [StringLength(DeliveryTripConsts.MaxTripNumberLength)]
    public string TripNumber { get; set; } = null!;

    [Required]
    [StringLength(DeliveryTripConsts.MaxDriverNameLength)]
    public string Driver { get; set; } = null!;

    public string? DriverName { get; set; }
    public string? DriverEmail { get; set; }
    public string? DriverAddress { get; set; }

    [Required]
    [StringLength(DeliveryTripConsts.MaxVehicleLength)]
    public string Vehicle { get; set; } = null!;

    [Required]
    public DateTime DepartureTime { get; set; }

    public Guid? EmployeeId { get; set; }
    public string? Uom { get; set; }
    public List<CreateUpdateDeliveryStopDto> DeliveryStops { get; set; } = new();
}

public class GetStopsFromDeliveryNotesInput
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> DeliveryNoteIds { get; set; } = new();
}

public class CalculateArrivalTimesInput
{
    public bool OptimizeRoute { get; set; }
    public decimal AverageSpeedKmH { get; set; } = 40m;
}
