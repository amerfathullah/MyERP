using System;
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

[Authorize(MyERPPermissions.AssetMovements.Default)]
public class AssetMovementAppService : ApplicationService, IAssetMovementAppService
{
    private readonly IRepository<AssetMovement, Guid> _repository;
    private readonly IRepository<Asset, Guid> _assetRepository;
    private readonly IRepository<AssetActivity, Guid> _activityRepository;
    private readonly IRepository<Location, Guid> _locationRepository;
    private readonly AssetMovementMapper _mapper;

    public AssetMovementAppService(
        IRepository<AssetMovement, Guid> repository,
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetActivity, Guid> activityRepository,
        IRepository<Location, Guid> locationRepository,
        AssetMovementMapper mapper)
    {
        _repository = repository;
        _assetRepository = assetRepository;
        _activityRepository = activityRepository;
        _locationRepository = locationRepository;
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

        am.Submit();

        // Update target assets location and custodian
        foreach (var item in am.Items)
        {
            var asset = await _assetRepository.FindAsync(item.AssetId);
            if (asset != null)
            {
                asset.UpdateLocationAndCustodian(item.TargetLocation, item.ToEmployeeId, item.TargetLocationId);
                await _assetRepository.UpdateAsync(asset);

                // Audit Activity
                var activity = new AssetActivity(
                    GuidGenerator.Create(),
                    asset.Id,
                    AssetActivityType.Moved,
                    $"Asset moved to location {item.TargetLocation ?? "N/A"}",
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

        am.Cancel();

        // Revert location and custodian
        foreach (var item in am.Items)
        {
            var asset = await _assetRepository.FindAsync(item.AssetId);
            if (asset != null)
            {
                asset.UpdateLocationAndCustodian(item.SourceLocation, item.FromEmployeeId, item.SourceLocationId);
                await _assetRepository.UpdateAsync(asset);

                var activity = new AssetActivity(
                    GuidGenerator.Create(),
                    asset.Id,
                    AssetActivityType.Moved,
                    $"Asset movement cancelled: reverted to location {item.SourceLocation ?? "N/A"}",
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
