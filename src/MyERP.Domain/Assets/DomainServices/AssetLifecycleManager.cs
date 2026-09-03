using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Assets.DomainServices;

/// <summary>
/// Domain service for Asset lifecycle rules.
/// Handles depreciation validation, disposal GL calculations, and repair cost capitalization.
/// Per ERPNext: assets/doctype/asset + asset_depreciation.instructions.md.
/// </summary>
public class AssetLifecycleManager : DomainService
{
    private readonly IRepository<Asset, Guid> _assetRepository;
    private readonly IRepository<AssetCategory, Guid> _categoryRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public AssetLifecycleManager(
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetCategory, Guid> categoryRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _assetRepository = assetRepository;
        _categoryRepository = categoryRepository;
        _companyRepository = companyRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _journalEntryRepository = journalEntryRepository;
        _numberGenerator = numberGenerator;
    }

    /// <summary>
    /// Calculates gain or loss on asset disposal.
    /// Per ERPNext: gain/loss = disposal_amount - value_after_depreciation_on_disposal_date.
    /// Positive = gain (profit), negative = loss (write-off).
    /// Takes disposalDate/disposalAmount explicitly rather than reading them off the asset —
    /// callers invoke this before Asset.Sell()/Scrap() sets those fields, so asset.DisposalAmount
    /// is still null at call time (a pre-existing bug: it silently evaluated as disposalAmount=0
    /// every time, found while touching this method for the book-value-simulation fix below).
    /// Also uses Asset.SimulateBookValueAtDate instead of the last-booked ValueAfterDepreciation,
    /// which can be stale by up to a full depreciation period if disposal happens between
    /// scheduler runs.
    /// </summary>
    public decimal CalculateDisposalGainLoss(Asset asset, DateTime disposalDate, decimal disposalAmount)
    {
        return disposalAmount - asset.SimulateBookValueAtDate(disposalDate);
    }

    /// <summary>
    /// Posts the GL entry removing a disposed (sold or scrapped) asset from the books.
    /// Per ERPNext get_gl_entries_on_asset_disposal: CR Fixed Asset for the full original cost,
    /// DR Accumulated Depreciation for depreciation booked to date, DR/CR Disposal Account for
    /// the gain/loss (disposalAmount - book value), plus DR the settlement account for any real
    /// proceeds received (0 for scrap). Call AFTER Asset.Sell()/Scrap() has set DisposalAmount.
    /// Must be called before the asset entity is saved so the pre-disposal ValueAfterDepreciation
    /// is still available for the accumulated-depreciation calculation.
    /// </summary>
    /// <param name="settlementAccountId">
    /// Account receiving the disposal proceeds (cash/bank/debtor). Required whenever
    /// disposalAmount > 0 — there is no invoice booking that leg for a standalone disposal,
    /// so without it the entry cannot balance. Pass null for a pure scrap (disposalAmount = 0).
    /// </param>
    public async Task<Guid> PostDisposalJournalEntryAsync(
        Asset asset, decimal disposalAmount, decimal preDisposalValueAfterDepreciation, Guid? settlementAccountId)
    {
        if (disposalAmount > 0 && !settlementAccountId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.AssetDisposalAccountMissing)
                .WithData("assetName", asset.AssetName)
                .WithData("accountField", "SettlementAccountId");

        var category = asset.AssetCategoryId.HasValue
            ? await _categoryRepository.FindAsync(asset.AssetCategoryId.Value)
            : null;
        var accounts = category?.GetAccountForCompany(asset.CompanyId);
        if (accounts == null)
            throw new BusinessException(MyERPDomainErrorCodes.AssetDisposalAccountMissing)
                .WithData("assetName", asset.AssetName)
                .WithData("accountField", "AssetCategoryAccount");
        var accumulatedDepreciation = asset.PurchaseAmount - preDisposalValueAfterDepreciation;
        // Per ERPNext PR #47427 / commit 51ea33e743:
        // Do not mandate depreciation account for assets without depreciation.
        if (accumulatedDepreciation != 0 && !accounts.AccumulatedDepreciationAccountId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.AssetDisposalAccountMissing)
                .WithData("assetName", asset.AssetName)
                .WithData("accountField", "AccumulatedDepreciationAccountId");

        var company = await _companyRepository.GetAsync(asset.CompanyId);
        var profitAmount = disposalAmount - preDisposalValueAfterDepreciation;
        if (profitAmount != 0 && !company.DisposalAccountId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.AssetDisposalAccountMissing)
                .WithData("assetName", asset.AssetName)
                .WithData("accountField", "Company.DisposalAccountId");

        var fiscalYear = (await _fiscalYearRepository.GetQueryableAsync())
            .FirstOrDefault(fy => fy.CompanyId == asset.CompanyId
                && fy.StartDate <= (asset.DisposalDate ?? DateTime.UtcNow)
                && fy.EndDate >= (asset.DisposalDate ?? DateTime.UtcNow));
        if (fiscalYear == null)
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("postingDate", (asset.DisposalDate ?? DateTime.UtcNow).ToString("yyyy-MM-dd"));
        var jeNumber = await _numberGenerator.GenerateAsync("JE", asset.CompanyId);
        var je = new JournalEntry(GuidGenerator.Create(), asset.CompanyId, fiscalYear.Id,
            asset.DisposalDate ?? DateTime.UtcNow, asset.TenantId)
        {
            EntryNumber = jeNumber,
            ReferenceType = "Asset",
            ReferenceId = asset.Id,
            Narration = $"Asset disposal ({asset.AssetNumber})",
        };

        je.AddLine(accounts.FixedAssetAccountId, asset.PurchaseAmount, isDebit: false,
            description: "Asset disposal — remove original cost");
        if (accumulatedDepreciation != 0 && accounts.AccumulatedDepreciationAccountId.HasValue)
            je.AddLine(accounts.AccumulatedDepreciationAccountId.Value, accumulatedDepreciation, isDebit: true,
                description: "Asset disposal — clear accumulated depreciation");
        if (disposalAmount != 0)
            je.AddLine(settlementAccountId!.Value, disposalAmount, isDebit: true,
                description: "Asset disposal — proceeds received");
        if (profitAmount > 0)
            je.AddLine(company.DisposalAccountId!.Value, profitAmount, isDebit: false,
                description: "Asset disposal — gain");
        else if (profitAmount < 0)
            je.AddLine(company.DisposalAccountId!.Value, -profitAmount, isDebit: true,
                description: "Asset disposal — loss");

        je.Validate();
        je.Post();
        await _journalEntryRepository.InsertAsync(je);
        return je.Id;
    }

    /// <summary>
    /// Validates that an asset can be submitted for depreciation.
    /// Checks: has depreciation settings, has category, available-for-use date is set.
    /// </summary>
    public void ValidateForSubmission(Asset asset)
    {
        // Per ERPNext commit 0f5be4b245: composite component assets cannot calculate depreciation
        // and do not require AvailableForUseDate.
        if (asset.IsCompositeComponent)
        {
            if (asset.CalculateDepreciation)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Composite component asset cannot calculate depreciation.");
            return;
        }

        if (asset.CalculateDepreciation)
        {
            if (asset.UsefulLifeMonths <= 0)
                throw new BusinessException(MyERPDomainErrorCodes.AssetMissingRequiredField)
                    .WithData("assetName", asset.AssetName)
                    .WithData("fieldName", "UsefulLifeMonths");

            if (!asset.AvailableForUseDate.HasValue)
                throw new BusinessException(MyERPDomainErrorCodes.AssetMissingRequiredField)
                    .WithData("assetName", asset.AssetName)
                    .WithData("fieldName", "AvailableForUseDate");
        }
    }

    /// <summary>
    /// Resolves GL accounts for depreciation journal entries.
    /// Priority: AssetCategory → Company defaults.
    /// Returns (depreciationExpenseAccountId, accumulatedDepreciationAccountId).
    /// </summary>
    public async Task<(Guid? DepreciationExpenseAccount, Guid? AccumulatedDepreciationAccount)>
        ResolveDepreciationAccountsAsync(Guid? categoryId, Guid companyId)
    {
        Guid? depExpenseAccount = null;
        Guid? accDepAccount = null;

        // Try category first
        if (categoryId.HasValue)
        {
            var category = await _categoryRepository.FindAsync(categoryId.Value);
            if (category != null)
            {
                depExpenseAccount = category.DepreciationAccountId;
                accDepAccount = category.AccumulatedDepreciationAccountId;
            }
        }

        // Fall back to company defaults
        if (!depExpenseAccount.HasValue || !accDepAccount.HasValue)
        {
            var company = await _companyRepository.FindAsync(companyId);
            if (company != null)
            {
                depExpenseAccount ??= company.DepreciationExpenseAccountId;
                accDepAccount ??= company.AccumulatedDepreciationAccountId;
            }
        }

        return (depExpenseAccount, accDepAccount);
    }

    /// <summary>
    /// Validates asset repair capitalization rules.
    /// Per gotcha #35: fully depreciated assets CAN be repaired but
    /// capitalize_repair_cost and increase_in_asset_life are forced to 0.
    /// </summary>
    public (bool CanCapitalize, bool CanExtendLife) GetRepairOptions(Asset asset)
    {
        if (asset.IsFullyDepreciated || asset.Status == AssetStatus.FullyDepreciated)
        {
            // Fully depreciated: repair tracked but no accounting/schedule impact
            return (false, false);
        }

        return (true, true);
    }

    /// <summary>
    /// Calculates net purchase amount for asset creation from purchase document item.
    /// Per ERPNext commit 46e01c2d92 / PR #57618:
    /// Uses ValuationRate * Quantity so capitalized landed costs are included in the asset purchase amount.
    /// </summary>
    public decimal CalculateAssetPurchaseAmount(decimal valuationRate, decimal qty)
    {
        return valuationRate * qty;
    }

    /// <summary>
    /// Splits an asset with quantity > 1 into two separate assets with proportional cost scaling.
    /// Per ERPNext: split_asset() in assets/doctype/asset/mapper.py (Gotcha #969).
    /// </summary>
    public async Task<Asset> SplitAssetAsync(Guid assetId, int splitQty)
    {
        if (splitQty <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Split quantity must be greater than zero.");

        var existingAsset = await _assetRepository.GetAsync(assetId, includeDetails: true);

        if (splitQty >= existingAsset.AssetQuantity)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Split Quantity must be less than Asset Quantity.");

        if (existingAsset.Status is AssetStatus.Draft or AssetStatus.Cancelled or AssetStatus.Scrapped or AssetStatus.Sold)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("from", existingAsset.Status.ToString())
                .WithData("detail", "Only active submitted assets can be split.");

        var totalQty = existingAsset.AssetQuantity;
        var remainingQty = totalQty - splitQty;
        var splitScale = (decimal)splitQty / totalQty;
        var remainingScale = (decimal)remainingQty / totalQty;

        // Generate new asset number
        var newAssetNumber = await _numberGenerator.GenerateAsync("Asset", existingAsset.CompanyId);

        // Create split asset
        var newAsset = new Asset(
            Guid.NewGuid(),
            existingAsset.CompanyId,
            newAssetNumber,
            $"{existingAsset.AssetName} (Split)",
            existingAsset.PurchaseDate,
            existingAsset.PurchaseAmount,
            existingAsset.TenantId)
        {
            AssetCategoryId = existingAsset.AssetCategoryId,
            ItemId = existingAsset.ItemId,
            Location = existingAsset.Location,
            LocationId = existingAsset.LocationId,
            CustodianEmployeeId = existingAsset.CustodianEmployeeId,
            CalculateDepreciation = existingAsset.CalculateDepreciation,
            DepreciationMethod = existingAsset.DepreciationMethod,
            UsefulLifeMonths = existingAsset.UsefulLifeMonths,
            DepreciationRate = existingAsset.DepreciationRate,
            FrequencyMonths = existingAsset.FrequencyMonths,
            AvailableForUseDate = existingAsset.AvailableForUseDate,
            ExpectedValueAfterUsefulLife = existingAsset.ExpectedValueAfterUsefulLife,
            Notes = $"Split from {existingAsset.AssetNumber}",
            SplitFromAssetId = existingAsset.Id
        };

        // Copy depreciation details / finance books
        foreach (var d in existingAsset.DepreciationDetails)
        {
            var newDetail = new AssetDepreciationDetail(
                Guid.NewGuid(),
                newAsset.Id,
                d.DepreciationMethod,
                d.TotalNumberOfDepreciations,
                d.FrequencyOfDepreciation,
                d.NetPurchaseAmount)
            {
                FinanceBookId = d.FinanceBookId,
                Rate = d.Rate,
                OpeningAccumulatedDepreciation = d.OpeningAccumulatedDepreciation,
                ValueAfterDepreciation = d.ValueAfterDepreciation,
                ExpectedValueAfterUsefulLife = d.ExpectedValueAfterUsefulLife,
                DepreciationStartDate = d.DepreciationStartDate
            };
            newAsset.DepreciationDetails.Add(newDetail);
        }

        newAsset.Submit();
        newAsset.ApplySplitScale(splitScale, splitQty);

        // Scale existing asset
        existingAsset.ApplySplitScale(remainingScale, remainingQty);

        await _assetRepository.InsertAsync(newAsset);
        await _assetRepository.UpdateAsync(existingAsset);

        return newAsset;
    }
}
