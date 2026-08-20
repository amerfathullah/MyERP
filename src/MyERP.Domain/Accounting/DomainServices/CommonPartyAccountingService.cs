using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Auto-reconciles a Sales/Purchase Invoice's outstanding balance when its party (Customer/Supplier)
/// is linked via <see cref="PartyLink"/> to the same real-world entity under its other role — e.g. a
/// vendor who is also a customer. Per returns-inter-company skill, "Common Party Accounting":
/// gated by <see cref="MyERPSettings.Accounts.EnableCommonPartyAccounting"/>; posts a 2-line JE
/// (reconciliation line against the invoice + advance line against the primary party's own account)
/// and mirrors it into the payment ledger, the same double-bookkeeping shape every other posting
/// path in this codebase uses (GL + PLE together).
///
/// Per-party currency has no field anywhere in this codebase (only the document and company carry a
/// currency code) — the primary party's account is assumed to transact in the company's own default
/// currency. This is a deliberate, documented simplification: a real per-party currency model would
/// need its own migration and is out of scope for wiring up this feature for the first time.
/// </summary>
public class CommonPartyAccountingService : DomainService
{
    private readonly ISettingProvider _settingProvider;
    private readonly IRepository<PartyLink, Guid> _partyLinkRepository;
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly PaymentLedgerService _pleService;

    public CommonPartyAccountingService(
        ISettingProvider settingProvider,
        IRepository<PartyLink, Guid> partyLinkRepository,
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Company, Guid> companyRepository,
        PaymentLedgerService pleService)
    {
        _settingProvider = settingProvider;
        _partyLinkRepository = partyLinkRepository;
        _journalRepository = journalRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _customerRepository = customerRepository;
        _supplierRepository = supplierRepository;
        _companyRepository = companyRepository;
        _pleService = pleService;
    }

    /// <summary>
    /// Looks up a Party Link for the document's party and, if found and the setting is enabled,
    /// posts the reconciliation JE + PLE row. Returns null (no-op) when the setting is off, there's
    /// no outstanding, no link exists, or no fiscal year is open for the posting date — matching
    /// DocumentPostingOrchestrator.ValidateBudgetOnPostingAsync's own "no FY = skip silently" precedent.
    /// </summary>
    public async Task<CommonPartyReconciliationResult?> ReconcileAsync(CommonPartyReconciliationContext ctx)
    {
        var enabled = await _settingProvider.GetOrNullAsync(MyERPSettings.Accounts.EnableCommonPartyAccounting);
        if (enabled != "true") return null;

        if (ctx.OutstandingAmount <= 0) return null;

        var linkQuery = await _partyLinkRepository.GetQueryableAsync();
        var link = linkQuery.FirstOrDefault(l =>
            l.SecondaryPartyType == ctx.PartyType && l.SecondaryPartyId == ctx.PartyId);
        if (link == null) return null;

        var company = await _companyRepository.GetAsync(ctx.CompanyId);

        // Resolve the primary party's own account (opposite type from the document's party).
        Guid primaryAccountId;
        if (link.PrimaryPartyType == "Supplier")
        {
            var supplier = await _supplierRepository.GetAsync(link.PrimaryPartyId);
            primaryAccountId = supplier.DefaultPayableAccountId ?? company.DefaultPayableAccountId
                ?? throw new BusinessException(MyERPDomainErrorCodes.DefaultAccountNotConfigured)
                    .WithData("reason", "No payable account configured for the Common Party Accounting primary party.");
        }
        else
        {
            var customer = await _customerRepository.GetAsync(link.PrimaryPartyId);
            primaryAccountId = customer.DefaultReceivableAccountId ?? company.DefaultReceivableAccountId
                ?? throw new BusinessException(MyERPDomainErrorCodes.DefaultAccountNotConfigured)
                    .WithData("reason", "No receivable account configured for the Common Party Accounting primary party.");
        }

        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fiscalYear = fyQuery.FirstOrDefault(fy =>
            fy.CompanyId == ctx.CompanyId && fy.StartDate <= ctx.PostingDate && fy.EndDate >= ctx.PostingDate);
        if (fiscalYear == null) return null;

        // 3-way exchange rate resolution per the skill's algorithm (secondary→default, primary→default,
        // secondary→primary), scoped to what this codebase actually models — see class doc-comment.
        var secondaryToDefault = ctx.ExchangeRate;
        var primaryToDefault = 1m;
        var secondaryToPrimary = secondaryToDefault / primaryToDefault;

        var amountInSecondaryCurrency = ctx.OutstandingAmount;
        var amountInDefaultCurrency = Math.Round(amountInSecondaryCurrency * secondaryToDefault, 2);
        var amountInPrimaryCurrency = Math.Round(amountInSecondaryCurrency * secondaryToPrimary, 2);

        var je = new JournalEntry(GuidGenerator.Create(), ctx.CompanyId, fiscalYear.Id, ctx.PostingDate, ctx.TenantId)
        {
            ReferenceType = ctx.DocumentType,
            ReferenceId = ctx.DocumentId,
            ReferenceNumber = ctx.DocumentNumber,
            Narration = $"Common Party Accounting auto-reconciliation for {ctx.DocumentType} {ctx.DocumentNumber}",
            IsMultiCurrency = ctx.CurrencyCode != company.CurrencyCode,
        };

        // SalesInvoice (party=Customer): CR the receivable to reconcile it, DR the primary's account.
        // PurchaseInvoice (party=Supplier): DR the payable to reconcile it, CR the primary's account.
        bool secondaryLineIsDebit = ctx.PartyType == "Supplier";

        je.AddReconciliationLine(
            ctx.PartyAccountId, amountInDefaultCurrency, isDebit: secondaryLineIsDebit,
            ctx.PartyId, ctx.PartyType, ctx.CostCenterId, ctx.ProjectId,
            ctx.CurrencyCode, amountInSecondaryCurrency, secondaryToDefault,
            ctx.DocumentType, ctx.DocumentId, isAdvance: false,
            description: "Common Party Accounting reconciliation");

        je.AddReconciliationLine(
            primaryAccountId, amountInDefaultCurrency, isDebit: !secondaryLineIsDebit,
            link.PrimaryPartyId, link.PrimaryPartyType, ctx.CostCenterId, ctx.ProjectId,
            company.CurrencyCode, amountInPrimaryCurrency, primaryToDefault,
            null, null, isAdvance: !ctx.IsReturn,
            description: "Common Party Accounting advance");

        je.Post();
        await _journalRepository.InsertAsync(je);

        // PLE sign convention matches PostSalesInvoiceAsync/PostPurchaseInvoiceAsync: positive =
        // increasing Customer outstanding, negative = increasing Supplier outstanding — so a
        // reconciliation (which reduces outstanding either way) is the opposite sign of a fresh invoice.
        var pleAmount = secondaryLineIsDebit ? amountInDefaultCurrency : -amountInDefaultCurrency;
        var pleAmountInAccountCurrency = secondaryLineIsDebit ? amountInSecondaryCurrency : -amountInSecondaryCurrency;

        await _pleService.CreateEntryAsync(
            companyId: ctx.CompanyId, postingDate: ctx.PostingDate,
            accountId: ctx.PartyAccountId, partyType: ctx.PartyType, partyId: ctx.PartyId,
            voucherType: "JournalEntry", voucherId: je.Id,
            againstVoucherType: ctx.DocumentType, againstVoucherId: ctx.DocumentId,
            amount: pleAmount, amountInAccountCurrency: pleAmountInAccountCurrency,
            accountCurrency: ctx.CurrencyCode, tenantId: ctx.TenantId);

        return new CommonPartyReconciliationResult
        {
            JournalEntry = je,
            ReconciledAmount = amountInSecondaryCurrency,
        };
    }
}

/// <summary>Input for <see cref="CommonPartyAccountingService.ReconcileAsync"/>.</summary>
public class CommonPartyReconciliationContext
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime PostingDate { get; set; }

    /// <summary>"SalesInvoice" or "PurchaseInvoice".</summary>
    public string DocumentType { get; set; } = null!;
    public Guid DocumentId { get; set; }
    public string? DocumentNumber { get; set; }

    /// <summary>The document's own party type: "Customer" (SI) or "Supplier" (PI).</summary>
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }

    /// <summary>The invoice's own receivable/payable account.</summary>
    public Guid PartyAccountId { get; set; }

    /// <summary>Outstanding amount in transaction currency.</summary>
    public decimal OutstandingAmount { get; set; }
    public string CurrencyCode { get; set; } = "MYR";

    /// <summary>Transaction currency → company currency.</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public bool IsReturn { get; set; }
}

public class CommonPartyReconciliationResult
{
    public JournalEntry JournalEntry { get; set; } = null!;

    /// <summary>Amount reconciled, in the document's transaction currency — add to invoice.AmountPaid.</summary>
    public decimal ReconciledAmount { get; set; }
}
