using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

public class DeliveryStop : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid DeliveryTripId { get; internal set; }

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

    protected DeliveryStop() { }

    public DeliveryStop(
        Guid id,
        Guid deliveryTripId,
        string address,
        Guid? customerId = null,
        string? customerName = null,
        Guid? deliveryNoteId = null,
        string? deliveryNoteNumber = null,
        decimal grandTotal = 0)
        : base(id)
    {
        DeliveryTripId = deliveryTripId;
        Address = address;
        CustomerId = customerId;
        CustomerName = customerName;
        DeliveryNoteId = deliveryNoteId;
        DeliveryNoteNumber = deliveryNoteNumber;
        GrandTotal = grandTotal;
    }
}
