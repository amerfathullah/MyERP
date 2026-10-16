using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Maintenance.Entities;
using MyERP.Maintenance;
using MyERP.Assets.Entities;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.AssetMaintenances.Default)]
public class AssetMaintenanceAppService :
    CrudAppService<
        AssetMaintenance,
        AssetMaintenanceDto,
        Guid,
        GetAssetMaintenanceListDto,
        CreateUpdateAssetMaintenanceDto>,
    IAssetMaintenanceAppService
{
    private readonly IRepository<AssetMaintenanceLog, Guid> _logRepository;
    private readonly IRepository<Asset, Guid> _assetRepository;
    private readonly IRepository<AssetMaintenanceTeam, Guid> _teamRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public AssetMaintenanceAppService(
        IRepository<AssetMaintenance, Guid> repository,
        IRepository<AssetMaintenanceLog, Guid> logRepository,
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetMaintenanceTeam, Guid> teamRepository,
        IRepository<Employee, Guid> employeeRepository)
        : base(repository)
    {
        _logRepository = logRepository;
        _assetRepository = assetRepository;
        _teamRepository = teamRepository;
        _employeeRepository = employeeRepository;
        GetPolicyName = MyERPPermissions.AssetMaintenances.Default;
        GetListPolicyName = MyERPPermissions.AssetMaintenances.Default;
        CreatePolicyName = MyERPPermissions.AssetMaintenances.Create;
        UpdatePolicyName = MyERPPermissions.AssetMaintenances.Edit;
        DeletePolicyName = MyERPPermissions.AssetMaintenances.Delete;
    }

    protected override async Task<IQueryable<AssetMaintenance>> CreateFilteredQueryAsync(GetAssetMaintenanceListDto input)
    {
        var query = await Repository.WithDetailsAsync(am => am.Tasks);
        if (input.CompanyId.HasValue)
        {
            query = query.Where(am => am.CompanyId == input.CompanyId.Value);
        }
        if (input.AssetId.HasValue)
        {
            query = query.Where(am => am.AssetId == input.AssetId.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            query = query.Where(am => (am.AssetName != null && am.AssetName.Contains(input.Filter)) ||
                                      (am.ItemCode != null && am.ItemCode.Contains(input.Filter)) ||
                                      (am.ItemName != null && am.ItemName.Contains(input.Filter)));
        }
        return query;
    }

    public override async Task<AssetMaintenanceDto> GetAsync(Guid id)
    {
        await CheckGetPolicyAsync();
        var query = await Repository.WithDetailsAsync(am => am.Tasks);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(am => am.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }
        return ObjectMapper.Map<AssetMaintenance, AssetMaintenanceDto>(entity);
    }

    public async Task<AssetMaintenanceDto> GetByAssetAsync(Guid assetId)
    {
        await CheckGetPolicyAsync();
        var query = await Repository.WithDetailsAsync(am => am.Tasks);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(am => am.AssetId == assetId));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }
        return ObjectMapper.Map<AssetMaintenance, AssetMaintenanceDto>(entity);
    }

    [Authorize(MyERPPermissions.AssetMaintenances.Create)]
    public override async Task<AssetMaintenanceDto> CreateAsync(CreateUpdateAssetMaintenanceDto input)
    {
        var asset = await _assetRepository.GetAsync(input.AssetId);
        var (teamMemberNames, teamMemberEmployeeIds) = await GetTeamMembersAsync(input.MaintenanceTeamId);

        var entity = new AssetMaintenance(
            GuidGenerator.Create(),
            input.CompanyId,
            input.AssetId)
        {
            AssetName = input.AssetName,
            ItemId = input.ItemId,
            ItemCode = input.ItemCode,
            ItemName = input.ItemName,
            MaintenanceManagerId = input.MaintenanceManagerId,
            MaintenanceManagerName = input.MaintenanceManagerName,
            MaintenanceTeamId = input.MaintenanceTeamId,
            MaintenanceTeamName = input.MaintenanceTeamName,
        };

        if (input.Tasks != null)
        {
            foreach (var taskDto in input.Tasks)
            {
                var task = entity.AddTask(
                    taskDto.MaintenanceTask,
                    taskDto.Periodicity,
                    taskDto.StartDate,
                    taskDto.NextDueDate,
                    taskDto.EndDate,
                    taskDto.MaintenanceType,
                    taskDto.AssignToEmployeeId,
                    taskDto.AssignTo,
                    taskDto.AssignToName,
                    taskDto.CertificateRequired,
                    taskDto.Description,
                    taskDto.CertificateNo);
                task.MaintenanceStatus = taskDto.MaintenanceStatus;
            }
        }

        entity.Validate(asset, teamMemberNames, teamMemberEmployeeIds);

        // Auto-generate initial planned maintenance log for active tasks with next due date
        foreach (var task in entity.Tasks.Where(t => t.MaintenanceStatus != AssetMaintenanceStatus.Cancelled && t.NextDueDate.HasValue))
        {
            var initialLog = new AssetMaintenanceLog(
                GuidGenerator.Create(),
                input.CompanyId,
                entity.Id,
                task.Id,
                input.AssetId,
                task.MaintenanceTask,
                task.NextDueDate!.Value,
                task.Periodicity)
            {
                AssetName = input.AssetName,
                ItemId = input.ItemId,
                ItemCode = input.ItemCode,
                ItemName = input.ItemName,
                MaintenanceType = task.MaintenanceType,
                AssignToEmployeeId = task.AssignToEmployeeId,
                AssignTo = task.AssignTo,
                AssignToName = task.AssignToName,
                HasCertificate = task.CertificateRequired,
                Description = task.Description,
                CertificateNo = task.CertificateNo,
            };
            initialLog.CheckOverdue(DateTime.UtcNow.Date);
            await _logRepository.InsertAsync(initialLog, autoSave: false);

            if (initialLog.DueDate.Date <= DateTime.UtcNow.Date &&
                (initialLog.Status == AssetMaintenanceStatus.Planned || initialLog.Status == AssetMaintenanceStatus.Overdue))
            {
                asset.SetInMaintenance();
                await _assetRepository.UpdateAsync(asset, autoSave: false);
            }
        }

        await Repository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<AssetMaintenance, AssetMaintenanceDto>(entity);
    }

    [Authorize(MyERPPermissions.AssetMaintenances.Edit)]
    public override async Task<AssetMaintenanceDto> UpdateAsync(Guid id, CreateUpdateAssetMaintenanceDto input)
    {
        var query = await Repository.WithDetailsAsync(am => am.Tasks);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(am => am.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        var asset = await _assetRepository.GetAsync(entity.AssetId);
        var teamId = input.MaintenanceTeamId ?? entity.MaintenanceTeamId;
        var (teamMemberNames, teamMemberEmployeeIds) = await GetTeamMembersAsync(teamId);

        entity.AssetName = input.AssetName;
        entity.ItemId = input.ItemId;
        entity.ItemCode = input.ItemCode;
        entity.ItemName = input.ItemName;
        entity.MaintenanceManagerId = input.MaintenanceManagerId;
        entity.MaintenanceManagerName = input.MaintenanceManagerName;
        entity.MaintenanceTeamId = input.MaintenanceTeamId;
        entity.MaintenanceTeamName = input.MaintenanceTeamName;

        // Sync tasks
        var existingTaskIds = entity.Tasks.Select(t => t.Id).ToList();
        var incomingTaskIds = input.Tasks.Where(t => t.Id.HasValue).Select(t => t.Id!.Value).ToList();

        // Cancel orphaned logs for removed tasks (Planned or Overdue) - ERPNext PR #59998 / commit 66a7400f45
        foreach (var taskId in existingTaskIds)
        {
            if (!incomingTaskIds.Contains(taskId))
            {
                var orphanedLogs = await _logRepository.GetListAsync(l => l.AssetMaintenanceTaskId == taskId &&
                    (l.Status == AssetMaintenanceStatus.Planned || l.Status == AssetMaintenanceStatus.Overdue));
                foreach (var log in orphanedLogs)
                {
                    log.Cancel();
                    await _logRepository.UpdateAsync(log, autoSave: false);
                }
                entity.RemoveTask(taskId);
            }
        }

        // Update or add tasks
        foreach (var taskDto in input.Tasks)
        {
            if (taskDto.Id.HasValue)
            {
                var task = entity.Tasks.FirstOrDefault(t => t.Id == taskDto.Id.Value);
                if (task != null)
                {
                    task.MaintenanceTask = taskDto.MaintenanceTask;
                    task.Periodicity = taskDto.Periodicity;
                    task.MaintenanceType = taskDto.MaintenanceType;
                    task.StartDate = taskDto.StartDate;
                    task.EndDate = taskDto.EndDate;
                    if (taskDto.NextDueDate.HasValue)
                    {
                        task.NextDueDate = taskDto.NextDueDate.Value;
                    }
                    task.MaintenanceStatus = taskDto.MaintenanceStatus;
                    task.AssignToEmployeeId = taskDto.AssignToEmployeeId;
                    task.AssignTo = taskDto.AssignTo;
                    task.AssignToName = taskDto.AssignToName;
                    task.CertificateRequired = taskDto.CertificateRequired;
                    task.Description = taskDto.Description;
                    task.CertificateNo = taskDto.CertificateNo;

                    // If task cancelled directly: cancel its open draft logs - ERPNext PR #59998 / commit ca12528bde
                    if (task.MaintenanceStatus == AssetMaintenanceStatus.Cancelled)
                    {
                        var openLogs = await _logRepository.GetListAsync(l =>
                            l.AssetMaintenanceTaskId == task.Id &&
                            (l.Status == AssetMaintenanceStatus.Planned || l.Status == AssetMaintenanceStatus.Overdue));
                        foreach (var openLog in openLogs)
                        {
                            openLog.Cancel();
                            await _logRepository.UpdateAsync(openLog, autoSave: false);
                        }
                    }
                    else
                    {
                        // Reset overdue open logs to Planned if next due date moved forward
                        var openLogs = await _logRepository.GetListAsync(l =>
                            l.AssetMaintenanceTaskId == task.Id &&
                            (l.Status == AssetMaintenanceStatus.Planned || l.Status == AssetMaintenanceStatus.Overdue));
                        foreach (var openLog in openLogs)
                        {
                            if (task.NextDueDate.HasValue)
                            {
                                openLog.DueDate = task.NextDueDate.Value;
                                openLog.CheckOverdue(DateTime.UtcNow.Date);
                                await _logRepository.UpdateAsync(openLog, autoSave: false);
                            }
                        }
                    }
                }
            }
            else
            {
                var newTask = entity.AddTask(
                    taskDto.MaintenanceTask,
                    taskDto.Periodicity,
                    taskDto.StartDate,
                    taskDto.NextDueDate,
                    taskDto.EndDate,
                    taskDto.MaintenanceType,
                    taskDto.AssignToEmployeeId,
                    taskDto.AssignTo,
                    taskDto.AssignToName,
                    taskDto.CertificateRequired,
                    taskDto.Description,
                    taskDto.CertificateNo);
                newTask.MaintenanceStatus = taskDto.MaintenanceStatus;

                if (newTask.MaintenanceStatus != AssetMaintenanceStatus.Cancelled && newTask.NextDueDate.HasValue)
                {
                    var initialLog = new AssetMaintenanceLog(
                        GuidGenerator.Create(),
                        input.CompanyId,
                        entity.Id,
                        newTask.Id,
                        input.AssetId,
                        newTask.MaintenanceTask,
                        newTask.NextDueDate.Value,
                        newTask.Periodicity)
                    {
                        AssetName = input.AssetName,
                        ItemId = input.ItemId,
                        ItemCode = input.ItemCode,
                        ItemName = input.ItemName,
                        MaintenanceType = newTask.MaintenanceType,
                        AssignToEmployeeId = newTask.AssignToEmployeeId,
                        AssignTo = newTask.AssignTo,
                        AssignToName = newTask.AssignToName,
                        HasCertificate = newTask.CertificateRequired,
                        Description = newTask.Description,
                        CertificateNo = newTask.CertificateNo,
                    };
                    initialLog.CheckOverdue(DateTime.UtcNow.Date);
                    await _logRepository.InsertAsync(initialLog, autoSave: false);
                }
            }
        }

        entity.Validate(asset, teamMemberNames, teamMemberEmployeeIds);

        // Update asset maintenance status
        var hasDueLogs = await _logRepository.AnyAsync(l =>
            l.AssetId == asset.Id &&
            (l.Status == AssetMaintenanceStatus.Planned || l.Status == AssetMaintenanceStatus.Overdue) &&
            l.DueDate.Date <= DateTime.UtcNow.Date);
        if (hasDueLogs)
        {
            asset.SetInMaintenance();
            await _assetRepository.UpdateAsync(asset, autoSave: false);
        }
        else if (asset.Status == AssetStatus.InMaintenance)
        {
            asset.ResetMaintenanceStatus();
            await _assetRepository.UpdateAsync(asset, autoSave: false);
        }

        await Repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<AssetMaintenance, AssetMaintenanceDto>(entity);
    }

    [Authorize(MyERPPermissions.AssetMaintenances.Delete)]
    public override async Task DeleteAsync(Guid id)
    {
        var maintenance = await Repository.FindAsync(id);
        if (maintenance == null) return;

        // Submitted/completed logs block deletion - ERPNext PR #59998 / commit b3da286caf
        var hasCompletedLogs = await _logRepository.AnyAsync(l =>
            l.AssetMaintenanceId == id && l.Status == AssetMaintenanceStatus.Completed);
        if (hasCompletedLogs)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotDeleteWithCompletedLogs);
        }

        // Delete draft maintenance logs with the asset maintenance
        var draftLogs = await _logRepository.GetListAsync(l => l.AssetMaintenanceId == id);
        foreach (var log in draftLogs)
        {
            await _logRepository.DeleteAsync(log, autoSave: false);
        }

        var assetId = maintenance.AssetId;
        await Repository.DeleteAsync(id, autoSave: true);

        // Reset asset status if it was InMaintenance and has no remaining due logs
        var hasDueLogs = await _logRepository.AnyAsync(l =>
            l.AssetId == assetId &&
            (l.Status == AssetMaintenanceStatus.Planned || l.Status == AssetMaintenanceStatus.Overdue) &&
            l.DueDate.Date <= DateTime.UtcNow.Date);
        if (!hasDueLogs)
        {
            var asset = await _assetRepository.FindAsync(assetId);
            if (asset != null && asset.Status == AssetStatus.InMaintenance)
            {
                asset.ResetMaintenanceStatus();
                await _assetRepository.UpdateAsync(asset);
            }
        }
    }

    private async Task<(HashSet<string>? teamMemberNames, HashSet<Guid>? teamMemberEmployeeIds)> GetTeamMembersAsync(Guid? teamId)
    {
        if (!teamId.HasValue)
        {
            return (null, null);
        }

        var teamQuery = await _teamRepository.WithDetailsAsync(t => t.Members);
        var team = await AsyncExecuter.FirstOrDefaultAsync(teamQuery.Where(t => t.Id == teamId.Value));
        if (team == null || team.Members.Count == 0)
        {
            return (null, null);
        }

        var memberEmployeeIds = team.Members.Select(m => m.EmployeeId).ToHashSet();
        var employees = await _employeeRepository.GetListAsync(e => memberEmployeeIds.Contains(e.Id));
        var memberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var emp in employees)
        {
            if (!string.IsNullOrWhiteSpace(emp.FullName)) memberNames.Add(emp.FullName);
            if (!string.IsNullOrWhiteSpace(emp.Email)) memberNames.Add(emp.Email);
            if (!string.IsNullOrWhiteSpace(emp.EmployeeId)) memberNames.Add(emp.EmployeeId);
        }

        return (memberNames, memberEmployeeIds);
    }
}
