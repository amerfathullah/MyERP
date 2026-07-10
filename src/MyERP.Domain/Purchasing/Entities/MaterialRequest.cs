using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Material Request — request for materials (purchase, transfer, issue, or manufacture).
/// Maps to ERPNext stock/doctype/material_request.
/// </summary>
public class MaterialRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string RequestNumber { get; set; } = null!;
    public MaterialRequestType RequestType { get; set; }
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public DateTime RequestDate { get; set; }
    public DateTime? RequiredByDate { get; set; }

    /// <summary>Source work order (if created from manufacturing).</summary>
    public Guid? WorkOrderId { get; set; }

    public Guid? SourceWarehouseId { get; set; }
    public Guid? TargetWarehouseId { get; set; }

    public string? Notes { get; set; }

    private readonly List<MaterialRequestItem> _items = new();
    public IReadOnlyList<MaterialRequestItem> Items => _items.AsReadOnly();

    protected MaterialRequest() { }

    public MaterialRequest(Guid id, Guid companyId, string requestNumber,
        MaterialRequestType requestType, DateTime requestDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        RequestNumber = Check.NotNullOrWhiteSpace(requestNumber, nameof(requestNumber), MaterialRequestConsts.MaxRequestNumberLength);
        RequestType = requestType;
        RequestDate = requestDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string itemName, decimal quantity, string uom,
        Guid? warehouseId = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _items.Add(new MaterialRequestItem(
            Guid.NewGuid(), Id, itemId, itemName, quantity, uom, warehouseId));
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status is DocumentStatus.Cancelled or DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }
}
