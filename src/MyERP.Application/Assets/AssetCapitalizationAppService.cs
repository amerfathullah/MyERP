using System;
using System.Collections.Generic;
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

/// <summary>
/// Manages Asset Capitalization — CWIP (Capital Work in Progress) to Asset conversion.
/// Consumes: stock items, service/expense items, and existing assets to create a new composite asset.
/// Per ERPNext: TotalAssetValue auto-calculated from all consumed sources.
/// </summary>
[Authorize(MyERPPermissions.Assets.Default)]
public class AssetCapitalizationAppService : ApplicationService
{
    private readonly IRepository<AssetCapitalization, Guid> _repository;

    public AssetCapitalizationAppService(IRepository<AssetCapitalization, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<AssetCapitalizationDto> GetAsync(Guid id)
    {
        var cap = await _repository.GetAsync(id);
        return ObjectMapper.Map<AssetCapitalization, AssetCapitalizationDto>(cap);
    }

    public async Task<PagedResultDto<AssetCapitalizationDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        var count = query.Count();
        var list = query.OrderByDescending(x => x.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<AssetCapitalizationDto>(count, list.Select(ObjectMapper.Map<AssetCapitalization, AssetCapitalizationDto>).ToList());
    }

    [Authorize(MyERPPermissions.Assets.Create)]
    public async Task<AssetCapitalizationDto> CreateAsync(CreateAssetCapitalizationDto input)
    {
        var cap = new AssetCapitalization(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CapitalizationNumber,
            input.PostingDate,
            input.TargetAssetId,
            CurrentTenant.Id);

        cap.TargetAssetName = input.TargetAssetName;

        foreach (var item in input.StockItems)
        {
            cap.AddStockItem(item.ItemId, item.ItemName, item.Quantity, item.Rate, item.WarehouseId);
        }

        foreach (var item in input.ServiceItems)
        {
            cap.AddServiceItem(item.ItemId, item.ItemName, item.Amount, item.ExpenseAccountId);
        }

        foreach (var item in input.ConsumedAssets)
        {
            cap.AddConsumedAsset(item.AssetId, item.AssetName, item.ValueAfterDepreciation);
        }

        await _repository.InsertAsync(cap);
        return ObjectMapper.Map<AssetCapitalization, AssetCapitalizationDto>(cap);
    }

    [Authorize(MyERPPermissions.Assets.Submit)]
    public async Task SubmitAsync(Guid id)
    {
        var cap = await _repository.GetAsync(id);
        cap.Submit();
        await _repository.UpdateAsync(cap);
    }

    [Authorize(MyERPPermissions.Assets.Submit)]
    public async Task CancelAsync(Guid id)
    {
        var cap = await _repository.GetAsync(id);
        cap.Cancel();
        await _repository.UpdateAsync(cap);
    }
}

#region DTOs

public class AssetCapitalizationDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string? TargetAssetName { get; set; }
    public Guid TargetAssetId { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal TotalAssetValue { get; set; }
    public string Status { get; set; } = null!;
}

public class CreateAssetCapitalizationDto
{
    public Guid CompanyId { get; set; }
    public string CapitalizationNumber { get; set; } = null!;
    public string? TargetAssetName { get; set; }
    public Guid TargetAssetId { get; set; }
    public DateTime PostingDate { get; set; }
    public List<CapStockItemDto> StockItems { get; set; } = new();
    public List<CapServiceItemDto> ServiceItems { get; set; } = new();
    public List<CapConsumedAssetDto> ConsumedAssets { get; set; } = new();
}

public class CapStockItemDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class CapServiceItemDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Amount { get; set; }
    public Guid? ExpenseAccountId { get; set; }
}

public class CapConsumedAssetDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = null!;
    public decimal ValueAfterDepreciation { get; set; }
}

#endregion
