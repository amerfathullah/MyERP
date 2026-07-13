using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Serial No — individual serial number for stock tracking.
/// Created via stock transactions (Purchase Receipt, Stock Entry, Manufacture).
/// Cannot be created independently — must come via SLE.
/// </summary>
public class SerialNo : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Unique serial number string.</summary>
    public string SerialNumber { get; set; } = null!;

    public Guid ItemId { get; set; }

    /// <summary>Current warehouse (null = delivered/consumed).</summary>
    public Guid? WarehouseId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>Batch this serial belongs to (if batch-tracked).</summary>
    public Guid? BatchId { get; set; }

    /// <summary>Document that created this serial (Purchase Receipt, Stock Entry, etc.).</summary>
    public string? PurchaseDocumentType { get; set; }
    public Guid? PurchaseDocumentId { get; set; }

    /// <summary>Delivery document that dispatched this serial.</summary>
    public string? DeliveryDocumentType { get; set; }
    public Guid? DeliveryDocumentId { get; set; }

    /// <summary>Customer who currently owns this serial (if sold).</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Supplier who provided this serial.</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>Purchase rate at time of acquisition.</summary>
    public decimal PurchaseRate { get; set; }

    /// <summary>Warranty expiry date.</summary>
    public DateTime? WarrantyExpiryDate { get; set; }

    /// <summary>AMC (Annual Maintenance Contract) expiry date.</summary>
    public DateTime? AmcExpiryDate { get; set; }

    /// <summary>
    /// Maintenance status priority: Warranty > AMC > Out of Warranty > Out of AMC.
    /// </summary>
    public string MaintenanceStatus { get; set; } = "Out of Warranty";

    /// <summary>Status: Active, Delivered, Consumed, Inactive.</summary>
    public SerialNoStatus Status { get; set; } = SerialNoStatus.Active;

    protected SerialNo() { }

    public SerialNo(Guid id, Guid itemId, string serialNumber, Guid companyId, Guid? warehouseId = null, Guid? tenantId = null)
        : base(id)
    {
        ItemId = itemId;
        SerialNumber = Check.NotNullOrWhiteSpace(serialNumber, nameof(serialNumber), 100);
        CompanyId = companyId;
        WarehouseId = warehouseId;
        TenantId = tenantId;
    }

    /// <summary>Update maintenance status based on current date.</summary>
    public void UpdateMaintenanceStatus(DateTime? asOfDate = null)
    {
        var now = asOfDate ?? DateTime.UtcNow;
        if (WarrantyExpiryDate.HasValue && now <= WarrantyExpiryDate.Value)
            MaintenanceStatus = "Under Warranty";
        else if (AmcExpiryDate.HasValue && now <= AmcExpiryDate.Value)
            MaintenanceStatus = "Under AMC";
        else if (WarrantyExpiryDate.HasValue)
            MaintenanceStatus = "Out of Warranty";
        else
            MaintenanceStatus = "Out of AMC";
    }
}

public enum SerialNoStatus
{
    Active = 0,
    Delivered = 1,
    Consumed = 2,
    Inactive = 3,
}
