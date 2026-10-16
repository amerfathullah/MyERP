using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Maintenance.Entities;
using MyERP.Maintenance;
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
    private readonly IRepository<Asset, Guid> _assetRepository;

    public AssetMaintenanceLogAppService(
        IRepository<AssetMaintenanceLog, Guid> repository,
        IRepository<AssetMaintenance, Guid> maintenanceRepository,
        IRepository<Asset, Guid> assetRepository)
        : base(repository)
    {
        _maintenanceRepository = maintenanceRepository;
        _assetRepository = assetRepository;
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

                // Auto-create next planned maintenance log if not Random and within EndDate
                if (task.Periodicity != MaintenancePeriodicity.Random && task.NextDueDate.HasValue)
                {
                    var nextLog = new AssetMaintenanceLog(
                        GuidGenerator.Create(),
                        log.CompanyId,
                        maintenance.Id,
                        task.Id,
                        log.AssetId,
                        task.MaintenanceTask,
                        task.NextDueDate.Value,
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
                    nextLog.CheckOverdue(DateTime.UtcNow.Date);
                    await Repository.InsertAsync(nextLog, autoSave: false);
                }
            }
        }

        await Repository.UpdateAsync(log, autoSave: true);

        // Update asset status: exit InMaintenance if no open due logs remain - ERPNext PR #59998 / commit 730bb168b7
        var hasDueLogs = await Repository.AnyAsync(l =>
            l.AssetId == log.AssetId &&
            l.Id != log.Id &&
            (l.Status == AssetMaintenanceStatus.Planned || l.Status == AssetMaintenanceStatus.Overdue) &&
            l.DueDate.Date <= DateTime.UtcNow.Date);
        if (!hasDueLogs)
        {
            var asset = await _assetRepository.FindAsync(log.AssetId);
            if (asset != null && asset.Status == AssetStatus.InMaintenance)
            {
                asset.ResetMaintenanceStatus();
                await _assetRepository.UpdateAsync(asset);
            }
        }

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
