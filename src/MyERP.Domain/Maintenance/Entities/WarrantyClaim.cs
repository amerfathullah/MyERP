using System;
using MyERP.Maintenance;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Maintenance.Entities;

/// <summary>
/// Warranty Claim — customer complaint about product quality or defect within warranty period.
/// Per ERPNext: warranty claims are tracked against Serial Numbers and Items.
/// Links to: Customer, Item, SerialNo, SalesInvoice, Address.
/// 
/// Lifecycle: Open → WorkInProgress → Closed / Cancelled
/// When resolved: can trigger Maintenance Visit or Stock Entry (replacement).
/// Cancel blocked when Maintenance Visits exist (must cancel visits first).
/// 
/// Per ERPNext gotcha: resolution_date auto-set on Close, cancel cascades restoration.
/// </summary>
public class WarrantyClaim : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string ClaimNumber { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Guid ItemId { get; set; }

    /// <summary>Serial number of the defective item (if serialized).</summary>
    public Guid? SerialNoId { get; set; }

    /// <summary>Sales Invoice that originally sold the item (for warranty verification).</summary>
    public Guid? SalesInvoiceId { get; set; }

    /// <summary>Warranty expiry date from Item/SerialNo.</summary>
    public DateTime? WarrantyExpiryDate { get; set; }

    /// <summary>AMC (Annual Maintenance Contract) expiry date.</summary>
    public DateTime? AmcExpiryDate { get; set; }

    /// <summary>Date when the complaint was received.</summary>
    public DateTime ComplaintDate { get; set; }

    /// <summary>Description of the complaint/defect.</summary>
    public string? Complaint { get; set; }

    /// <summary>Description of the resolution applied.</summary>
    public string? Resolution { get; set; }

    /// <summary>Date when the issue was resolved.</summary>
    public DateTime? ResolutionDate { get; set; }

    /// <summary>Customer's service address for this claim.</summary>
    public Guid? ServiceAddressId { get; set; }

    public WarrantyClaimStatus Status { get; private set; } = WarrantyClaimStatus.Open;

    protected WarrantyClaim() { }

    public WarrantyClaim(Guid id, Guid companyId, Guid customerId, Guid itemId,
        DateTime complaintDate, Guid? tenantId = null) : base(id)
    {
        Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        Check.NotDefaultOrNull<Guid>(customerId, nameof(customerId));
        Check.NotDefaultOrNull<Guid>(itemId, nameof(itemId));

        CompanyId = companyId;
        CustomerId = customerId;
        ItemId = itemId;
        ComplaintDate = complaintDate;
        TenantId = tenantId;
    }

    /// <summary>Transition to Work In Progress (investigation/repair started).</summary>
    public void StartWork()
    {
        if (Status != WarrantyClaimStatus.Open)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("from", Status.ToString()).WithData("to", "WorkInProgress");
        Status = WarrantyClaimStatus.WorkInProgress;
    }

    /// <summary>Close the warranty claim (resolved).</summary>
    public void Close(string? resolution = null)
    {
        if (Status is WarrantyClaimStatus.Closed or WarrantyClaimStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("from", Status.ToString()).WithData("to", "Closed");
        Status = WarrantyClaimStatus.Closed;
        Resolution = resolution ?? Resolution;
        ResolutionDate = DateTime.UtcNow;
    }

    /// <summary>Cancel the warranty claim.</summary>
    public void Cancel()
    {
        if (Status == WarrantyClaimStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("from", Status.ToString()).WithData("to", "Cancelled");
        Status = WarrantyClaimStatus.Cancelled;
    }

    /// <summary>Check if the claim is within warranty coverage.</summary>
    public bool IsUnderWarranty(DateTime? asOfDate = null)
    {
        var checkDate = asOfDate ?? DateTime.UtcNow;
        if (WarrantyExpiryDate.HasValue && WarrantyExpiryDate.Value >= checkDate)
            return true;
        if (AmcExpiryDate.HasValue && AmcExpiryDate.Value >= checkDate)
            return true;
        return false;
    }
}
