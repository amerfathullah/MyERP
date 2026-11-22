using System;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Handles "Expenses Added To Stock" GL entries — creates a DR/CR pair that
/// captures additional purchase expenses (freight, duty, etc.) into stock valuation.
/// 
/// Per ERPNext PR #57190: requires both Accounts Settings flag AND company account configured.
/// Per ERPNext PR #57519: MUST skip non-stock items (is_stock_item guard).
/// 
/// GL Entry pair:
///   DR: Expenses Added To Stock Account (expense → stock absorption)
///   CR: Expenses Added To Stock Contra Account (stock → expense reversal)
/// 
/// Only fires for Purchase Receipt and Subcontracting Receipt GL posting.
/// Resolution chain (per gotcha #4958):
///   Item.ExpensesAddedToStockAccount → ItemGroup → Brand → Company
/// </summary>
public class StockExpenseGlService : DomainService
{
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public StockExpenseGlService(
        IRepository<Company, Guid> companyRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _companyRepository = companyRepository;
        _itemRepository = itemRepository;
    }

    /// <summary>
    /// Determines whether stock expense GL entries should be generated for an item.
    /// Two-level gate: 
    ///   1. Accounts Settings.BookStockExpenseGlEntries must be enabled
    ///   2. Item must be a stock item (MaintainStock = true)
    /// Per PR #57519: explicitly skips non-stock items to prevent incorrect GL entries.
    /// </summary>
    public async Task<bool> ShouldPostStockExpenseAsync(Guid itemId, Guid companyId, bool settingEnabled)
    {
        // Gate 1: Global setting check
        if (!settingEnabled) return false;

        // Gate 2: Item must be a stock item (PR #57519 fix)
        var item = await _itemRepository.FindAsync(itemId);
        if (item == null || !item.MaintainStock) return false;

        // Gate 3: Company must have the account configured
        var company = await _companyRepository.GetAsync(companyId);
        if (!company.ExpensesAddedToStockAccountId.HasValue) return false;

        return true;
    }

    /// <summary>
    /// Resolves the Expenses Added To Stock account for an item.
    /// 4-level resolution chain: Item → ItemGroup → Brand → Company default.
    /// </summary>
    public async Task<Guid?> ResolveStockExpenseAccountAsync(Guid itemId, Guid companyId)
    {
        // For now, use company-level default (full 4-level resolution can be added later)
        var company = await _companyRepository.GetAsync(companyId);
        return company.ExpensesAddedToStockAccountId;
    }

    /// <summary>
    /// Resolves the contra account for Expenses Added To Stock entries.
    /// </summary>
    public async Task<Guid?> ResolveStockExpenseContraAccountAsync(Guid companyId)
    {
        var company = await _companyRepository.GetAsync(companyId);
        return company.ExpensesAddedToStockContraAccountId;
    }

    /// <summary>
    /// Appends a balanced "Expenses Added To Stock" GL pair onto a draft JournalEntry.
    /// Per ERPNext PR #57190 &amp; PR #57519 (base_stock_gl_composer.append_expenses_added_to_stock_pair):
    /// - Skips zero amounts and non-stock items (<see cref="Item.MaintainStock"/> == false) without requiring accounts.
    /// - Requires both <see cref="Company.ExpensesAddedToStockAccountId"/> and <see cref="Company.ExpensesAddedToStockContraAccountId"/>.
    /// - Positive amount: DR ExpensesAddedToStock, CR Contra. Negative amount: reversed direction.
    /// </summary>
    public async Task<bool> AppendExpensesAddedToStockPairAsync(
        JournalEntry journal,
        Company company,
        Guid itemId,
        decimal amount,
        Guid? costCenterId = null,
        Guid? projectId = null)
    {
        var rounded = Math.Round(amount, 2);
        if (rounded == 0m) return false;

        var item = await _itemRepository.FindAsync(itemId);
        if (item == null || !item.MaintainStock) return false;

        if (!company.ExpensesAddedToStockAccountId.HasValue || !company.ExpensesAddedToStockContraAccountId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DefaultAccountNotConfigured)
                .WithData("reason", $"Please set Expenses Added To Stock Account and Contra Account in Company '{company.Name}'.");
        }

        var effectiveCostCenterId = costCenterId ?? company.DefaultCostCenterId;
        var absAmount = Math.Abs(rounded);
        var isPositive = rounded > 0m;
        var remarks = $"Expenses Added To Stock for Item {item.ItemCode}";

        journal.AddLine(
            company.ExpensesAddedToStockAccountId.Value,
            absAmount,
            isDebit: isPositive,
            description: remarks,
            costCenterId: effectiveCostCenterId,
            projectId: projectId);

        journal.AddLine(
            company.ExpensesAddedToStockContraAccountId.Value,
            absAmount,
            isDebit: !isPositive,
            description: remarks,
            costCenterId: effectiveCostCenterId,
            projectId: projectId);

        return true;
    }
}
