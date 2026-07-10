using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Work Order — production execution referencing a BOM.
/// </summary>
public class WorkOrder : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string WorkOrderNumber { get; set; } = null!;
    public WorkOrderStatus Status { get; private set; }

    public Guid ItemId { get; set; }
    public Guid BomId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ProducedQuantity { get; set; }
    public decimal MaterialTransferred { get; set; }

    public Guid CompanyId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public Guid? WipWarehouseId { get; set; }
    public Guid? FgWarehouseId { get; set; }

    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    public string? Notes { get; set; }

    public List<WorkOrderItem> RequiredItems { get; set; } = new();

    protected WorkOrder() { }

    public WorkOrder(Guid id, Guid companyId, string workOrderNumber, Guid itemId, Guid bomId, decimal quantity, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        WorkOrderNumber = workOrderNumber;
        ItemId = itemId;
        BomId = bomId;
        Quantity = quantity;
        Status = WorkOrderStatus.Draft;
        TenantId = tenantId;
    }

    public void SetPlannedDates(DateTime? startDate, DateTime? endDate)
    {
        if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
            throw new BusinessException(MyERPDomainErrorCodes.PlannedEndDateBeforeStartDate);
        PlannedStartDate = startDate;
        PlannedEndDate = endDate;
    }

    public void ValidateDates()
    {
        if (PlannedStartDate.HasValue && PlannedEndDate.HasValue && PlannedEndDate.Value < PlannedStartDate.Value)
            throw new BusinessException(MyERPDomainErrorCodes.PlannedEndDateBeforeStartDate);
        if (ActualStartDate.HasValue && ActualEndDate.HasValue && ActualEndDate.Value < ActualStartDate.Value)
            throw new BusinessException(MyERPDomainErrorCodes.ActualEndDateBeforeStartDate);
    }

    public void Submit()
    {
        if (Status != WorkOrderStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        ValidateDates();
        Status = WorkOrderStatus.Submitted;
    }

    public void Start()
    {
        if (Status is not (WorkOrderStatus.Submitted or WorkOrderStatus.NotStarted))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = WorkOrderStatus.InProcess;
        ActualStartDate ??= DateTime.UtcNow;
    }

    public void RecordProduction(decimal quantity)
    {
        if (Status != WorkOrderStatus.InProcess)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        ProducedQuantity += quantity;
        if (ProducedQuantity >= Quantity)
        {
            Status = WorkOrderStatus.Completed;
            ActualEndDate = DateTime.UtcNow;
        }
    }

    public void RecordMaterialTransfer(decimal quantity)
    {
        MaterialTransferred += quantity;
        if (Status == WorkOrderStatus.Submitted)
            Status = WorkOrderStatus.NotStarted;
    }

    public void Stop()
    {
        if (Status != WorkOrderStatus.InProcess)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = WorkOrderStatus.Stopped;
    }

    public void Cancel()
    {
        if (Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = WorkOrderStatus.Cancelled;
    }

    public decimal PercentComplete => Quantity > 0 ? Math.Round(ProducedQuantity / Quantity * 100, 1) : 0;
}
