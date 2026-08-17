using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Journal Entry — the core of double-entry accounting.
/// Every financial transaction must produce a balanced JournalEntry.
/// Maps to ERPNext accounts/doctype/journal_entry.
/// </summary>
public class JournalEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }

    public string? EntryNumber { get; set; }
    public DateTime PostingDate { get; set; }

    /// <summary>Source document type (e.g., "SalesInvoice", "PurchaseInvoice", "Manual").</summary>
    public string? ReferenceType { get; set; }

    /// <summary>Source document ID.</summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>Human-readable reference number.</summary>
    public string? ReferenceNumber { get; set; }

    public string? Narration { get; set; }

    /// <summary>
    /// Voucher type determines validation rules and GL behavior.
    /// Per ERPNext: 18 voucher types with type-specific account restrictions.
    /// </summary>
    public JournalEntryVoucherType VoucherType { get; set; } = JournalEntryVoucherType.JournalEntry;

    /// <summary>If this is a reversal, references the original JE being reversed.</summary>
    public Guid? ReversalOfId { get; set; }

    /// <summary>Indicates this is an opening balance entry (auto-forces IsOpening on lines).</summary>
    public bool IsOpening { get; set; }

    /// <summary>Multi-currency: whether this JE involves foreign currency accounts.</summary>
    public bool IsMultiCurrency { get; set; }

    /// <summary>Inter-company: the other company in an inter-company JE.</summary>
    public Guid? InterCompanyJournalEntryId { get; set; }

    /// <summary>
    /// Date the bank confirmed this entry cleared (from Bank Clearance / Bank Reconciliation).
    /// Null = outstanding (not yet cleared at the bank). Only meaningful for BankEntry/ContraEntry/
    /// CreditCardEntry voucher types that touch a bank GL account.
    /// Maps to ERPNext accounts/doctype/bank_clearance clearance_date.
    /// </summary>
    public DateTime? ClearanceDate { get; private set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public decimal TotalDebit { get; private set; }
    public decimal TotalCredit { get; private set; }

    private readonly List<JournalEntryLine> _lines = new();
    public IReadOnlyList<JournalEntryLine> Lines => _lines.AsReadOnly();

    protected JournalEntry() { }

    public JournalEntry(Guid id, Guid companyId, Guid fiscalYearId, DateTime postingDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        FiscalYearId = Check.NotDefaultOrNull<Guid>(fiscalYearId, nameof(fiscalYearId));
        PostingDate = postingDate;
        TenantId = tenantId;
    }

    public void AddLine(Guid accountId, decimal amount, bool isDebit, string? description = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));

        _lines.Add(new JournalEntryLine(
            Guid.NewGuid(),
            Id,
            accountId,
            amount,
            isDebit,
            description));

        RecalculateTotals();
    }

    /// <summary>
    /// Adds a GL line with dimension support (cost center, project, finance book).
    /// Per ERPNext gotcha #24: accounting dimensions must propagate to exchange gain/loss JEs.
    /// </summary>
    public void AddLineWithDimensions(Guid accountId, decimal amount, bool isDebit,
        Guid? costCenterId = null, Guid? projectId = null, string? financeBook = null,
        string? description = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));

        var line = new JournalEntryLine(
            Guid.NewGuid(), Id, accountId, amount, isDebit, description);
        line.CostCenterId = costCenterId;
        line.ProjectId = projectId;
        line.FinanceBook = financeBook;
        _lines.Add(line);

        RecalculateTotals();
    }

    /// <summary>
    /// Adds a line with party reference. Validates account type compatibility.
    /// Per DO-NOT: "Allow party on non-Receivable/Payable/Equity accounts in GL entries"
    /// </summary>
    public void AddLineWithParty(Guid accountId, decimal amount, bool isDebit,
        Guid partyId, string partyType, AccountSubType? accountSubType, string? description = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));

        // Validate: party can only be set on Receivable, Payable, or Equity accounts
        if (accountSubType.HasValue &&
            accountSubType != AccountSubType.AccountsReceivable &&
            accountSubType != AccountSubType.AccountsPayable &&
            accountSubType != AccountSubType.ShareCapital &&
            accountSubType != AccountSubType.RetainedEarnings)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PartyNotAllowedOnAccount)
                .WithData("partyType", partyType)
                .WithData("accountSubType", accountSubType.Value.ToString());
        }

        var line = new JournalEntryLine(Guid.NewGuid(), Id, accountId, amount, isDebit, description);
        line.PartyId = partyId;
        line.PartyType = partyType;
        _lines.Add(line);

        RecalculateTotals();
    }

    /// <summary>
    /// Validates that Total Debit = Total Credit (double-entry requirement).
    /// </summary>
    public void Validate()
    {
        if (!_lines.Any())
            throw new BusinessException(MyERPDomainErrorCodes.UnbalancedJournalEntry);

        RecalculateTotals();

        if (TotalDebit != TotalCredit)
            throw new BusinessException(MyERPDomainErrorCodes.UnbalancedJournalEntry)
                .WithData("totalDebit", TotalDebit)
                .WithData("totalCredit", TotalCredit);
    }

    public void Post()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Validate();
        ValidateVoucherTypeRules();
        Status = DocumentStatus.Posted;
        AddLocalEvent(new JournalEntryPostedEvent(this));
    }

    /// <summary>
    /// Validates voucher type-specific rules per ERPNext journal_entry.py.
    /// Per gotcha #2614: JE has 18 voucher types with distinct validation paths.
    /// </summary>
    private void ValidateVoucherTypeRules()
    {
        switch (VoucherType)
        {
            case JournalEntryVoucherType.OpeningEntry:
                // Per ERPNext: opening entries auto-force is_opening on all lines
                IsOpening = true;
                break;

            case JournalEntryVoucherType.Reversal:
                // Per ERPNext: reversal JEs MUST reference the original
                if (!ReversalOfId.HasValue)
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                        .WithData("detail", "Reversal JE must reference the original journal entry.");
                break;

            case JournalEntryVoucherType.DepreciationEntry:
                // Per ERPNext: depreciation entries skip certain validations
                // (party mandatory check, reference duplication check)
                break;

            case JournalEntryVoucherType.ExchangeGainOrLoss:
                // Per gotcha #591: Exchange GL allows same-row DR+CR with multi_currency
                // No additional validation needed — standard balance check suffices
                break;

            case JournalEntryVoucherType.PeriodClosing:
                // Per gotcha #317: closing account must be Liability or Equity
                // Validated externally by PeriodClosingPostingService
                break;
        }
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Posted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = DocumentStatus.Cancelled;
        AddLocalEvent(new JournalEntryCancelledEvent(this));
    }

    /// <summary>
    /// Sets or clears the bank clearance date. Pass null to un-clear (reversible).
    /// Per ERPNext bank_clearance.py: clearance_date must not precede PostingDate.
    /// Opening entries (IsOpening) are excluded from clearance per ERPNext bank_clearance.py.
    /// </summary>
    public void SetClearanceDate(DateTime? clearanceDate)
    {
        if (IsOpening)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Opening journal entries cannot be bank-cleared.");

        if (clearanceDate.HasValue && clearanceDate.Value.Date < PostingDate.Date)
            throw new BusinessException(MyERPDomainErrorCodes.ClearanceDateBeforePostingDate)
                .WithData("clearanceDate", clearanceDate.Value)
                .WithData("postingDate", PostingDate);

        ClearanceDate = clearanceDate;
    }

    private void RecalculateTotals()
    {
        TotalDebit = _lines.Where(l => l.IsDebit).Sum(l => l.Amount);
        TotalCredit = _lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
    }
}

// Domain Events
public record JournalEntryPostedEvent(JournalEntry JournalEntry);
public record JournalEntryCancelledEvent(JournalEntry JournalEntry);
