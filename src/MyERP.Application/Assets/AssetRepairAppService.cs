using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
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
    private readonly IRepository<MyERP.Purchasing.Entities.PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly AssetRepairMapper _mapper;
    private readonly AssetLifecycleManager _lifecycleManager;

    public AssetRepairAppService(
        IRepository<AssetRepair, Guid> repository,
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetActivity, Guid> activityRepository,
        IRepository<MyERP.Purchasing.Entities.PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        AssetRepairMapper mapper,
        AssetLifecycleManager lifecycleManager)
    {
        _repository = repository;
        _assetRepository = assetRepository;
        _activityRepository = activityRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _journalEntryRepository = journalEntryRepository;
        _mapper = mapper;
        _lifecycleManager = lifecycleManager;
    }

    /// <summary>
    /// Per ERPNext asset_repair.py validate_purchase_invoice_status: every referenced Purchase
    /// Invoice must actually be submitted, and belong to the same company as the repair — neither
    /// was checked here, so an Asset Repair could capitalize a cost claimed against a Draft/
    /// Cancelled invoice, or one from an unrelated company, straight onto the asset's book value.
    /// </summary>
    private async Task ValidateInvoiceRowsAsync(CreateUpdateAssetRepairDto input)
    {
        if (input.Invoices == null || !input.Invoices.Any()) return;

        var invoiceIds = input.Invoices.Select(i => i.PurchaseInvoiceId).Distinct().ToArray();
        var invoices = (await _purchaseInvoiceRepository.GetQueryableAsync())
            .Where(pi => invoiceIds.Contains(pi.Id))
            .ToDictionary(pi => pi.Id);

        foreach (var invoiceId in invoiceIds)
        {
            if (!invoices.TryGetValue(invoiceId, out var pi))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Purchase Invoice {invoiceId} does not exist.");
            }
            if (pi.CompanyId != input.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Purchase Invoice {pi.InvoiceNumber} belongs to a different company.");
            }
            // MyERP splits ERPNext's atomic "submit" into Submit (status change) + Post (GL creation)
            // steps, so a fully processed invoice ends at Posted, not Submitted. Accepting only
            // Submitted would reject exactly the invoices whose GL actually exists to claim against.
            if (pi.Status != Core.DocumentStatus.Submitted && pi.Status != Core.DocumentStatus.Posted)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Purchase Invoice {pi.InvoiceNumber} must be submitted.");
            }
        }
    }

    /// <summary>
    /// Per ERPNext asset_repair.py validate_purchase_invoice_repair_cost / get_unallocated_repair_cost:
    /// the repair cost claimed against a given (Purchase Invoice, Expense Account) pair must not exceed
    /// what was actually posted to GL for that pair, minus whatever other completed Asset Repair
    /// documents have already claimed against the same pair. Without this, two separate Asset Repair
    /// documents could each capitalize the full invoice expense onto their own asset's book value,
    /// double- (or N-times-) counting the same underlying Purchase Invoice cost. "Completed" is
    /// MyERP's equivalent of ERPNext's submitted (docstatus=1) state — Pending repairs, like ERPNext
    /// drafts, don't count as allocated yet, so re-running this check in CompleteAsync (the "submit"
    /// moment) is what actually resolves a race between two still-Pending documents claiming the
    /// same pair: whichever completes first locks in its allocation.
    /// </summary>
    private async Task ValidateRepairCostAllocationAsync(
        Guid? excludeRepairId,
        IEnumerable<(Guid PurchaseInvoiceId, Guid ExpenseAccountId, decimal RepairCost)> rows)
    {
        var claims = rows.Where(r => r.RepairCost > 0).ToList();
        if (claims.Count == 0) return;

        var invoiceIds = claims.Select(c => c.PurchaseInvoiceId).Distinct().ToArray();

        var jeQuery = await _journalEntryRepository.GetQueryableAsync();
        var journals = jeQuery
            .Where(je => je.ReferenceType == "PurchaseInvoice" && je.ReferenceId.HasValue && invoiceIds.Contains(je.ReferenceId.Value))
            .Where(je => je.Status == Core.DocumentStatus.Posted)
            .ToList();

        var repairQuery = await _repository.WithDetailsAsync(r => r.Invoices);
        var otherCompletedRepairs = repairQuery
            .Where(r => r.Status == AssetRepairStatus.Completed && r.Id != excludeRepairId)
            .ToList();

        foreach (var claim in claims)
        {
            var glTotal = journals
                .Where(je => je.ReferenceId == claim.PurchaseInvoiceId)
                .SelectMany(je => je.Lines)
                .Where(l => l.AccountId == claim.ExpenseAccountId)
                .Sum(l => l.IsDebit ? l.Amount : -l.Amount);

            var allocatedByOthers = otherCompletedRepairs
                .SelectMany(r => r.Invoices)
                .Where(i => i.PurchaseInvoiceId == claim.PurchaseInvoiceId && i.ExpenseAccountId == claim.ExpenseAccountId)
                .Sum(i => i.RepairCost);

            var unallocated = glTotal - allocatedByOthers;

            if (claim.RepairCost > unallocated)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail",
                        $"Claimed repair cost ({claim.RepairCost:N2}) exceeds the unallocated posted expense " +
                        $"({unallocated:N2}) for this Purchase Invoice and Expense Account.");
            }
        }
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

        // Disallow repair on fully depreciated, sold, scrapped, or cancelled assets (ERPNext PR #51753 / commit 66fe1aa85d)
        if (asset.Status is AssetStatus.FullyDepreciated or AssetStatus.Sold or AssetStatus.Scrapped or AssetStatus.Cancelled or AssetStatus.Draft)
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

        // Validate non-negative repair cost (ERPNext PR #49190 / commit c140596ab3)
        if (input.RepairCost < 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "RepairCost");
        }

        // Per ERPNext validate_purchase_invoice_status: every referenced invoice must be submitted
        // and belong to this company.
        await ValidateInvoiceRowsAsync(input);

        // Validate duplicate purchase invoice rows (per ERPNext PR #50804 / commit ff9b392024)
        if (input.Invoices != null && input.Invoices.Any())
        {
            if (input.Invoices.Any(i => i.RepairCost < 0))
            {
                throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                    .WithData("field", "RepairCost");
            }

            var duplicates = input.Invoices
                .GroupBy(i => (i.PurchaseInvoiceId, i.ExpenseAccountId))
                .Where(g => g.Count() > 1)
                .ToList();

            if (duplicates.Any())
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Duplicate Purchase Invoice and Expense Account combination found in Asset Repair invoice rows.");
            }

            // Per ERPNext validate_purchase_invoice_repair_cost: claimed cost per (invoice, expense
            // account) pair cannot exceed what's unallocated against that pair's posted GL amount.
            await ValidateRepairCostAllocationAsync(
                excludeRepairId: null,
                input.Invoices
                    .Where(i => i.ExpenseAccountId.HasValue)
                    .Select(i => (i.PurchaseInvoiceId, i.ExpenseAccountId!.Value, i.RepairCost)));
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
        repair.SetDowntime();

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

        // Disallow repair on fully depreciated, sold, scrapped, or cancelled assets (ERPNext PR #51753 / commit 66fe1aa85d)
        if (asset.Status is AssetStatus.FullyDepreciated or AssetStatus.Sold or AssetStatus.Scrapped or AssetStatus.Cancelled or AssetStatus.Draft)
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

        // Validate non-negative repair cost (ERPNext PR #49190 / commit c140596ab3)
        if (input.RepairCost < 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "RepairCost");
        }

        // Per ERPNext validate_purchase_invoice_status: every referenced invoice must be submitted
        // and belong to this company.
        await ValidateInvoiceRowsAsync(input);

        // Validate duplicate purchase invoice rows (per ERPNext PR #50804 / commit ff9b392024)
        if (input.Invoices != null && input.Invoices.Any())
        {
            if (input.Invoices.Any(i => i.RepairCost < 0))
            {
                throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                    .WithData("field", "RepairCost");
            }

            var duplicates = input.Invoices
                .GroupBy(i => (i.PurchaseInvoiceId, i.ExpenseAccountId))
                .Where(g => g.Count() > 1)
                .ToList();

            if (duplicates.Any())
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Duplicate Purchase Invoice and Expense Account combination found in Asset Repair invoice rows.");
            }

            // Per ERPNext validate_purchase_invoice_repair_cost: claimed cost per (invoice, expense
            // account) pair cannot exceed what's unallocated against that pair's posted GL amount.
            await ValidateRepairCostAllocationAsync(
                excludeRepairId: repair.Id,
                input.Invoices
                    .Where(i => i.ExpenseAccountId.HasValue)
                    .Select(i => (i.PurchaseInvoiceId, i.ExpenseAccountId!.Value, i.RepairCost)));
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
        repair.SetDowntime();

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

        // Re-check the GL allocation cap at completion (ERPNext re-runs validate() on submit): two
        // Asset Repairs can both be Pending and pass the create/update check against the same
        // (invoice, account) pair since neither counts as "allocated" yet — whichever completes
        // first here locks in its claim, and the second is blocked.
        if (repair.Invoices.Any())
        {
            await ValidateRepairCostAllocationAsync(
                excludeRepairId: repair.Id,
                repair.Invoices
                    .Where(i => i.ExpenseAccountId.HasValue)
                    .Select(i => (i.PurchaseInvoiceId, i.ExpenseAccountId!.Value, i.RepairCost)));
        }

        // Consumed stock cost is always added to asset value; repair cost is added if capitalized (ERPNext PR #47233 / commit ed8a8532e1)
        var capitalizedCost = repair.ConsumedItemsCost + (repair.CapitalizeRepairCost ? repair.RepairCost : 0m);
        if (capitalizedCost > 0)
        {
            var asset = await _assetRepository.GetAsync(repair.AssetId);
            asset.ApplyRepairCapitalization(capitalizedCost, repair.CapitalizeRepairCost ? repair.IncreaseInAssetLife : 0);
            await _assetRepository.UpdateAsync(asset);

            var activity = new AssetActivity(
                GuidGenerator.Create(),
                asset.Id,
                AssetActivityType.Repaired,
                $"Asset repair #{repair.RepairNumber} capitalized",
                repair.CompletionDate ?? DateTime.UtcNow,
                $"Capitalized amount: {capitalizedCost:N2}, Life extension: {(repair.CapitalizeRepairCost ? repair.IncreaseInAssetLife : 0)} months",
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

        var capitalizedCost = repair.ConsumedItemsCost + (repair.CapitalizeRepairCost ? repair.RepairCost : 0m);
        if (capitalizedCost > 0)
        {
            var asset = await _assetRepository.GetAsync(repair.AssetId);
            asset.ApplyRepairCapitalization(-1 * capitalizedCost, repair.CapitalizeRepairCost ? -1 * repair.IncreaseInAssetLife : 0);
            await _assetRepository.UpdateAsync(asset);

            var activity = new AssetActivity(
                GuidGenerator.Create(),
                asset.Id,
                AssetActivityType.Repaired,
                $"Asset repair #{repair.RepairNumber} cancelled",
                DateTime.UtcNow,
                $"Reverted capitalized repair cost of {capitalizedCost:N2}",
                "AssetRepair",
                repair.Id.ToString(),
                CurrentTenant.Id);

            await _activityRepository.InsertAsync(activity);
        }

        await _repository.UpdateAsync(repair);
        return _mapper.Map(repair);
    }
}
