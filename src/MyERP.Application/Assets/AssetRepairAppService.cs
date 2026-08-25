using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Assets.DomainServices;
using MyERP.Assets.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.AssetRepairs.Default)]
public class AssetRepairAppService : ApplicationService, IAssetRepairAppService
{
    private readonly IRepository<AssetRepair, Guid> _repository;
    private readonly IRepository<Asset, Guid> _assetRepository;
    private readonly IRepository<AssetActivity, Guid> _activityRepository;
    private readonly AssetRepairMapper _mapper;
    private readonly AssetLifecycleManager _lifecycleManager;

    public AssetRepairAppService(
        IRepository<AssetRepair, Guid> repository,
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetActivity, Guid> activityRepository,
        AssetRepairMapper mapper,
        AssetLifecycleManager lifecycleManager)
    {
        _repository = repository;
        _assetRepository = assetRepository;
        _activityRepository = activityRepository;
        _mapper = mapper;
        _lifecycleManager = lifecycleManager;
    }

    public async Task<PagedResultDto<AssetRepairDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.WithDetailsAsync(r => r.StockItems, r => r.Invoices);
        var totalCount = await AsyncExecuter.CountAsync(query);
        var items = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(r => r.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        return new PagedResultDto<AssetRepairDto>(totalCount, items.Select(_mapper.Map).ToList());
    }

    public async Task<AssetRepairDto> GetAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(r => r.StockItems, r => r.Invoices);
        var repair = await AsyncExecuter.FirstOrDefaultAsync(query, r => r.Id == id);

        if (repair == null)
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);

        return _mapper.Map(repair);
    }

    [Authorize(MyERPPermissions.AssetRepairs.Create)]
    public async Task<AssetRepairDto> CreateAsync(CreateUpdateAssetRepairDto input)
    {
        var asset = await _assetRepository.GetAsync(input.AssetId);

        if (asset.CompanyId != input.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AssetCompanyMismatch)
                .WithData("assetName", asset.AssetName);
        }

        // Disallow repair on sold/scrapped assets
        if (asset.Status is AssetStatus.Sold or AssetStatus.Scrapped)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AssetCannotBeMoved)
                .WithData("assetName", asset.AssetName)
                .WithData("status", asset.Status.ToString());
        }

        // Validate completion date not before failure date
        if (input.CompletionDate.HasValue && input.CompletionDate.Value < input.FailureDate)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        // Validate stock items are active
        if (input.StockItems != null && input.StockItems.Any())
        {
            var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
            await itemValidation.ValidateItemsForTransactionAsync(input.StockItems.Select(i => i.ItemId).ToArray());
        }

        var repairNumber = $"AS-REP-{DateTime.UtcNow:yyyyMMdd}-{GuidGenerator.Create().ToString()[..6].ToUpper()}";
        var repair = new AssetRepair(
            GuidGenerator.Create(),
            repairNumber,
            input.CompanyId,
            input.AssetId,
            CurrentTenant.Id)
        {
            RepairDescription = input.RepairDescription,
            ActionsPerformed = input.ActionsPerformed,
            Downtime = input.Downtime,
            FailureDate = input.FailureDate,
            CompletionDate = input.CompletionDate,
            CostCenterId = input.CostCenterId,
            ProjectId = input.ProjectId,
            RepairCost = input.RepairCost,
            CapitalizeRepairCost = input.CapitalizeRepairCost,
            IncreaseInAssetLife = input.IncreaseInAssetLife,
        };

        if (input.StockItems != null)
        {
            foreach (var stockItem in input.StockItems)
            {
                repair.AddStockItem(
                    GuidGenerator.Create(),
                    stockItem.ItemId,
                    stockItem.Qty,
                    stockItem.ValuationRate,
                    stockItem.WarehouseId,
                    stockItem.ItemName,
                    stockItem.SerialAndBatchBundleId);
            }
        }

        if (input.Invoices != null)
        {
            foreach (var inv in input.Invoices)
            {
                repair.AddInvoice(
                    GuidGenerator.Create(),
                    inv.PurchaseInvoiceId,
                    inv.RepairCost,
                    inv.PurchaseInvoiceNumber,
                    inv.ExpenseAccountId);
            }
        }

        // Per gotcha #35: fully depreciated assets can be repaired
        // but capitalize_repair_cost and increase_in_asset_life are forced to 0
        repair.ApplyFullyDepreciatedRules(!_lifecycleManager.GetRepairOptions(asset).CanCapitalize);

        repair.CalculateTotals();

        await _repository.InsertAsync(repair);
        return _mapper.Map(repair);
    }

    [Authorize(MyERPPermissions.AssetRepairs.Edit)]
    public async Task<AssetRepairDto> UpdateAsync(Guid id, CreateUpdateAssetRepairDto input)
    {
        var query = await _repository.WithDetailsAsync(r => r.StockItems, r => r.Invoices);
        var repair = await AsyncExecuter.FirstOrDefaultAsync(query, r => r.Id == id);

        if (repair == null)
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);

        if (repair.Status != AssetRepairStatus.Pending)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        var asset = await _assetRepository.GetAsync(input.AssetId);

        if (asset.CompanyId != input.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AssetCompanyMismatch)
                .WithData("assetName", asset.AssetName);
        }

        if (asset.Status is AssetStatus.Sold or AssetStatus.Scrapped)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AssetCannotBeMoved)
                .WithData("assetName", asset.AssetName)
                .WithData("status", asset.Status.ToString());
        }

        if (input.CompletionDate.HasValue && input.CompletionDate.Value < input.FailureDate)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        if (input.StockItems != null && input.StockItems.Any())
        {
            var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
            await itemValidation.ValidateItemsForTransactionAsync(input.StockItems.Select(i => i.ItemId).ToArray());
        }

        repair.AssetId = input.AssetId;
        repair.RepairDescription = input.RepairDescription;
        repair.ActionsPerformed = input.ActionsPerformed;
        repair.Downtime = input.Downtime;
        repair.FailureDate = input.FailureDate;
        repair.CompletionDate = input.CompletionDate;
        repair.CostCenterId = input.CostCenterId;
        repair.ProjectId = input.ProjectId;
        repair.RepairCost = input.RepairCost;
        repair.CapitalizeRepairCost = input.CapitalizeRepairCost;
        repair.IncreaseInAssetLife = input.IncreaseInAssetLife;

        repair.StockItems.Clear();
        if (input.StockItems != null)
        {
            foreach (var stockItem in input.StockItems)
            {
                repair.AddStockItem(
                    stockItem.Id ?? GuidGenerator.Create(),
                    stockItem.ItemId,
                    stockItem.Qty,
                    stockItem.ValuationRate,
                    stockItem.WarehouseId,
                    stockItem.ItemName,
                    stockItem.SerialAndBatchBundleId);
            }
        }

        repair.Invoices.Clear();
        if (input.Invoices != null)
        {
            foreach (var inv in input.Invoices)
            {
                repair.AddInvoice(
                    inv.Id ?? GuidGenerator.Create(),
                    inv.PurchaseInvoiceId,
                    inv.RepairCost,
                    inv.PurchaseInvoiceNumber,
                    inv.ExpenseAccountId);
            }
        }

        repair.ApplyFullyDepreciatedRules(!_lifecycleManager.GetRepairOptions(asset).CanCapitalize);

        repair.CalculateTotals();

        await _repository.UpdateAsync(repair);
        return _mapper.Map(repair);
    }

    [Authorize(MyERPPermissions.AssetRepairs.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var repair = await _repository.GetAsync(id);
        if (repair.Status != AssetRepairStatus.Pending)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        await _repository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.AssetRepairs.Edit)]
    public async Task<AssetRepairDto> CompleteAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(r => r.StockItems, r => r.Invoices);
        var repair = await AsyncExecuter.FirstOrDefaultAsync(query, r => r.Id == id);

        if (repair == null)
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);

        repair.Complete();

        // If capitalizing repair cost, update asset value and schedule
        if (repair.CapitalizeRepairCost && repair.TotalRepairCost > 0)
        {
            var asset = await _assetRepository.GetAsync(repair.AssetId);
            asset.ApplyRepairCapitalization(repair.TotalRepairCost, repair.IncreaseInAssetLife);
            await _assetRepository.UpdateAsync(asset);

            var activity = new AssetActivity(
                GuidGenerator.Create(),
                asset.Id,
                AssetActivityType.Repaired,
                $"Asset repair #{repair.RepairNumber} capitalized",
                repair.CompletionDate ?? DateTime.UtcNow,
                $"Capitalized amount: {repair.TotalRepairCost:N2}, Life extension: {repair.IncreaseInAssetLife} months",
                "AssetRepair",
                repair.Id.ToString(),
                CurrentTenant.Id);

            await _activityRepository.InsertAsync(activity);
        }

        await _repository.UpdateAsync(repair);
        return _mapper.Map(repair);
    }

    [Authorize(MyERPPermissions.AssetRepairs.Edit)]
    public async Task<AssetRepairDto> CancelAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(r => r.StockItems, r => r.Invoices);
        var repair = await AsyncExecuter.FirstOrDefaultAsync(query, r => r.Id == id);

        if (repair == null)
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);

        repair.Cancel();

        if (repair.CapitalizeRepairCost && repair.TotalRepairCost > 0)
        {
            var asset = await _assetRepository.GetAsync(repair.AssetId);
            asset.ApplyRepairCapitalization(-1 * repair.TotalRepairCost, -1 * repair.IncreaseInAssetLife);
            await _assetRepository.UpdateAsync(asset);

            var activity = new AssetActivity(
                GuidGenerator.Create(),
                asset.Id,
                AssetActivityType.Repaired,
                $"Asset repair #{repair.RepairNumber} cancelled",
                DateTime.UtcNow,
                $"Reverted capitalized repair cost of {repair.TotalRepairCost:N2}",
                "AssetRepair",
                repair.Id.ToString(),
                CurrentTenant.Id);

            await _activityRepository.InsertAsync(activity);
        }

        await _repository.UpdateAsync(repair);
        return _mapper.Map(repair);
    }
}
