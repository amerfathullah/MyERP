using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Settings;
using MyERP.Tax.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;

namespace MyERP.Tax.DomainServices;

/// <summary>
/// Centralized tax recalculation service extracted from inline AppService logic.
/// All invoice/order AppService SubmitAsync methods delegate here instead of
/// repeating the same TaxesAndTotalsService.Calculate() pattern.
/// Per ERPNext: server-side authoritative tax calculation on submit.
/// </summary>
public class TransactionTaxRecalculationService : DomainService
{
    private readonly TaxesAndTotalsService _taxService;
    private readonly IRepository<TransactionTaxRow, Guid> _taxRowRepo;
    private readonly ISettingProvider _settingProvider;

    public TransactionTaxRecalculationService(
        TaxesAndTotalsService taxService,
        IRepository<TransactionTaxRow, Guid> taxRowRepo,
        ISettingProvider settingProvider)
    {
        _taxService = taxService;
        _taxRowRepo = taxRowRepo;
        _settingProvider = settingProvider;
    }

    /// <summary>
    /// Recalculates taxes and totals for a document on submit.
    /// Returns the recalculated totals for the caller to apply to the entity.
    /// </summary>
    public async Task<RecalculatedTotals> RecalculateAsync(TaxRecalculationInput input)
    {
        // Fetch applicable tax rows for this document
        var taxRowQuery = await _taxRowRepo.GetQueryableAsync();
        var taxRows = taxRowQuery
            .Where(t => t.ParentType == input.DocumentType && t.ParentId == input.DocumentId)
            .OrderBy(t => t.RowIndex)
            .ToList();

        // Fallback cost center on tax rows if not set (ERPNext PR #49718 / commit b75940bf0e)
        if (input.CostCenterId.HasValue)
        {
            foreach (var taxRow in taxRows)
            {
                if (!taxRow.CostCenterId.HasValue)
                {
                    taxRow.CostCenterId = input.CostCenterId;
                }
            }
        }

        if (taxRows.Count == 0 && input.Items.Count > 0)
        {
            // No tax rows — totals are just net amounts
            var netTotal = input.Items.Sum(i => i.Quantity * i.UnitPrice);
            return new RecalculatedTotals
            {
                NetTotal = netTotal,
                TaxAmount = 0,
                GrandTotal = netTotal - input.DiscountAmount,
                BaseNetTotal = netTotal * input.ExchangeRate,
                BaseTaxAmount = 0,
                BaseGrandTotal = (netTotal - input.DiscountAmount) * input.ExchangeRate,
            };
        }

        // Build transaction items for the calculator
        var transactionItems = input.Items.Select(i => new TransactionItem
        {
            ItemId = i.ItemId,
            Qty = i.Quantity,
            Rate = i.UnitPrice,
            NetAmount = i.Quantity * i.UnitPrice,
        }).ToList();

        // Run the tax cascade
        var roundRowWiseTax = await _settingProvider.IsTrueAsync(MyERPSettings.Accounts.RoundRowWiseTax);
        var calcResult = _taxService.Calculate(
            transactionItems,
            taxRows,
            input.ExchangeRate,
            input.DiscountAmount,
            input.IsDiscountOnGrandTotal ? "Grand Total" : "Net Total",
            roundRowWiseTax: roundRowWiseTax);

        return new RecalculatedTotals
        {
            NetTotal = calcResult.NetTotal,
            TaxAmount = calcResult.TotalTax,
            GrandTotal = calcResult.GrandTotal,
            BaseNetTotal = calcResult.BaseNetTotal,
            BaseTaxAmount = calcResult.BaseTotalTax,
            BaseGrandTotal = calcResult.BaseGrandTotal,
        };
    }

    /// <summary>
    /// Quick net total calculation without tax cascade (for documents without tax rows).
    /// </summary>
    public static RecalculatedTotals CalculateNetOnly(
        IReadOnlyList<TaxItemInput> items,
        decimal discountAmount,
        decimal exchangeRate)
    {
        var netTotal = items.Sum(i => i.Quantity * i.UnitPrice);
        var grandTotal = netTotal - discountAmount;

        return new RecalculatedTotals
        {
            NetTotal = netTotal,
            TaxAmount = 0,
            GrandTotal = grandTotal,
            BaseNetTotal = netTotal * exchangeRate,
            BaseTaxAmount = 0,
            BaseGrandTotal = grandTotal * exchangeRate,
        };
    }
}

// --- Supporting types ---

public class TaxRecalculationInput
{
    public string DocumentType { get; set; } = null!;
    public Guid DocumentId { get; set; }
    public Guid? CostCenterId { get; set; }
    public IReadOnlyList<TaxItemInput> Items { get; set; } = Array.Empty<TaxItemInput>();
    public decimal ExchangeRate { get; set; } = 1;
    public decimal DiscountAmount { get; set; }
    public bool IsDiscountOnGrandTotal { get; set; } = true;
}

public class TaxItemInput
{
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class RecalculatedTotals
{
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal BaseNetTotal { get; set; }
    public decimal BaseTaxAmount { get; set; }
    public decimal BaseGrandTotal { get; set; }
}
