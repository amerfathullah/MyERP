using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Maintenance.Entities;
using MyERP.Maintenance;
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

    public AssetMaintenanceAppService(
        IRepository<AssetMaintenance, Guid> repository,
        IRepository<AssetMaintenanceLog, Guid> logRepository)
        : base(repository)
    {
        _logRepository = logRepository;
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

                // Auto-generate initial planned maintenance log
                var initialLog = new AssetMaintenanceLog(
                    GuidGenerator.Create(),
                    input.CompanyId,
                    entity.Id,
                    task.Id,
                    input.AssetId,
                    task.MaintenanceTask,
                    task.NextDueDate,
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
                await _logRepository.InsertAsync(initialLog, autoSave: false);
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

        // Cancel orphaned logs for removed tasks
        foreach (var taskId in existingTaskIds)
        {
            if (!incomingTaskIds.Contains(taskId))
            {
                var orphanedLogs = await _logRepository.GetListAsync(l => l.AssetMaintenanceTaskId == taskId && l.Status == AssetMaintenanceStatus.Planned);
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
                    task.AssignToEmployeeId = taskDto.AssignToEmployeeId;
                    task.AssignTo = taskDto.AssignTo;
                    task.AssignToName = taskDto.AssignToName;
                    task.CertificateRequired = taskDto.CertificateRequired;
                    task.Description = taskDto.Description;
                    task.CertificateNo = taskDto.CertificateNo;
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

                var initialLog = new AssetMaintenanceLog(
                    GuidGenerator.Create(),
                    input.CompanyId,
                    entity.Id,
                    newTask.Id,
                    input.AssetId,
                    newTask.MaintenanceTask,
                    newTask.NextDueDate,
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
                await _logRepository.InsertAsync(initialLog, autoSave: false);
            }
        }

        await Repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<AssetMaintenance, AssetMaintenanceDto>(entity);
    }
}
