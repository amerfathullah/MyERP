using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Assets.DomainServices;

/// <summary>
/// Posts the GL journal entry AND the stock consumption for an Asset Capitalization.
/// Per the entity's own doc comment ("Consumed stock items — reduces inventory, adds to asset
/// value") both were meant to happen on submit; before this service existed, submitting an Asset
/// Capitalization updated the target asset's book value directly but never touched a
/// StockLedgerEntry (claimed stock consumption while leaving warehouse quantities untouched —
/// phantom stock) and posted zero GL for any of the three source types (stock/service/consumed
/// assets).
/// </summary>
public class AssetCapitalizationPostingService : DomainService
{
    private readonly IRepository<Asset, Guid> _assetRepository;
    private readonly IRepository<AssetCategory, Guid> _categoryRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly StockValuationService _valuationService;
    private readonly WarehouseAccountService _warehouseAccountService;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public AssetCapitalizationPostingService(
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetCategory, Guid> categoryRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        StockValuationService valuationService,
        WarehouseAccountService warehouseAccountService,
        IDocumentNumberGenerator numberGenerator)
    {
        _assetRepository = assetRepository;
        _categoryRepository = categoryRepository;
        _companyRepository = companyRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _journalEntryRepository = journalEntryRepository;
        _valuationService = valuationService;
        _warehouseAccountService = warehouseAccountService;
        _numberGenerator = numberGenerator;
    }

    /// <summary>
    /// Consumes stock items (real StockLedgerEntry per item, reducing the source warehouse), then
    /// posts one balanced JE: DR the target asset's Fixed Asset account for the recomputed total,
    /// CR each stock item's warehouse stock account (at its true FIFO/moving-average consumption
    /// cost, not the row's original estimated Rate — stock can only leave at its actual valuation),
    /// CR each service item's expense account, and derecognize each consumed asset (CR its Fixed
    /// Asset account for full cost, DR its Accumulated Depreciation for depreciation to date —
    /// same shape as AssetLifecycleManager's disposal JE, just landing in the target asset instead
    /// of a disposal/settlement account).
    /// </summary>
    /// <returns>
    /// The posted JE's id (null if the capitalization has no lines with value) and the recomputed
    /// total — the caller applies THIS amount to the target asset's book value so GL and asset
    /// value stay consistent, rather than the total estimated when the lines were first entered.
    /// </returns>
    public async Task<(Guid? JournalEntryId, decimal TotalCapitalizedAmount)> PostAsync(
        AssetCapitalization cap, Asset targetAsset)
    {
        var company = await _companyRepository.GetAsync(cap.CompanyId);

        var targetCategory = targetAsset.AssetCategoryId.HasValue
            ? (await _categoryRepository.WithDetailsAsync(c => c.Accounts))
                .FirstOrDefault(c => c.Id == targetAsset.AssetCategoryId.Value)
            : null;
        var targetAccounts = targetCategory?.GetAccountForCompany(targetAsset.CompanyId);
        if (targetAccounts == null)
            throw new BusinessException(MyERPDomainErrorCodes.AssetCapitalizationAccountMissing)
                .WithData("assetName", targetAsset.AssetName)
                .WithData("accountField", "AssetCategoryAccount (target asset)");

        var fiscalYear = (await _fiscalYearRepository.GetQueryableAsync())
            .FirstOrDefault(fy => fy.CompanyId == cap.CompanyId
                && fy.StartDate <= cap.PostingDate && fy.EndDate >= cap.PostingDate);
        if (fiscalYear == null)
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("postingDate", cap.PostingDate.ToString("yyyy-MM-dd"));

        decimal trueTotal = 0m;
        var creditLines = new List<(Guid AccountId, decimal Amount, string Description)>();
        var extraDebitLines = new List<(Guid AccountId, decimal Amount, string Description)>();

        foreach (var item in cap.StockItems)
        {
            if (!item.WarehouseId.HasValue)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Stock item '{item.ItemName}' has no warehouse to consume from.");

            // Read the true consumption value off the SLE CreateLedgerEntryAsync just returned
            // rather than re-querying the balance afterward — that insert isn't flushed to the DB
            // yet (repository default autoSave:false), so a fresh query wouldn't see it.
            var before = await _valuationService.GetCurrentBalanceAsync(item.ItemId, item.WarehouseId.Value);
            var sle = await _valuationService.CreateLedgerEntryAsync(
                cap.CompanyId, item.ItemId, item.WarehouseId.Value, cap.PostingDate,
                -item.Qty, item.Rate, "AssetCapitalization", cap.Id, targetAsset.TenantId);
            var consumedValue = before.Value - sle.BalanceValue;

            trueTotal += consumedValue;
            var stockAccountId = await _warehouseAccountService.ResolveStockAccountAsync(
                item.WarehouseId.Value, cap.CompanyId, item.ItemId);
            creditLines.Add((stockAccountId, consumedValue, $"Asset capitalization — stock consumed: {item.ItemName}"));
        }

        foreach (var item in cap.ServiceItems)
        {
            var expenseAccountId = item.ExpenseAccountId ?? company.DefaultExpenseAccountId;
            if (!expenseAccountId.HasValue)
                throw new BusinessException(MyERPDomainErrorCodes.AssetCapitalizationAccountMissing)
                    .WithData("assetName", targetAsset.AssetName)
                    .WithData("accountField", $"ExpenseAccountId (service item '{item.ItemName}')");

            trueTotal += item.Amount;
            creditLines.Add((expenseAccountId.Value, item.Amount, $"Asset capitalization — service consumed: {item.ItemName}"));
        }

        foreach (var consumed in cap.ConsumedAssets)
        {
            var sourceAsset = await _assetRepository.GetAsync(consumed.AssetId);
            var sourceCategory = sourceAsset.AssetCategoryId.HasValue
                ? (await _categoryRepository.WithDetailsAsync(c => c.Accounts))
                    .FirstOrDefault(c => c.Id == sourceAsset.AssetCategoryId.Value)
                : null;
            var sourceAccounts = sourceCategory?.GetAccountForCompany(sourceAsset.CompanyId);
            if (sourceAccounts == null)
                throw new BusinessException(MyERPDomainErrorCodes.AssetCapitalizationAccountMissing)
                    .WithData("assetName", sourceAsset.AssetName)
                    .WithData("accountField", "AssetCategoryAccount (consumed asset)");

            var currentValue = sourceAsset.ValueAfterDepreciation;
            var accumulatedDepreciation = sourceAsset.PurchaseAmount - currentValue;

            trueTotal += currentValue;
            creditLines.Add((sourceAccounts.FixedAssetAccountId, sourceAsset.PurchaseAmount,
                $"Asset capitalization — consumed asset: {sourceAsset.AssetName}"));

            if (accumulatedDepreciation != 0)
            {
                if (!sourceAccounts.AccumulatedDepreciationAccountId.HasValue)
                    throw new BusinessException(MyERPDomainErrorCodes.AssetCapitalizationAccountMissing)
                        .WithData("assetName", sourceAsset.AssetName)
                        .WithData("accountField", "AccumulatedDepreciationAccountId (consumed asset)");

                extraDebitLines.Add((sourceAccounts.AccumulatedDepreciationAccountId.Value, accumulatedDepreciation,
                    $"Asset capitalization — clear accumulated depreciation: {sourceAsset.AssetName}"));
            }
        }

        if (trueTotal <= 0)
        {
            // SubmitAsync already guards that the capitalization has at least one line, so reaching
            // here with nothing of value means every line valued at zero (e.g. a fully-depreciated
            // consumed asset with no other lines) — nothing to capitalize or post.
            return (null, 0m);
        }

        var jeNumber = await _numberGenerator.GenerateAsync("JE", cap.CompanyId);
        var je = new JournalEntry(GuidGenerator.Create(), cap.CompanyId, fiscalYear.Id, cap.PostingDate, targetAsset.TenantId)
        {
            EntryNumber = jeNumber,
            ReferenceType = "AssetCapitalization",
            ReferenceId = cap.Id,
            Narration = $"Asset capitalization ({cap.CapitalizationNumber}) into {targetAsset.AssetName}",
        };

        je.AddLine(targetAccounts.FixedAssetAccountId, trueTotal, isDebit: true,
            description: "Asset capitalization — target asset");
        foreach (var (accountId, amount, description) in extraDebitLines)
            je.AddLine(accountId, amount, isDebit: true, description: description);
        foreach (var (accountId, amount, description) in creditLines)
            je.AddLine(accountId, amount, isDebit: false, description: description);

        je.Validate();
        je.Post();
        await _journalEntryRepository.InsertAsync(je);

        return (je.Id, trueTotal);
    }

    /// <summary>
    /// Restores each stock item's quantity on cancel — a compensating inward StockLedgerEntry per
    /// item, mirroring the outward one PostAsync created. Without this, cancelling a submitted
    /// capitalization would reverse GL and the target asset's value but leave the consumed stock
    /// permanently gone, trading the original phantom-stock bug for the opposite one.
    /// </summary>
    public async Task ReverseStockAsync(AssetCapitalization cap, Guid? tenantId)
    {
        foreach (var item in cap.StockItems)
        {
            if (!item.WarehouseId.HasValue)
                continue;

            await _valuationService.CreateLedgerEntryAsync(
                cap.CompanyId, item.ItemId, item.WarehouseId.Value, DateTime.UtcNow,
                item.Qty, item.Rate, "AssetCapitalization", cap.Id, tenantId);
        }
    }
}
