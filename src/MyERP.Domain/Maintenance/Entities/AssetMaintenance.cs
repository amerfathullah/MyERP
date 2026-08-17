using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Maintenance.Entities;

public class AssetMaintenance : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; protected set; }

    public Guid AssetId { get; protected set; }
    public string? AssetName { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }

    public Guid? MaintenanceManagerId { get; set; }
    public string? MaintenanceManagerName { get; set; }

    public Guid? MaintenanceTeamId { get; set; }
    public string? MaintenanceTeamName { get; set; }

    public virtual ICollection<AssetMaintenanceTask> Tasks { get; protected set; }

    protected AssetMaintenance()
    {
        Tasks = new Collection<AssetMaintenanceTask>();
    }

    public AssetMaintenance(
        Guid id,
        Guid companyId,
        Guid assetId)
        : base(id)
    {
        CompanyId = companyId;
        AssetId = assetId;
        Tasks = new Collection<AssetMaintenanceTask>();
    }

    public AssetMaintenanceTask AddTask(
        string maintenanceTask,
        MaintenancePeriodicity periodicity,
        DateTime startDate,
        DateTime? nextDueDate = null,
        DateTime? endDate = null,
        string? maintenanceType = null,
        Guid? assignToEmployeeId = null,
        string? assignTo = null,
        string? assignToName = null,
        bool certificateRequired = false,
        string? description = null,
        string? certificateNo = null)
    {
        var task = new AssetMaintenanceTask(
            Guid.NewGuid(),
            Id,
            maintenanceTask,
            periodicity,
            startDate,
            nextDueDate,
            endDate)
        {
            TenantId = TenantId,
            MaintenanceType = maintenanceType,
            AssignToEmployeeId = assignToEmployeeId,
            AssignTo = assignTo,
            AssignToName = assignToName,
            CertificateRequired = certificateRequired,
            Description = description,
            CertificateNo = certificateNo,
        };

        Tasks.Add(task);
        return task;
    }

    public void RemoveTask(Guid taskId)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            Tasks.Remove(task);
        }
    }
}
