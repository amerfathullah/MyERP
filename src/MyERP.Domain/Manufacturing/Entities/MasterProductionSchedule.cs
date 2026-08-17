using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Master Production Schedule — stages demand from open Sales Orders and Material Requests
/// (optionally narrowed to a selected subset), then aggregates it per item/delivery-date into
/// a lead-time-adjusted order release schedule. Maps to ERPNext
/// manufacturing/doctype/master_production_schedule. Complements the already-migrated
/// Production Plan, which handles BOM explosion and Work Order / Material Request generation;
/// MPS is the upstream demand-aggregation and release-date planning step.
/// </summary>
public class MasterProductionSchedule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string ScheduleNumber { get; set; } = null!;
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public DateTime PostingDate { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime? ToDate { get; private set; }

    /// <summary>When set, demand is aggregated across all child warehouses under this one.</summary>
    public Guid? ParentWarehouseId { get; set; }

    private readonly List<MpsSalesOrderRef> _salesOrders = new();
    public IReadOnlyList<MpsSalesOrderRef> SalesOrders => _salesOrders.AsReadOnly();

    private readonly List<MpsMaterialRequestRef> _materialRequests = new();
    public IReadOnlyList<MpsMaterialRequestRef> MaterialRequests => _materialRequests.AsReadOnly();

    private readonly List<MasterProductionScheduleItem> _items = new();
    public IReadOnlyList<MasterProductionScheduleItem> Items => _items.AsReadOnly();

    protected MasterProductionSchedule() { }

    public MasterProductionSchedule(Guid id, Guid companyId, string scheduleNumber, DateTime postingDate, DateTime fromDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ScheduleNumber = Check.NotNullOrWhiteSpace(scheduleNumber, nameof(scheduleNumber), 50);
        PostingDate = postingDate;
        FromDate = fromDate;
        TenantId = tenantId;
    }

    public void SetSalesOrders(IEnumerable<MpsSalesOrderRef> refs)
    {
        _salesOrders.Clear();
        _salesOrders.AddRange(refs);
    }

    public void SetMaterialRequests(IEnumerable<MpsMaterialRequestRef> refs)
    {
        _materialRequests.Clear();
        _materialRequests.AddRange(refs);
    }

    /// <summary>Replaces the aggregated Items table and recomputes ToDate, per ERPNext set_to_date().</summary>
    public void SetItems(IEnumerable<MasterProductionScheduleItem> items)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _items.Clear();
        _items.AddRange(items);

        ToDate = _items.Count > 0 ? _items.Max(i => i.DeliveryDate) : null;
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (_items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.MasterProductionScheduleHasNoItems);
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }
}

/// <summary>Staged reference to an open Sales Order pulled in for demand aggregation.</summary>
public class MpsSalesOrderRef : FullAuditedEntity<Guid>
{
    public Guid MasterProductionScheduleId { get; set; }
    public Guid SalesOrderId { get; set; }
    public DateTime SalesOrderDate { get; set; }
    public Guid CustomerId { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Status { get; set; }

    protected MpsSalesOrderRef() { }

    public MpsSalesOrderRef(Guid id, Guid masterProductionScheduleId, Guid salesOrderId, DateTime salesOrderDate,
        Guid customerId, decimal grandTotal, string? status) : base(id)
    {
        MasterProductionScheduleId = masterProductionScheduleId;
        SalesOrderId = salesOrderId;
        SalesOrderDate = salesOrderDate;
        CustomerId = customerId;
        GrandTotal = grandTotal;
        Status = status;
    }
}

/// <summary>Staged reference to an open Material Request pulled in for demand aggregation.</summary>
public class MpsMaterialRequestRef : FullAuditedEntity<Guid>
{
    public Guid MasterProductionScheduleId { get; set; }
    public Guid MaterialRequestId { get; set; }
    public DateTime MaterialRequestDate { get; set; }

    protected MpsMaterialRequestRef() { }

    public MpsMaterialRequestRef(Guid id, Guid masterProductionScheduleId, Guid materialRequestId, DateTime materialRequestDate)
        : base(id)
    {
        MasterProductionScheduleId = masterProductionScheduleId;
        MaterialRequestId = materialRequestId;
        MaterialRequestDate = materialRequestDate;
    }
}

/// <summary>
/// Aggregated demand for a single (item, delivery date) pair, with a lead-time-backed
/// order release date. Per ERPNext get_item_wise_mps_data / add_mps_data.
/// </summary>
public class MasterProductionScheduleItem : FullAuditedEntity<Guid>
{
    public Guid MasterProductionScheduleId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public Guid? BomId { get; set; }
    public string Uom { get; set; } = "Unit";
    public Guid? WarehouseId { get; set; }

    public DateTime DeliveryDate { get; set; }
    public decimal PlannedQty { get; set; }

    /// <summary>Cumulative manufacturing/purchase lead time in days, summed recursively down the BOM tree.</summary>
    public int CumulativeLeadTimeDays { get; set; }

    /// <summary>DeliveryDate minus CumulativeLeadTimeDays — when the order/production must be released.</summary>
    public DateTime OrderReleaseDate { get; set; }

    protected MasterProductionScheduleItem() { }

    public MasterProductionScheduleItem(Guid id, Guid masterProductionScheduleId, Guid itemId, string itemName,
        DateTime deliveryDate, decimal plannedQty, int cumulativeLeadTimeDays) : base(id)
    {
        MasterProductionScheduleId = masterProductionScheduleId;
        ItemId = itemId;
        ItemName = itemName;
        DeliveryDate = deliveryDate;
        PlannedQty = plannedQty;
        CumulativeLeadTimeDays = cumulativeLeadTimeDays;
        OrderReleaseDate = deliveryDate.AddDays(-cumulativeLeadTimeDays);
    }
}
