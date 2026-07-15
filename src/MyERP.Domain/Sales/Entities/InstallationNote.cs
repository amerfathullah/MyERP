using System;
using System.Collections.Generic;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Installation Note — records installation of delivered items at customer site.
/// Links to Delivery Note; tracks installed_qty per DN item.
/// Maps to ERPNext selling/doctype/installation_note.
/// Per DO-NOT: "Allow Installation Note qty to exceed Delivery Note qty"
/// </summary>
public class InstallationNote : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string InstallationNumber { get; set; } = null!;
    public DateTime InstallationDate { get; set; }
    public Guid CustomerId { get; set; }
    public Guid DeliveryNoteId { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public string? Remarks { get; set; }

    private readonly List<InstallationNoteItem> _items = new();
    public IReadOnlyList<InstallationNoteItem> Items => _items.AsReadOnly();

    protected InstallationNote() { }

    public InstallationNote(Guid id, Guid companyId, string installationNumber,
        Guid customerId, Guid deliveryNoteId, DateTime installationDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        InstallationNumber = installationNumber;
        CustomerId = customerId;
        DeliveryNoteId = deliveryNoteId;
        InstallationDate = installationDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, decimal qty, string? serialNo = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (qty <= 0) throw new ArgumentException("Quantity must be positive.");

        _items.Add(new InstallationNoteItem
        {
            ItemId = itemId,
            Qty = qty,
            SerialNo = serialNo
        });
    }

    /// <summary>
    /// Validates installation date is not before the linked DN posting_date.
    /// Per ERPNext: installation_date >= DN.posting_date (cannot install before delivery).
    /// </summary>
    public void ValidateInstallationDate(DateTime dnPostingDate)
    {
        if (InstallationDate < dnPostingDate)
        {
            throw new BusinessException("MyERP:03016")
                .WithData("installationDate", InstallationDate)
                .WithData("deliveryDate", dnPostingDate);
        }
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }
}

public class InstallationNoteItem : Volo.Abp.Domain.Entities.Entity<Guid>
{
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public string? SerialNo { get; set; }
}
