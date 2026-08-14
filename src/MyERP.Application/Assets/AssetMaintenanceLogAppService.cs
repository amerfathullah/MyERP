using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Assets.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.AssetMaintenanceLogs.Default)]
public class AssetMaintenanceLogAppService :
    CrudAppService<
        AssetMaintenanceLog,
        AssetMaintenanceLogDto,
        Guid,
        GetAssetMaintenanceLogListDto,
        CreateUpdateAssetMaintenanceLogDto>,
    IAssetMaintenanceLogAppService
{
    private readonly IRepository<AssetMaintenance, Guid> _maintenanceRepository;

    public AssetMaintenanceLogAppService(
        IRepository<AssetMaintenanceLog, Guid> repository,
        IRepository<AssetMaintenance, Guid> maintenanceRepository)
        : base(repository)
    {
        _maintenanceRepository = maintenanceRepository;
        GetPolicyName = MyERPPermissions.AssetMaintenanceLogs.Default;
        GetListPolicyName = MyERPPermissions.AssetMaintenanceLogs.Default;
        CreatePolicyName = MyERPPermissions.AssetMaintenanceLogs.Create;
        UpdatePolicyName = MyERPPermissions.AssetMaintenanceLogs.Edit;
        DeletePolicyName = MyERPPermissions.AssetMaintenanceLogs.Delete;
    }

    protected override async Task<IQueryable<AssetMaintenanceLog>> CreateFilteredQueryAsync(GetAssetMaintenanceLogListDto input)
    {
        var query = await base.CreateFilteredQueryAsync(input);
        if (input.CompanyId.HasValue)
        {
            query = query.Where(l => l.CompanyId == input.CompanyId.Value);
        }
        if (input.AssetId.HasValue)
        {
            query = query.Where(l => l.AssetId == input.AssetId.Value);
        }
        if (input.AssetMaintenanceId.HasValue)
        {
            query = query.Where(l => l.AssetMaintenanceId == input.AssetMaintenanceId.Value);
        }
        if (input.Status.HasValue)
        {
            query = query.Where(l => l.Status == input.Status.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            query = query.Where(l => l.MaintenanceTask.Contains(input.Filter) ||
                                     (l.AssetName != null && l.AssetName.Contains(input.Filter)) ||
                                     (l.AssignTo != null && l.AssignTo.Contains(input.Filter)));
        }
        return query;
    }

    [Authorize(MyERPPermissions.AssetMaintenanceLogs.Create)]
    public override async Task<AssetMaintenanceLogDto> CreateAsync(CreateUpdateAssetMaintenanceLogDto input)
    {
        var entity = new AssetMaintenanceLog(
            GuidGenerator.Create(),
            input.CompanyId,
            input.AssetMaintenanceId,
            input.AssetMaintenanceTaskId,
            input.AssetId,
            input.MaintenanceTask,
            input.DueDate,
            input.Periodicity)
        {
            AssetName = input.AssetName,
            ItemId = input.ItemId,
            ItemCode = input.ItemCode,
            ItemName = input.ItemName,
            MaintenanceType = input.MaintenanceType,
            AssignToEmployeeId = input.AssignToEmployeeId,
            AssignTo = input.AssignTo,
            AssignToName = input.AssignToName,
            HasCertificate = input.HasCertificate,
            CertificateDetails = input.CertificateDetails,
            Cost = input.Cost,
            Remarks = input.Remarks,
            Description = input.Description,
            CertificateNo = input.CertificateNo,
        };

        entity.CheckOverdue(DateTime.UtcNow);
        await Repository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<AssetMaintenanceLog, AssetMaintenanceLogDto>(entity);
    }

    [Authorize(MyERPPermissions.AssetMaintenanceLogs.Edit)]
    public override async Task<AssetMaintenanceLogDto> UpdateAsync(Guid id, CreateUpdateAssetMaintenanceLogDto input)
    {
        var entity = await Repository.GetAsync(id);

        entity.DueDate = input.DueDate;
        entity.Periodicity = input.Periodicity;
        entity.MaintenanceType = input.MaintenanceType;
        entity.AssignToEmployeeId = input.AssignToEmployeeId;
        entity.AssignTo = input.AssignTo;
        entity.AssignToName = input.AssignToName;
        entity.HasCertificate = input.HasCertificate;
        entity.CertificateDetails = input.CertificateDetails;
        entity.Cost = input.Cost;
        entity.Remarks = input.Remarks;
        entity.Description = input.Description;
        entity.CertificateNo = input.CertificateNo;

        entity.CheckOverdue(DateTime.UtcNow);
        await Repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<AssetMaintenanceLog, AssetMaintenanceLogDto>(entity);
    }

    [Authorize(MyERPPermissions.AssetMaintenanceLogs.Complete)]
    public async Task<AssetMaintenanceLogDto> CompleteAsync(Guid id, CompleteAssetMaintenanceLogDto input)
    {
        var log = await Repository.GetAsync(id);
        log.Complete(
            input.CompletionDate,
            input.ActionsPerformed,
            input.Cost,
            input.HasCertificate,
            input.CertificateDetails,
            input.Remarks);

        if (!string.IsNullOrWhiteSpace(input.CertificateNo))
        {
            log.CertificateNo = input.CertificateNo;
        }

        // Update parent maintenance task
        var maintenanceQuery = await _maintenanceRepository.WithDetailsAsync(m => m.Tasks);
        var maintenance = await AsyncExecuter.FirstOrDefaultAsync(maintenanceQuery.Where(m => m.Id == log.AssetMaintenanceId));
        if (maintenance != null)
        {
            var task = maintenance.Tasks.FirstOrDefault(t => t.Id == log.AssetMaintenanceTaskId);
            if (task != null)
            {
                task.UpdateOnCompletion(input.CompletionDate);
                await _maintenanceRepository.UpdateAsync(maintenance, autoSave: true);

                // Auto-create next planned maintenance log if not Random
                if (task.Periodicity != MaintenancePeriodicity.Random)
                {
                    var nextLog = new AssetMaintenanceLog(
                        GuidGenerator.Create(),
                        log.CompanyId,
                        maintenance.Id,
                        task.Id,
                        log.AssetId,
                        task.MaintenanceTask,
                        task.NextDueDate,
                        task.Periodicity)
                    {
                        AssetName = log.AssetName,
                        ItemId = log.ItemId,
                        ItemCode = log.ItemCode,
                        ItemName = log.ItemName,
                        MaintenanceType = task.MaintenanceType,
                        AssignToEmployeeId = task.AssignToEmployeeId,
                        AssignTo = task.AssignTo,
                        AssignToName = task.AssignToName,
                        HasCertificate = task.CertificateRequired,
                        Description = task.Description,
                        CertificateNo = task.CertificateNo,
                    };
                    await Repository.InsertAsync(nextLog, autoSave: false);
                }
            }
        }

        await Repository.UpdateAsync(log, autoSave: true);
        return ObjectMapper.Map<AssetMaintenanceLog, AssetMaintenanceLogDto>(log);
    }

    [Authorize(MyERPPermissions.AssetMaintenanceLogs.Cancel)]
    public async Task<AssetMaintenanceLogDto> CancelAsync(Guid id)
    {
        var entity = await Repository.GetAsync(id);
        entity.Cancel();
        await Repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<AssetMaintenanceLog, AssetMaintenanceLogDto>(entity);
    }

    public async Task<List<AssetMaintenanceLogDto>> GetLogsByAssetAsync(Guid assetId)
    {
        await CheckGetPolicyAsync();
        var logs = await Repository.GetListAsync(l => l.AssetId == assetId);
        return logs.Select(ObjectMapper.Map<AssetMaintenanceLog, AssetMaintenanceLogDto>).ToList();
    }
}
