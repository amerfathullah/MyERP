using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Maintenance.Entities;

public class AssetMaintenanceLog : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; protected set; }

    public Guid AssetMaintenanceId { get; protected set; }
    public Guid AssetMaintenanceTaskId { get; protected set; }

    public Guid AssetId { get; protected set; }
    public string? AssetName { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }

    public string MaintenanceTask { get; protected set; } = null!;
    public MaintenancePeriodicity Periodicity { get; set; } = MaintenancePeriodicity.Monthly;
    public string? MaintenanceType { get; set; }

    public DateTime DueDate { get; set; }
    public DateTime? CompletionDate { get; protected set; }

    public AssetMaintenanceStatus Status { get; protected set; } = AssetMaintenanceStatus.Planned;

    public Guid? AssignToEmployeeId { get; set; }
    public string? AssignTo { get; set; }
    public string? AssignToName { get; set; }

    public bool HasCertificate { get; set; }
    public string? CertificateDetails { get; set; }
    public decimal? Cost { get; set; }
    public string? Remarks { get; set; }

    public string? Description { get; set; }
    public string? ActionsPerformed { get; protected set; }
    public string? CertificateNo { get; set; }

    protected AssetMaintenanceLog() { }

    public AssetMaintenanceLog(
        Guid id,
        Guid companyId,
        Guid assetMaintenanceId,
        Guid assetMaintenanceTaskId,
        Guid assetId,
        string maintenanceTask,
        DateTime dueDate,
        MaintenancePeriodicity periodicity = MaintenancePeriodicity.Monthly)
        : base(id)
    {
        CompanyId = companyId;
        AssetMaintenanceId = assetMaintenanceId;
        AssetMaintenanceTaskId = assetMaintenanceTaskId;
        AssetId = assetId;
        MaintenanceTask = Check.NotNullOrWhiteSpace(maintenanceTask, nameof(maintenanceTask), AssetMaintenanceConsts.MaxTaskNameLength);
        DueDate = dueDate;
        Periodicity = periodicity;
        Status = AssetMaintenanceStatus.Planned;
    }

    public void Complete(
        DateTime completionDate,
        string? actionsPerformed = null,
        decimal? cost = null,
        bool hasCertificate = false,
        string? certificateDetails = null,
        string? remarks = null)
    {
        if (Status == AssetMaintenanceStatus.Cancelled)
        {
            throw new BusinessException("MyERP.Assets:CannotCompleteCancelledMaintenanceLog");
        }

        Status = AssetMaintenanceStatus.Completed;
        CompletionDate = completionDate;
        ActionsPerformed = actionsPerformed;
        Cost = cost;
        HasCertificate = hasCertificate;
        CertificateDetails = certificateDetails;
        Remarks = remarks;
    }

    public void Cancel()
    {
        Status = AssetMaintenanceStatus.Cancelled;
    }

    public void CheckOverdue(DateTime currentDate)
    {
        if (Status == AssetMaintenanceStatus.Planned && DueDate < currentDate)
        {
            Status = AssetMaintenanceStatus.Overdue;
        }
    }
}
