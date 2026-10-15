using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Assets.Entities;
using MyERP.Core;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.AssetMovements.Default)]
public class AssetMovementAppService : ApplicationService, IAssetMovementAppService
{
    private readonly IRepository<AssetMovement, Guid> _repository;
    private readonly IRepository<Asset, Guid> _assetRepository;
    private readonly IRepository<AssetActivity, Guid> _activityRepository;
    private readonly IRepository<Location, Guid> _locationRepository;
    private readonly IRepository<MyERP.HumanResources.Entities.Employee, Guid> _employeeRepository;
    private readonly AssetMovementMapper _mapper;

    public AssetMovementAppService(
        IRepository<AssetMovement, Guid> repository,
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetActivity, Guid> activityRepository,
        IRepository<Location, Guid> locationRepository,
        IRepository<MyERP.HumanResources.Entities.Employee, Guid> employeeRepository,
        AssetMovementMapper mapper)
    {
        _repository = repository;
        _assetRepository = assetRepository;
        _activityRepository = activityRepository;
        _locationRepository = locationRepository;
        _employeeRepository = employeeRepository;
        _mapper = mapper;
    }

    /// <summary>Resolves a LocationId to the Location master's name for the denormalized display field.</summary>
    private async Task<string?> ResolveLocationNameAsync(Guid? locationId, string? fallback)
    {
        if (!locationId.HasValue) return fallback;
        var location = await _locationRepository.FindAsync(locationId.Value);
        return location?.LocationName ?? fallback;
    }

    public async Task<PagedResultDto<AssetMovementDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.WithDetailsAsync(m => m.Items);
        var totalCount = await AsyncExecuter.CountAsync(query);
        var items = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(m => m.TransactionDate)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        return new PagedResultDto<AssetMovementDto>(totalCount, items.Select(_mapper.Map).ToList());
    }

    public async Task<AssetMovementDto> GetAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(m => m.Items);
        var am = await AsyncExecuter.FirstOrDefaultAsync(query, m => m.Id == id);

        if (am == null)
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);

        return _mapper.Map(am);
    }

    [Authorize(MyERPPermissions.AssetMovements.Create)]
    public async Task<AssetMovementDto> CreateAsync(CreateUpdateAssetMovementDto input)
    {
        var movementNumber = $"AS-MOV-{DateTime.UtcNow:yyyyMMdd}-{GuidGenerator.Create().ToString()[..6].ToUpper()}";
        var sourceLocationName = await ResolveLocationNameAsync(input.SourceLocationId, input.SourceLocation);
        var targetLocationName = await ResolveLocationNameAsync(input.TargetLocationId, input.TargetLocation);
        var am = new AssetMovement(
            GuidGenerator.Create(),
            movementNumber,
            input.CompanyId,
            input.Purpose,
            input.TransactionDate,
            CurrentTenant.Id)
        {
            ReferenceType = input.ReferenceType,
            ReferenceId = input.ReferenceId,
            AssetId = input.AssetId,
            SourceLocation = sourceLocationName,
            SourceLocationId = input.SourceLocationId,
            SourceEmployeeId = input.SourceEmployeeId,
            TargetLocation = targetLocationName,
            TargetLocationId = input.TargetLocationId,
            TargetEmployeeId = input.TargetEmployeeId,
        };

        if (input.Items != null && input.Items.Count > 0)
        {
            foreach (var item in input.Items)
            {
                am.AddItem(
                    GuidGenerator.Create(),
                    item.AssetId,
                    item.AssetName,
                    await ResolveLocationNameAsync(item.SourceLocationId, item.SourceLocation),
                    await ResolveLocationNameAsync(item.TargetLocationId, item.TargetLocation),
                    item.FromEmployeeId,
                    item.ToEmployeeId,
                    item.SourceLocationId,
                    item.TargetLocationId);
            }
        }
        else if (input.AssetId.HasValue)
        {
            am.AddItem(
                GuidGenerator.Create(),
                input.AssetId.Value,
                null,
                sourceLocationName,
                targetLocationName,
                input.SourceEmployeeId,
                input.TargetEmployeeId,
                input.SourceLocationId,
                input.TargetLocationId);
        }

        await _repository.InsertAsync(am);
        return _mapper.Map(am);
    }

    [Authorize(MyERPPermissions.AssetMovements.Edit)]
    public async Task<AssetMovementDto> UpdateAsync(Guid id, CreateUpdateAssetMovementDto input)
    {
        var query = await _repository.WithDetailsAsync(m => m.Items);
        var am = await AsyncExecuter.FirstOrDefaultAsync(query, m => m.Id == id);

        if (am == null)
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);

        if (am.Status != Core.DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        am.Purpose = input.Purpose;
        am.TransactionDate = input.TransactionDate;
        am.ReferenceType = input.ReferenceType;
        am.ReferenceId = input.ReferenceId;
        am.AssetId = input.AssetId;
        am.SourceLocation = await ResolveLocationNameAsync(input.SourceLocationId, input.SourceLocation);
        am.SourceLocationId = input.SourceLocationId;
        am.SourceEmployeeId = input.SourceEmployeeId;
        am.TargetLocation = await ResolveLocationNameAsync(input.TargetLocationId, input.TargetLocation);
        am.TargetLocationId = input.TargetLocationId;
        am.TargetEmployeeId = input.TargetEmployeeId;

        am.Items.Clear();
        if (input.Items != null)
        {
            foreach (var item in input.Items)
            {
                am.AddItem(
                    item.Id ?? GuidGenerator.Create(),
                    item.AssetId,
                    item.AssetName,
                    await ResolveLocationNameAsync(item.SourceLocationId, item.SourceLocation),
                    await ResolveLocationNameAsync(item.TargetLocationId, item.TargetLocation),
                    item.FromEmployeeId,
                    item.ToEmployeeId,
                    item.SourceLocationId,
                    item.TargetLocationId);
            }
        }

        await _repository.UpdateAsync(am);
        return _mapper.Map(am);
    }

    [Authorize(MyERPPermissions.AssetMovements.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var am = await _repository.GetAsync(id);
        if (am.Status != Core.DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        await _repository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.AssetMovements.Edit)]
    public async Task<AssetMovementDto> SubmitAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(m => m.Items);
        var am = await AsyncExecuter.FirstOrDefaultAsync(query, m => m.Id == id);

        if (am == null)
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);

        if (!am.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        // Per ERPNext PR #60019 (commit 23adc7295b): Refuse the same asset in more than one row
        var seenAssets = new HashSet<Guid>();
        for (int i = 0; i < am.Items.Count; i++)
        {
            var item = am.Items[i];
            if (!seenAssets.Add(item.AssetId))
            {
                var asset = await _assetRepository.FindAsync(item.AssetId);
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{i + 1}: Asset '{asset?.AssetName ?? item.AssetId.ToString()}' is already added in another row.");
            }
        }

        // Validate each asset item before submit
        foreach (var item in am.Items)
        {
            var asset = await _assetRepository.GetAsync(item.AssetId);

            if (asset.CompanyId != am.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.AssetCompanyMismatch)
                    .WithData("assetName", asset.AssetName);
            }

            // Per ERPNext PR #60019 (commit 6b8511def7): Refuse moving unsubmitted or disposed assets for every purpose
            if (asset.Status is AssetStatus.Draft or AssetStatus.Scrapped or AssetStatus.Sold or AssetStatus.Capitalized)
            {
                throw new BusinessException(MyERPDomainErrorCodes.AssetCannotBeMoved)
                    .WithData("assetName", asset.AssetName)
                    .WithData("status", asset.Status.ToString());
            }

            if (item.SourceLocationId.HasValue && item.TargetLocationId.HasValue &&
                item.SourceLocationId == item.TargetLocationId &&
                item.FromEmployeeId == item.ToEmployeeId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.AssetMovementSameLocationAndCustodian)
                    .WithData("assetName", asset.AssetName);
            }

            // Per ERPNext asset_movement.py validate_location/validate_employee: a claimed source
            // location/custodian that doesn't match the asset's actual current location/custodian
            // would silently corrupt the location/custodian audit trail this document exists to keep.
            if (item.SourceLocationId.HasValue && asset.LocationId.HasValue
                && item.SourceLocationId != asset.LocationId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Asset '{asset.AssetName}' does not belong to the specified source location.");
            }

            if (item.FromEmployeeId.HasValue && asset.CustodianEmployeeId.HasValue
                && item.FromEmployeeId != asset.CustodianEmployeeId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Asset '{asset.AssetName}' does not belong to the specified custodian.");
            }

            // Per ERPNext: the receiving employee must belong to the movement's own company.
            if (item.ToEmployeeId.HasValue)
            {
                var toEmployee = await _employeeRepository.FindAsync(item.ToEmployeeId.Value);
                if (toEmployee != null && toEmployee.CompanyId != am.CompanyId)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Employee does not belong to the company for asset '{asset.AssetName}'.");
                }
            }

            // Validate transaction date against prior submitted movements for this asset (ERPNext PR #52340 / commit e98b68c38f)
            var movementQuery = await _repository.WithDetailsAsync(m => m.Items);
            var priorMovementDates = movementQuery
                .Where(m => m.Id != am.Id && m.Status == Core.DocumentStatus.Submitted && m.Items.Any(i => i.AssetId == item.AssetId))
                .Select(m => m.TransactionDate)
                .ToList();

            if (priorMovementDates.Any(d => d > am.TransactionDate))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Transaction date cannot be earlier than previous movement date for asset '{asset.AssetName}'.");
            }
        }

        am.Submit();

        // Update target assets location and custodian separately (PR #60019 / commits f3d11a89fc, 1073df2a15)
        foreach (var item in am.Items)
        {
            var asset = await _assetRepository.FindAsync(item.AssetId);
            if (asset != null)
            {
                string? changedLocation = null;
                Guid? changedLocationId = null;
                Guid? previousCustodian = null;
                Guid? changedCustodian = null;

                // Location update (Receipt, Transfer, TransferAndIssue, or explicit target location)
                if (am.Purpose is AssetMovementPurpose.Receipt or AssetMovementPurpose.Transfer or AssetMovementPurpose.TransferAndIssue
                    || item.TargetLocationId.HasValue || !string.IsNullOrWhiteSpace(item.TargetLocation))
                {
                    if (item.TargetLocationId != asset.LocationId || item.TargetLocation != asset.Location)
                    {
                        changedLocation = item.TargetLocation;
                        changedLocationId = item.TargetLocationId;
                        asset.Location = changedLocation;
                        asset.LocationId = changedLocationId;
                    }
                }

                // Custodian update (Issue, Receipt, TransferAndIssue, or row with employee)
                if (am.Purpose is AssetMovementPurpose.Issue or AssetMovementPurpose.Receipt or AssetMovementPurpose.TransferAndIssue
                    || item.ToEmployeeId.HasValue)
                {
                    if (item.ToEmployeeId != asset.CustodianEmployeeId)
                    {
                        previousCustodian = asset.CustodianEmployeeId;
                        changedCustodian = item.ToEmployeeId;
                        asset.CustodianEmployeeId = changedCustodian;
                    }
                }

                await _assetRepository.UpdateAsync(asset);

                // Audit Activity based on what actually changed (PR #60019 / commits f3d11a89fc, 1073df2a15)
                string? employeeName = null;
                if (changedCustodian.HasValue)
                {
                    var emp = await _employeeRepository.FindAsync(changedCustodian.Value);
                    employeeName = emp?.FullName ?? changedCustodian.Value.ToString();
                }

                string? prevEmpName = null;
                if (previousCustodian.HasValue)
                {
                    var emp = await _employeeRepository.FindAsync(previousCustodian.Value);
                    prevEmpName = emp?.FullName ?? previousCustodian.Value.ToString();
                }

                string activitySubject;
                if (!string.IsNullOrWhiteSpace(changedLocation) && !string.IsNullOrWhiteSpace(employeeName))
                {
                    activitySubject = $"Asset transferred to location {changedLocation} and issued to Employee {employeeName}";
                }
                else if (!string.IsNullOrWhiteSpace(changedLocation))
                {
                    activitySubject = $"Asset transferred to location {changedLocation}";
                }
                else if (!string.IsNullOrWhiteSpace(employeeName))
                {
                    activitySubject = $"Asset issued to Employee {employeeName}";
                }
                else if (!string.IsNullOrWhiteSpace(prevEmpName))
                {
                    activitySubject = $"Asset returned by Employee {prevEmpName}";
                }
                else
                {
                    activitySubject = $"Asset moved: #{am.MovementNumber}";
                }

                var activity = new AssetActivity(
                    GuidGenerator.Create(),
                    asset.Id,
                    AssetActivityType.Moved,
                    activitySubject,
                    am.TransactionDate,
                    $"Movement #{am.MovementNumber} purpose: {am.Purpose}",
                    "AssetMovement",
                    am.Id.ToString(),
                    CurrentTenant.Id);

                await _activityRepository.InsertAsync(activity);
            }
        }

        await _repository.UpdateAsync(am);
        return _mapper.Map(am);
    }

    [Authorize(MyERPPermissions.AssetMovements.Edit)]
    public async Task<AssetMovementDto> CancelAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(m => m.Items);
        var am = await AsyncExecuter.FirstOrDefaultAsync(query, m => m.Id == id);

        if (am == null)
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);

        // Prevent cancellation of last asset movement if asset is active (ERPNext PR #47291 / commit 9dee4ac891)
        var allMovements = await _repository.WithDetailsAsync(m => m.Items);
        foreach (var item in am.Items)
        {
            var asset = await _assetRepository.FindAsync(item.AssetId);
            if (asset != null && asset.Status == AssetStatus.Submitted)
            {
                var otherSubmittedMovementsCount = allMovements
                    .Count(m => m.Id != am.Id
                        && m.Status == DocumentStatus.Submitted
                        && m.Items.Any(i => i.AssetId == item.AssetId));

                if (otherSubmittedMovementsCount == 0)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Asset {asset.AssetName} has only one movement record. Please create another movement before cancelling this one to maintain asset tracking.");
                }
            }
        }

        am.Cancel();

        // Revert location and custodian separately from latest submitted movements (PR #60019)
        var allSubmittedMovements = allMovements
            .Where(m => m.Id != am.Id && m.Status == DocumentStatus.Submitted)
            .OrderByDescending(m => m.TransactionDate)
            .ThenByDescending(m => m.CreationTime)
            .ToList();

        foreach (var item in am.Items)
        {
            var asset = await _assetRepository.FindAsync(item.AssetId);
            if (asset != null)
            {
                var latestLocMovement = allSubmittedMovements
                    .FirstOrDefault(m => (m.Purpose is AssetMovementPurpose.Receipt or AssetMovementPurpose.Transfer or AssetMovementPurpose.TransferAndIssue)
                        && m.Items.Any(i => i.AssetId == item.AssetId && (i.TargetLocationId.HasValue || !string.IsNullOrWhiteSpace(i.TargetLocation))));

                string? revertedLocation = null;
                Guid? revertedLocationId = null;

                if (latestLocMovement != null)
                {
                    var locItem = latestLocMovement.Items.First(i => i.AssetId == item.AssetId);
                    revertedLocation = locItem.TargetLocation;
                    revertedLocationId = locItem.TargetLocationId;
                }
                else
                {
                    revertedLocation = item.SourceLocation;
                    revertedLocationId = item.SourceLocationId;
                }

                var latestCustMovement = allSubmittedMovements
                    .FirstOrDefault(m => (m.Purpose is AssetMovementPurpose.Issue or AssetMovementPurpose.Receipt or AssetMovementPurpose.TransferAndIssue)
                        && m.Items.Any(i => i.AssetId == item.AssetId));

                Guid? revertedCustodian = null;
                if (latestCustMovement != null)
                {
                    var custItem = latestCustMovement.Items.First(i => i.AssetId == item.AssetId);
                    revertedCustodian = custItem.ToEmployeeId;
                }
                else
                {
                    revertedCustodian = item.FromEmployeeId;
                }

                asset.Location = revertedLocation;
                asset.LocationId = revertedLocationId;
                asset.CustodianEmployeeId = revertedCustodian;
                await _assetRepository.UpdateAsync(asset);

                var activity = new AssetActivity(
                    GuidGenerator.Create(),
                    asset.Id,
                    AssetActivityType.Moved,
                    $"Asset movement cancelled: reverted to location {revertedLocation ?? "N/A"}",
                    DateTime.UtcNow,
                    $"Cancelled Movement #{am.MovementNumber}",
                    "AssetMovement",
                    am.Id.ToString(),
                    CurrentTenant.Id);

                await _activityRepository.InsertAsync(activity);
            }
        }

        await _repository.UpdateAsync(am);
        return _mapper.Map(am);
    }
}
