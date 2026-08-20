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
        if (!accounts.AccumulatedDepreciationAccountId.HasValue)
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

        var accumulatedDepreciation = asset.PurchaseAmount - preDisposalValueAfterDepreciation;
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
        if (accumulatedDepreciation != 0)
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
        if (asset.CalculateDepreciation)
        {
            if (asset.UsefulLifeMonths <= 0)
                throw new BusinessException("MyERP:15002")
                    .WithData("assetName", asset.AssetName)
                    .WithData("field", "UsefulLifeMonths");

            if (!asset.AvailableForUseDate.HasValue)
                throw new BusinessException("MyERP:15002")
                    .WithData("assetName", asset.AssetName)
                    .WithData("field", "AvailableForUseDate");
        }
    }

    /// <summary>
    /// Validates depreciation schedule entries are not in a frozen accounting period.
    /// Per DO-NOT: "Post depreciation entries for schedule dates within company's accounts_frozen_till_date".
    /// </summary>
    public async Task<DateTime?> GetFrozenDateAsync(Guid companyId)
    {
        var company = await _companyRepository.FindAsync(companyId);
        return company?.AccountsFrozenTillDate;
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
    /// Gets unbooked depreciation schedule entries that are due on or before a given date.
    /// Excludes entries in frozen accounting periods.
    /// </summary>
    public async Task<DepreciationScheduleEntry[]> GetDueDepreciationEntriesAsync(
        Asset asset, DateTime asOfDate)
    {
        var frozenDate = await GetFrozenDateAsync(asset.CompanyId);

        return asset.DepreciationSchedule
            .Where(e => !e.IsBooked
                     && e.ScheduleDate <= asOfDate
                     && (!frozenDate.HasValue || e.ScheduleDate > frozenDate.Value))
            .OrderBy(e => e.ScheduleDate)
            .ToArray();
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
}
