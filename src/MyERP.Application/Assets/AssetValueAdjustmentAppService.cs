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

[Authorize(MyERPPermissions.AssetValueAdjustments.Default)]
public class AssetValueAdjustmentAppService : ApplicationService, IAssetValueAdjustmentAppService
{
    private readonly IRepository<AssetValueAdjustment, Guid> _repository;
    private readonly IRepository<Asset, Guid> _assetRepository;
    private readonly IRepository<AssetActivity, Guid> _activityRepository;
    private readonly AssetValueAdjustmentMapper _mapper;

    public AssetValueAdjustmentAppService(
        IRepository<AssetValueAdjustment, Guid> repository,
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetActivity, Guid> activityRepository,
        AssetValueAdjustmentMapper mapper)
    {
        _repository = repository;
        _assetRepository = assetRepository;
        _activityRepository = activityRepository;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<AssetValueAdjustmentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var items = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(a => a.Date)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        return new PagedResultDto<AssetValueAdjustmentDto>(totalCount, items.Select(_mapper.Map).ToList());
    }

    public async Task<AssetValueAdjustmentDto> GetAsync(Guid id)
    {
        var adj = await _repository.GetAsync(id);
        return _mapper.Map(adj);
    }

    [Authorize(MyERPPermissions.AssetValueAdjustments.Create)]
    public async Task<AssetValueAdjustmentDto> CreateAsync(CreateUpdateAssetValueAdjustmentDto input)
    {
        var asset = await _assetRepository.GetAsync(input.AssetId);
        var currentVal = input.CurrentAssetValue > 0 ? input.CurrentAssetValue : asset.ValueAfterDepreciation;

        var adjNumber = $"AS-ADJ-{DateTime.UtcNow:yyyyMMdd}-{GuidGenerator.Create().ToString()[..6].ToUpper()}";
        var adj = new AssetValueAdjustment(
            GuidGenerator.Create(),
            adjNumber,
            input.CompanyId,
            input.AssetId,
            input.Date,
            currentVal,
            input.NewAssetValue,
            input.DifferenceAccountId,
            input.FinanceBookId,
            input.CostCenterId,
            CurrentTenant.Id)
        {
            Notes = input.Notes,
        };

        await _repository.InsertAsync(adj);
        return _mapper.Map(adj);
    }

    [Authorize(MyERPPermissions.AssetValueAdjustments.Edit)]
    public async Task<AssetValueAdjustmentDto> UpdateAsync(Guid id, CreateUpdateAssetValueAdjustmentDto input)
    {
        var adj = await _repository.GetAsync(id);
        if (adj.Status != Core.DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        adj.Date = input.Date;
        adj.UpdateValues(input.CurrentAssetValue, input.NewAssetValue);
        adj.DifferenceAccountId = input.DifferenceAccountId;
        adj.FinanceBookId = input.FinanceBookId;
        adj.CostCenterId = input.CostCenterId;
        adj.Notes = input.Notes;

        await _repository.UpdateAsync(adj);
        return _mapper.Map(adj);
    }

    [Authorize(MyERPPermissions.AssetValueAdjustments.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var adj = await _repository.GetAsync(id);
        if (adj.Status != Core.DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        await _repository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.AssetValueAdjustments.Edit)]
    public async Task<AssetValueAdjustmentDto> SubmitAsync(Guid id)
    {
        var adj = await _repository.GetAsync(id);
        adj.Submit();

        var asset = await _assetRepository.GetAsync(adj.AssetId);
        asset.ApplyValueAdjustment(adj.NewAssetValue);
        await _assetRepository.UpdateAsync(asset);

        var activity = new AssetActivity(
            GuidGenerator.Create(),
            asset.Id,
            AssetActivityType.Adjusted,
            $"Asset Value Adjustment #{adj.AdjustmentNumber} submitted",
            adj.Date,
            $"Adjusted from {adj.CurrentAssetValue:N2} to {adj.NewAssetValue:N2} (Diff: {adj.DifferenceAmount:N2})",
            "AssetValueAdjustment",
            adj.Id.ToString(),
            CurrentTenant.Id);

        await _activityRepository.InsertAsync(activity);
        await _repository.UpdateAsync(adj);
        return _mapper.Map(adj);
    }

    [Authorize(MyERPPermissions.AssetValueAdjustments.Edit)]
    public async Task<AssetValueAdjustmentDto> CancelAsync(Guid id)
    {
        var adj = await _repository.GetAsync(id);
        adj.Cancel();

        var asset = await _assetRepository.GetAsync(adj.AssetId);
        asset.ApplyValueAdjustment(adj.CurrentAssetValue);
        await _assetRepository.UpdateAsync(asset);

        var activity = new AssetActivity(
            GuidGenerator.Create(),
            asset.Id,
            AssetActivityType.Adjusted,
            $"Asset Value Adjustment #{adj.AdjustmentNumber} cancelled",
            DateTime.UtcNow,
            $"Reverted back to {adj.CurrentAssetValue:N2}",
            "AssetValueAdjustment",
            adj.Id.ToString(),
            CurrentTenant.Id);

        await _activityRepository.InsertAsync(activity);
        await _repository.UpdateAsync(adj);
        return _mapper.Map(adj);
    }
}
