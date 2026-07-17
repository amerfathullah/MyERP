using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Assets.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

public class AssetRepairDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }
    public string? RepairDescription { get; set; }
    public DateTime? FailureDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public decimal RepairCost { get; set; }
    public bool CapitalizeRepairCost { get; set; }
    public int IncreaseInAssetLife { get; set; }
    public decimal StockItemConsumedCost { get; set; }
    public int Status { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreateAssetRepairDto
{
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }
    public string? RepairDescription { get; set; }
    public DateTime? FailureDate { get; set; }
    public decimal RepairCost { get; set; }
    public bool CapitalizeRepairCost { get; set; }
    public int IncreaseInAssetLife { get; set; }
}

[Authorize(MyERPPermissions.Assets.Default)]
public class AssetRepairAppService : ApplicationService
{
    private readonly IRepository<AssetRepair, Guid> _repairRepository;
    private readonly IRepository<Asset, Guid> _assetRepository;

    public AssetRepairAppService(
        IRepository<AssetRepair, Guid> repairRepository,
        IRepository<Asset, Guid> assetRepository)
    {
        _repairRepository = repairRepository;
        _assetRepository = assetRepository;
    }

    public async Task<PagedResultDto<AssetRepairDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repairRepository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(r => r.CompanyId == input.CompanyId.Value);
        var totalCount = query.Count();
        var items = query.OrderByDescending(r => r.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<AssetRepairDto>(totalCount, items.Select(x => ObjectMapper.Map<AssetRepair, AssetRepairDto>(x)).ToList());
    }

    public async Task<AssetRepairDto> GetAsync(Guid id)
    {
        var repair = await _repairRepository.GetAsync(id);
        return ObjectMapper.Map<AssetRepair, AssetRepairDto>(repair);
    }

    [Authorize(MyERPPermissions.Assets.Create)]
    public async Task<AssetRepairDto> CreateAsync(CreateAssetRepairDto input)
    {
        var asset = await _assetRepository.GetAsync(input.AssetId);

        var repair = new AssetRepair(GuidGenerator.Create(), input.CompanyId,
            input.AssetId, CurrentTenant.Id)
        {
            RepairDescription = input.RepairDescription,
            FailureDate = input.FailureDate,
            RepairCost = input.RepairCost,
            CapitalizeRepairCost = input.CapitalizeRepairCost,
            IncreaseInAssetLife = input.IncreaseInAssetLife,
        };

        // Per gotcha #35: fully depreciated assets can be repaired
        // but capitalize_repair_cost and increase_in_asset_life are forced to 0
        repair.ApplyFullyDepreciatedRules(
            asset.IsFullyDepreciated || asset.Status == AssetStatus.FullyDepreciated);

        await _repairRepository.InsertAsync(repair);
        return ObjectMapper.Map<AssetRepair, AssetRepairDto>(repair);
    }

    [Authorize(MyERPPermissions.Assets.Submit)]
    public async Task<AssetRepairDto> CompleteAsync(Guid id)
    {
        var repair = await _repairRepository.GetAsync(id);
        repair.Complete();

        // If capitalizing repair cost, update asset value
        if (repair.CapitalizeRepairCost && repair.RepairCost > 0)
        {
            var asset = await _assetRepository.GetAsync(repair.AssetId);
            asset.ValueAfterDepreciation += repair.RepairCost;

            // Extend useful life if specified
            if (repair.IncreaseInAssetLife > 0)
            {
                asset.UsefulLifeMonths += repair.IncreaseInAssetLife;
            }

            await _assetRepository.UpdateAsync(asset);
        }

        await _repairRepository.UpdateAsync(repair);
        return ObjectMapper.Map<AssetRepair, AssetRepairDto>(repair);
    }

    [Authorize(MyERPPermissions.Assets.Submit)]
    public async Task<AssetRepairDto> CancelAsync(Guid id)
    {
        var repair = await _repairRepository.GetAsync(id);
        repair.Cancel();
        await _repairRepository.UpdateAsync(repair);
        return ObjectMapper.Map<AssetRepair, AssetRepairDto>(repair);
    }
}
