using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Shipment — logistics tracking for outbound deliveries.
/// Per ERPNext: tracks physical shipment of goods from warehouse to customer,
/// including carrier details, tracking, weight, and value declarations.
/// </summary>
public class Shipment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string ShipmentNumber { get; set; } = null!;

    /// <summary>Pickup details.</summary>
    public string? PickupFromType { get; set; } // Company, Supplier, Customer
    public Guid? PickupFromId { get; set; }
    public string? PickupFromName { get; set; }
    public Guid? PickupAddressId { get; set; }
    public string? PickupContactName { get; set; }
    public string? PickupContactPhone { get; set; }

    /// <summary>Delivery details.</summary>
    public string? DeliveryToType { get; set; } // Company, Supplier, Customer
    public Guid? DeliveryToId { get; set; }
    public string? DeliveryToName { get; set; }
    public Guid? DeliveryAddressId { get; set; }
    public string? DeliveryContactName { get; set; }
    public string? DeliveryContactPhone { get; set; }

    /// <summary>Shipment logistics.</summary>
    public DateTime? PickupDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? Carrier { get; set; }
    public string? CarrierService { get; set; }
    public string? TrackingNumber { get; set; }
    public string? TrackingUrl { get; set; }

    /// <summary>Package details.</summary>
    public decimal? TotalNetWeight { get; set; }
    public decimal? TotalGrossWeight { get; set; }
    public string? WeightUom { get; set; }

    /// <summary>Value declaration (for customs/insurance).</summary>
    public decimal? ValueOfGoods { get; set; }
    public string? CurrencyCode { get; set; }

    public ShipmentStatus Status { get; private set; } = ShipmentStatus.Draft;
    public string? Notes { get; set; }

    /// <summary>Linked Delivery Notes for this shipment.</summary>
    private readonly List<ShipmentDeliveryNote> _deliveryNotes = new();
    public IReadOnlyList<ShipmentDeliveryNote> DeliveryNotes => _deliveryNotes.AsReadOnly();

    protected Shipment() { }

    public Shipment(Guid id, Guid companyId, string shipmentNumber, Guid? tenantId = null)
        : base(id)
    {
        Check.NotNullOrWhiteSpace(shipmentNumber, nameof(shipmentNumber));
        CompanyId = companyId;
        ShipmentNumber = shipmentNumber;
        TenantId = tenantId;
    }

    public void AddDeliveryNote(Guid linkId, Guid deliveryNoteId, string? deliveryNoteNumber = null,
        decimal? grandTotal = null)
    {
        if (Status == ShipmentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "Shipment")
                .WithData("status", Status.ToString());
        _deliveryNotes.Add(new ShipmentDeliveryNote(linkId, Id, deliveryNoteId,
            deliveryNoteNumber, grandTotal));
    }

    public void Submit()
    {
        if (Status != ShipmentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "Shipment")
                .WithData("status", Status.ToString());
        Status = ShipmentStatus.Booked;
    }

    public void MarkInTransit()
    {
        if (Status != ShipmentStatus.Booked)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "Shipment")
                .WithData("status", Status.ToString());
        Status = ShipmentStatus.InTransit;
    }

    public void MarkDelivered(DateTime deliveryDate)
    {
        if (Status != ShipmentStatus.InTransit && Status != ShipmentStatus.Booked)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "Shipment")
                .WithData("status", Status.ToString());
        DeliveryDate = deliveryDate;
        Status = ShipmentStatus.Delivered;
    }

    public void Cancel()
    {
        if (Status == ShipmentStatus.Cancelled || Status == ShipmentStatus.Delivered)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "Shipment")
                .WithData("status", Status.ToString());
        Status = ShipmentStatus.Cancelled;
    }
}

public enum ShipmentStatus
{
    Draft = 0,
    Booked = 1,
    InTransit = 2,
    Delivered = 3,
    Cancelled = 4
}

/// <summary>Links a Delivery Note to a Shipment.</summary>
public class ShipmentDeliveryNote : FullAuditedEntity<Guid>
{
    public Guid ShipmentId { get; set; }
    public Guid DeliveryNoteId { get; set; }
    public string? DeliveryNoteNumber { get; set; }
    public decimal? GrandTotal { get; set; }

    protected ShipmentDeliveryNote() { }

    public ShipmentDeliveryNote(Guid id, Guid shipmentId, Guid deliveryNoteId,
        string? deliveryNoteNumber, decimal? grandTotal)
        : base(id)
    {
        ShipmentId = shipmentId;
        DeliveryNoteId = deliveryNoteId;
        DeliveryNoteNumber = deliveryNoteNumber;
        GrandTotal = grandTotal;
    }
}
