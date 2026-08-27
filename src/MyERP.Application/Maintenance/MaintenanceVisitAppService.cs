using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using MyERP.Maintenance.Entities;
using MyERP.Permissions;

namespace MyERP.Maintenance;

[Authorize(MyERPPermissions.MaintenanceVisits.Default)]
public class MaintenanceVisitAppService : ApplicationService, IMaintenanceVisitAppService
{
    private readonly IRepository<MaintenanceVisit, Guid> _visitRepository;
    private readonly IRepository<WarrantyClaim, Guid> _warrantyClaimRepository;

    public MaintenanceVisitAppService(
        IRepository<MaintenanceVisit, Guid> visitRepository,
        IRepository<WarrantyClaim, Guid> warrantyClaimRepository)
    {
        _visitRepository = visitRepository;
        _warrantyClaimRepository = warrantyClaimRepository;
    }

    public async Task<MaintenanceVisitDto> GetAsync(Guid id)
    {
        var entity = await _visitRepository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<MaintenanceVisitDto>> GetListAsync(GetMaintenanceVisitListDto input)
    {
        var queryable = await _visitRepository.GetQueryableAsync();
        queryable = queryable.WhereIf(input.CustomerId.HasValue,
            v => v.CustomerId == input.CustomerId!.Value);
        queryable = queryable.WhereIf(input.MaintenanceScheduleId.HasValue,
            v => v.MaintenanceScheduleId == input.MaintenanceScheduleId!.Value);
        queryable = queryable.WhereIf(input.MaintenanceType.HasValue,
            v => v.MaintenanceType == (input.MaintenanceType == 0 ? "Scheduled" :
                input.MaintenanceType == 1 ? "Unscheduled" : "Breakdown"));

        var totalCount = queryable.Count();
        var items = queryable
            .OrderByDescending(v => v.VisitDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<MaintenanceVisitDto>(
            totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.MaintenanceVisits.Create)]
    public async Task<MaintenanceVisitDto> CreateAsync(CreateMaintenanceVisitDto input)
    {
        var itemIds = input.Purposes.Where(p => p.ItemId != Guid.Empty).Select(p => p.ItemId).Distinct().ToArray();
        if (itemIds.Length > 0)
        {
            var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
            await itemValidation.ValidateItemsForTransactionAsync(itemIds);
        }

        var typeStr = input.MaintenanceType switch
        {
            0 => "Scheduled",
            1 => "Unscheduled",
            2 => "Breakdown",
            _ => "Scheduled"
        };

        var entity = new MaintenanceVisit(
            GuidGenerator.Create(), input.CompanyId,
            input.VisitDate, typeStr, CurrentTenant.Id)
        {
            CustomerId = input.CustomerId,
            ContactId = input.ContactId,
            MaintenanceScheduleId = input.MaintenanceScheduleId
        };

        foreach (var purposeDto in input.Purposes)
        {
            entity.AddPurpose(new MaintenanceVisitPurpose(
                GuidGenerator.Create(), entity.Id, purposeDto.WorkDone ?? string.Empty)
            {
                ItemId = purposeDto.ItemId,
                SerialNoId = purposeDto.SerialNoId
            });
        }

        await _visitRepository.InsertAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider?.LazyGetService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        if (activityLogRepo != null)
        {
            await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
                GuidGenerator.Create(), "MaintenanceVisit", entity.Id,
                "Created", entity.CompanyId,
                entity.Id.ToString()[..8], "Draft", "Draft", CurrentUser?.Id,
                $"Maintenance Visit {entity.Id.ToString()[..8]} created ({entity.MaintenanceType})", CurrentTenant?.Id));
        }

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaintenanceVisits.Edit)]
    public async Task<MaintenanceVisitDto> UpdateAsync(Guid id, CreateMaintenanceVisitDto input)
    {
        var entity = await _visitRepository.GetAsync(id);
        entity.VisitDate = input.VisitDate;
        entity.ContactId = input.ContactId;
        await _visitRepository.UpdateAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaintenanceVisits.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _visitRepository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.MaintenanceVisits.Submit)]
    public async Task<MaintenanceVisitDto> SubmitAsync(Guid id)
    {
        var entity = await _visitRepository.GetAsync(id);
        entity.Complete();
        await _visitRepository.UpdateAsync(entity, autoSave: true);

        // Cascade resolution to linked Warranty Claim (Gotcha #4171 / #5974)
        if (entity.WarrantyClaimId.HasValue)
        {
            var claim = await _warrantyClaimRepository.FindAsync(entity.WarrantyClaimId.Value);
            if (claim != null && claim.Status != WarrantyClaimStatus.Cancelled)
            {
                var resolutionText = entity.Purposes.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.WorkDone))?.WorkDone
                    ?? $"Completed via Maintenance Visit {entity.Id.ToString()[..8]}";
                claim.Close(resolutionText);
                await _warrantyClaimRepository.UpdateAsync(claim);
            }
        }

        var activityLogRepo = LazyServiceProvider?.LazyGetService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        if (activityLogRepo != null)
        {
            await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
                GuidGenerator.Create(), "MaintenanceVisit", entity.Id,
                "Completed", entity.CompanyId,
                entity.Id.ToString()[..8], "Draft", "Completed", CurrentUser?.Id,
                $"Maintenance Visit {entity.Id.ToString()[..8]} completed on {entity.VisitDate:yyyy-MM-dd}", CurrentTenant?.Id));
        }

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaintenanceVisits.Submit)]
    public async Task<MaintenanceVisitDto> CancelAsync(Guid id)
    {
        var entity = await _visitRepository.GetAsync(id);

        // Guard 1: must cancel newer visits first for the same Maintenance Schedule (Gotcha #2199 / #2998)
        if (entity.MaintenanceScheduleId.HasValue)
        {
            var query = await _visitRepository.GetQueryableAsync();
            var laterVisits = query
                .Where(v => v.MaintenanceScheduleId == entity.MaintenanceScheduleId.Value
                    && v.Id != entity.Id
                    && v.CompletionStatus != MaintenanceVisitStatus.Cancelled
                    && (v.VisitDate > entity.VisitDate || (v.VisitDate == entity.VisitDate && v.CreationTime > entity.CreationTime)))
                .ToList();

            if (laterVisits.Any())
            {
                throw new Volo.Abp.BusinessException("MyERP:14002")
                    .WithData("reason", $"Cannot cancel Maintenance Visit {entity.Id.ToString()[..8]} because later active visit(s) exist for the same schedule. Cancel later visits first.");
            }
        }

        // Guard 2: must cancel newer visits first for the same Warranty Claim (Gotcha #4171 / #5974)
        if (entity.WarrantyClaimId.HasValue)
        {
            var query = await _visitRepository.GetQueryableAsync();
            var laterClaimVisits = query
                .Where(v => v.WarrantyClaimId == entity.WarrantyClaimId.Value
                    && v.Id != entity.Id
                    && v.CompletionStatus != MaintenanceVisitStatus.Cancelled
                    && (v.VisitDate > entity.VisitDate || (v.VisitDate == entity.VisitDate && v.CreationTime > entity.CreationTime)))
                .ToList();

            if (laterClaimVisits.Any())
            {
                throw new Volo.Abp.BusinessException("MyERP:14003")
                    .WithData("reason", $"Cannot cancel Maintenance Visit {entity.Id.ToString()[..8]} because later active visit(s) exist for the same Warranty Claim. Cancel later visits first.");
            }
        }

        entity.Cancel();
        await _visitRepository.UpdateAsync(entity, autoSave: true);

        // Cascade restoration to linked Warranty Claim (Gotcha #4171 / #5974)
        if (entity.WarrantyClaimId.HasValue)
        {
            var claim = await _warrantyClaimRepository.FindAsync(entity.WarrantyClaimId.Value);
            if (claim != null && claim.Status != WarrantyClaimStatus.Cancelled)
            {
                var query = await _visitRepository.GetQueryableAsync();
                var otherActiveVisits = query
                    .Where(v => v.WarrantyClaimId == entity.WarrantyClaimId.Value
                        && v.Id != entity.Id
                        && v.CompletionStatus != MaintenanceVisitStatus.Cancelled)
                    .ToList();

                if (otherActiveVisits.Any())
                {
                    claim.SetWorkInProgress();
                }
                else
                {
                    claim.Reopen();
                }
                await _warrantyClaimRepository.UpdateAsync(claim);
            }
        }

        var activityLogRepo = LazyServiceProvider?.LazyGetService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        if (activityLogRepo != null)
        {
            await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
                GuidGenerator.Create(), "MaintenanceVisit", entity.Id,
                "Cancelled", entity.CompanyId,
                entity.Id.ToString()[..8], entity.CompletionStatus.ToString(), "Cancelled", CurrentUser?.Id,
                $"Maintenance Visit {entity.Id.ToString()[..8]} cancelled", CurrentTenant?.Id));
        }

        return MapToDto(entity);
    }

    private static MaintenanceVisitDto MapToDto(MaintenanceVisit entity) => new()
    {
        Id = entity.Id,
        CompanyId = entity.CompanyId,
        VisitNumber = entity.Id.ToString("N")[..8].ToUpper(),
        CustomerId = entity.CustomerId ?? Guid.Empty,
        MaintenanceType = entity.MaintenanceType switch
        {
            "Scheduled" => 0,
            "Unscheduled" => 1,
            "Breakdown" => 2,
            _ => 0
        },
        VisitDate = entity.VisitDate,
        CompletionStatus = (int)entity.CompletionStatus,
        MaintenanceScheduleId = entity.MaintenanceScheduleId,
        IsSubmitted = entity.CompletionStatus == MaintenanceVisitStatus.Completed,
        IsCancelled = entity.CompletionStatus == MaintenanceVisitStatus.Cancelled,
        Purposes = entity.Purposes.Select(p => new MaintenanceVisitPurposeDto
        {
            Id = p.Id,
            ItemId = p.ItemId ?? Guid.Empty,
            SerialNoId = p.SerialNoId,
            WorkDone = p.WorkDone,
            Status = 0
        }).ToList()
    };
}
