using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using MyERP.Assets;
using MyERP.Assets.Entities;

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

    /// <summary>
    /// Validates asset state, task date sequences, and team member assignments.
    /// Per ERPNext PR #59998 / commit ea529f57d2 & 9ecd9425fb.
    /// </summary>
    public void Validate(
        Asset asset,
        ICollection<string>? teamMemberNames = null,
        ICollection<Guid>? teamMemberEmployeeIds = null)
    {
        if (asset.Status == AssetStatus.Draft || asset.Status == AssetStatus.Cancelled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AssetMustBeSubmitted)
                .WithData("assetName", AssetName ?? asset.AssetName);
        }

        foreach (var task in Tasks)
        {
            if (task.EndDate.HasValue && task.StartDate.Date >= task.EndDate.Value.Date)
            {
                throw new BusinessException(MyERPDomainErrorCodes.StartDateMustBeLessThanEndDate)
                    .WithData("task", task.MaintenanceTask);
            }

            if (!task.NextDueDate.HasValue && task.Periodicity != MaintenancePeriodicity.Random)
            {
                task.NextDueDate = AssetMaintenanceTask.CalculateNextDueDate(
                    task.Periodicity, task.StartDate, task.EndDate, task.LastCompletionDate);
            }

            if (task.NextDueDate.HasValue && task.NextDueDate.Value.Date < task.StartDate.Date)
            {
                throw new BusinessException(MyERPDomainErrorCodes.NextDueDateCannotBeBeforeStartDate)
                    .WithData("task", task.MaintenanceTask);
            }

            task.SetStatus(DateTime.UtcNow.Date);

            // Assignee validation: skip cancelled tasks
            if (task.MaintenanceStatus != AssetMaintenanceStatus.Cancelled)
            {
                var isInvalid = false;
                if (teamMemberEmployeeIds != null && task.AssignToEmployeeId.HasValue)
                {
                    if (!teamMemberEmployeeIds.Contains(task.AssignToEmployeeId.Value))
                    {
                        isInvalid = true;
                    }
                }
                else if (teamMemberNames != null && !string.IsNullOrWhiteSpace(task.AssignTo))
                {
                    if (!teamMemberNames.Contains(task.AssignTo))
                    {
                        isInvalid = true;
                    }
                }

                if (isInvalid)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.AssigneeNotInMaintenanceTeam)
                        .WithData("assignTo", task.AssignTo ?? task.AssignToEmployeeId?.ToString() ?? string.Empty)
                        .WithData("team", MaintenanceTeamName ?? string.Empty);
                }
            }
        }
    }
}
