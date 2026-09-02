using System;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Represents a legal company entity. A tenant can have multiple companies.
/// Maps conceptually to ERPNext setup/doctype/company.
/// </summary>
public class Company : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; private set; } = null!;
    public string? ShortName { get; set; }

    /// <summary>Tax Identification Number (TIN) — required for LHDN e-Invoice.</summary>
    public string? TaxId { get; set; }

    /// <summary>Company registration number (e.g., SSM in Malaysia).</summary>
    public string? RegistrationNumber { get; set; }

    /// <summary>SST registration number for Royal Malaysian Customs.</summary>
    public string? SstRegistrationNumber { get; set; }

    /// <summary>MSIC code for e-Invoice classification.</summary>
    public string? MsicCode { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    // Address
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    /// <summary>Default currency code (ISO 4217), e.g. "MYR".</summary>
    public string CurrencyCode { get; set; } = "MYR";

    /// <summary>
    /// Default sales contact person for the company (PR #50159 / commit 4d9473f844).
    /// Auto-fetched into sales transactions (Sales Order, Sales Invoice, Quotation, Delivery Note).
    /// </summary>
    public string? DefaultSalesContact { get; set; }

    /// <summary>Fiscal year start month (1-12).</summary>
    public int FiscalYearStartMonth { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    /// <summary>Per MyInvois PR d9adf36: Enable or disable LHDN submission for this company.</summary>
    public bool EnableLhdnInvoice { get; set; } = false;
    /// <summary>Stock transactions before this date are frozen (blocked for non-admin users).</summary>
    public DateTime? StockFrozenUpto { get; set; }

    /// <summary>Alternative to StockFrozenUpto: freeze stock N days before today.</summary>
    public int StockFrozenUptoDays { get; set; }

    /// <summary>Role that can bypass the stock freeze (post to frozen periods).</summary>
    public string? StockAuthRole { get; set; }

    /// <summary>Accounting entries before this date are frozen.</summary>
    public DateTime? AccountsFrozenTillDate { get; set; }

    // Default Accounts (per ERPNext Company.set_default_accounts pattern)
    /// <summary>Default Accounts Receivable account for this company.</summary>
    public Guid? DefaultReceivableAccountId { get; set; }
    /// <summary>Default Accounts Payable account for this company.</summary>
    public Guid? DefaultPayableAccountId { get; set; }
    /// <summary>Default Income/Revenue account.</summary>
    public Guid? DefaultIncomeAccountId { get; set; }
    /// <summary>Default Expense account (COGS).</summary>
    public Guid? DefaultExpenseAccountId { get; set; }
    /// <summary>Default expense account for non-stock / service items. Per ERPNext PR #50007 / commit 4605051903.</summary>
    public Guid? ServiceExpenseAccountId { get; set; }
    /// <summary>Default Tax Payable account (e.g. SST Payable) for output tax collected on sales.</summary>
    public Guid? DefaultTaxPayableAccountId { get; set; }
    /// <summary>Default Bank account.</summary>
    public Guid? DefaultBankAccountId { get; set; }
    /// <summary>Default Stock/Inventory account (perpetual inventory).</summary>
    public Guid? DefaultInventoryAccountId { get; set; }
    /// <summary>
    /// Balancing account for stock movements with no natural other side — a standalone Material
    /// Receipt (stock appearing with no PO/PI reference) or a Disassemble entry's valuation
    /// residual. Per ERPNext: typically the seeded "Temporary Opening" equity account.
    /// </summary>
    public Guid? DefaultStockAdjustmentAccountId { get; set; }
    /// <summary>
    /// Asset account holding the value of raw materials recorded as consumed (via a Material
    /// Consumption for Manufacture stock entry) before the corresponding Manufacture entry
    /// completes the finished good — a reclassification out of raw material stock, not an expense.
    /// </summary>
    public Guid? DefaultWipAccountId { get; set; }
    /// <summary>Default depreciation expense account.</summary>
    public Guid? DepreciationExpenseAccountId { get; set; }
    /// <summary>Default accumulated depreciation account.</summary>
    public Guid? AccumulatedDepreciationAccountId { get; set; }
    /// <summary>Exchange gain/loss account (multi-currency fallback).</summary>
    public Guid? ExchangeGainLossAccountId { get; set; }
    /// <summary>Specific account for booking exchange gains. Falls back to ExchangeGainLossAccountId. Per ERPNext PR #57839.</summary>
    public Guid? ExchangeGainAccountId { get; set; }
    /// <summary>Specific account for booking exchange losses. Falls back to ExchangeGainLossAccountId. Per ERPNext PR #57839.</summary>
    public Guid? ExchangeLossAccountId { get; set; }
    /// <summary>Default Bank Charges account for payment deductions (single-currency transfers). Per ERPNext PR #57840.</summary>
    public Guid? BankChargesAccountId { get; set; }
    /// <summary>Gain/loss on fixed asset disposal (sale or scrap). Per ERPNext: Company.disposal_account.</summary>
    public Guid? DisposalAccountId { get; set; }
    /// <summary>
    /// Default Cost Center — fallback applied to a GL line on a Revenue/Expense account that has
    /// no cost center of its own. Per ERPNext: Company.cost_center (every company auto-creates a
    /// root cost center and links it here). Required for P&amp;L GL lines; see
    /// AccountingDimensionService.ValidatePlAccountsHaveCostCenterAsync.
    /// </summary>
    public Guid? DefaultCostCenterId { get; set; }

    // --- Advance Payment Settings (per ERPNext gotcha #205) ---

    /// <summary>
    /// When true, advance payments are routed to a separate liability/receivable account
    /// instead of the regular party account. Enables advance tracking before invoicing.
    /// Per ERPNext: Company.book_advance_payments_in_separate_party_account.
    /// </summary>
    public bool BookAdvancePaymentsInSeparatePartyAccount { get; set; }

    /// <summary>Default Advance Received account (Customer advances → Liability).</summary>
    public Guid? DefaultAdvanceReceivedAccountId { get; set; }

    /// <summary>Default Advance Paid account (Supplier advances → Asset).</summary>
    public Guid? DefaultAdvancePaidAccountId { get; set; }

    // --- Additional Settings ---

    /// <summary>Enable perpetual inventory (stock movements create GL entries automatically).</summary>
    public bool EnablePerpetualInventory { get; set; } = true;

    /// <summary>
    /// Default valuation method for new Items created under this company, per
    /// stock-ledger-engine's documented fallback chain (Item override → Company default →
    /// global StockSettings default → FIFO). Null = fall through to the next tier.
    /// </summary>
    public MyERP.Inventory.ValuationMethod? DefaultValuationMethod { get; set; }

    /// <summary>
    /// Round-off account for opening balance entries.
    /// Per ERPNext gotcha #200: opening entries use a DIFFERENT round-off account.
    /// </summary>
    public Guid? RoundOffForOpeningAccountId { get; set; }

    /// <summary>Default round-off account (for non-opening transaction rounding).</summary>
    public Guid? RoundOffAccountId { get; set; }

    /// <summary>Stock Received But Not Billed (GRNI) account for perpetual inventory.</summary>
    public Guid? StockReceivedButNotBilledAccountId { get; set; }

    /// <summary>Stock Delivered But Not Billed (SDBNB) account for perpetual inventory.</summary>
    public Guid? StockDeliveredButNotBilledAccountId { get; set; }

    /// <summary>
    /// Expenses Added To Stock account — captures additional purchase costs into stock valuation.
    /// Per PR #57190: two-level gate (Accounts Settings + this account must be configured).
    /// 4-level resolution chain: Item → ItemGroup → Brand → Company.
    /// </summary>
    public Guid? ExpensesAddedToStockAccountId { get; set; }

    /// <summary>Contra account for Expenses Added To Stock entries.</summary>
    public Guid? ExpensesAddedToStockContraAccountId { get; set; }

    /// <summary>Reporting currency for financial reports (default: same as CurrencyCode).</summary>
    public string? ReportingCurrency { get; set; }

    /// <summary>Auto-exchange rate revaluation: enables automated period-end revaluation.</summary>
    public bool AutoExchangeRateRevaluation { get; set; }

    /// <summary>Over-delivery/receipt allowance percentage (0 = exact match required).</summary>
    public decimal OverDeliveryReceiptAllowance { get; set; }

    /// <summary>Over-billing allowance percentage.</summary>
    public decimal OverBillingAllowance { get; set; }

    /// <summary>
    /// Enable Proforma Invoice creation from Sales Orders (v16 feature, PR #57263).
    /// Per gotcha #2454: gated by Selling Settings.enable_proforma_invoice.
    /// </summary>
    public bool EnableProformaInvoice { get; set; }

    /// <summary>
    /// When true, only UOMs defined in the Item UOM conversion child table are allowed in transactions.
    /// Per ERPNext: allow_uom_with_conversion_rate_defined_in_item (gotcha #6077).
    /// </summary>
    public bool AllowUomWithConversionRateDefinedInItem { get; set; }

    /// <summary>
    /// When true, allows LHDN Credit Note / Debit Note submission without an original invoice in the system (e.g. historical invoices).
    /// Migrated from myinvois custom_allow_creditnote_without_original_invoice_in_the_system.
    /// </summary>
    public bool AllowCreditNoteWithoutOriginalInvoice { get; set; }

    /// <summary>
    /// When true, allows booking inventory GL entries to item/item-group specific inventory accounts instead of warehouse accounts.
    /// Per ERPNext PR #50193 / commit 74192547ce.
    /// </summary>
    public bool EnableItemWiseInventoryAccount { get; set; }

    // --- Warehouse Defaults (moved from Stock Settings to Company per PR #57571) ---

    /// <summary>Default warehouse for transactions. Per PR #57571: now per-company, not global.</summary>
    public Guid? DefaultWarehouseId { get; set; }

    /// <summary>Sample retention warehouse for QC samples. Per PR #57571: now per-company.</summary>
    public Guid? SampleRetentionWarehouseId { get; set; }

    /// <summary>Default in-transit warehouse for stock transfers. Per PR #57571.</summary>
    public Guid? DefaultInTransitWarehouseId { get; set; }

    /// <summary>Default warehouse for sales returns. Per PR #57571.</summary>
    public Guid? DefaultWarehouseForSalesReturnId { get; set; }

    /// <summary>Default WIP warehouse for manufacturing operations.</summary>
    public Guid? DefaultWipWarehouseId { get; set; }

    /// <summary>Default Finished Goods warehouse for manufacturing output.</summary>
    public Guid? DefaultFgWarehouseId { get; set; }

    /// <summary>Default Scrap/Secondary Items warehouse for manufacturing waste.</summary>
    public Guid? DefaultScrapWarehouseId { get; set; }

    protected Company() { } // EF Core constructor

    public Company(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        SetName(name);
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), CompanyConsts.MaxNameLength);
    }

    /// <summary>
    /// Sets the default currency. Must be called BEFORE any transactions are submitted.
    /// Per DO-NOT: "Change company default_currency after submitted transactions exist (breaks multi-currency)"
    /// The AppService must validate that no submitted transactions exist before calling this.
    /// </summary>
    public void SetCurrency(string currencyCode, bool hasSubmittedTransactions)
    {
        if (hasSubmittedTransactions && currencyCode != CurrencyCode)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CompanyCurrencyLocked)
                .WithData("company", Name)
                .WithData("currentCurrency", CurrencyCode)
                .WithData("attemptedCurrency", currencyCode);
        }
        CurrencyCode = Check.NotNullOrWhiteSpace(currencyCode, nameof(currencyCode), 3);
    }

    /// <summary>
    /// Resolves the appropriate account for exchange gain or loss. Per ERPNext PR #57839.
    /// </summary>
    public Guid? GetExchangeGainLossAccountId(bool isGain)
    {
        return isGain
            ? (ExchangeGainAccountId ?? ExchangeGainLossAccountId)
            : (ExchangeLossAccountId ?? ExchangeGainLossAccountId);
    }
}
