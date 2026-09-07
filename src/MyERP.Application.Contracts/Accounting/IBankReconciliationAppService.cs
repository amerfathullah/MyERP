using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBankReconciliationAppService : IApplicationService
{
    Task<PagedResultDto<BankTransactionDto>> GetTransactionsAsync(GetBankTransactionsDto input);
    Task<BankTransactionDto> ReconcileAsync(ReconcileBankTransactionDto input);
    Task<BankTransactionDto> UnreconcileAsync(Guid id);
    Task<BankTransactionDto> ImportTransactionAsync(ImportBankTransactionDto input);
    Task<AutoMatchResultDto> AutoMatchAsync(Guid bankAccountId, Guid companyId);
    Task<BankReconciliationSummaryDto> GetSummaryAsync(Guid bankAccountId);
    Task<List<MatchCandidateDto>> GetMatchCandidatesAsync(Guid bankTransactionId, Guid companyId);
    Task<MirrorTransactionDto?> SearchForMirrorTransactionAsync(Guid transactionId);
    Task<InternalTransferResultDto> CreateInternalTransferAsync(CreateInternalTransferDto input);
    Task<VoucherCreatedResultDto> CreatePaymentEntryFromTransactionAsync(CreatePEFromTransactionDto input);
    Task<JournalEntryCreatedResultDto> CreateJournalEntryFromTransactionAsync(CreateJEFromTransactionDto input);
    Task<BankReconciliationStatementDto> GetReconciliationStatementAsync(GetBankReconciliationStatementInput input);
}

/// <summary>
/// Bank Reconciliation Statement — compares GL balance with uncleared items to derive expected bank balance.
/// Per ERPNext: GL balance - outstanding uncleared = calculated bank statement balance.
/// Critical for month-end close and bank statement matching.
/// </summary>
public class GetBankReconciliationStatementInput
{
    [Required]
    public Guid BankAccountId { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public DateTime ReportDate { get; set; }
}

public class BankReconciliationStatementDto
{
    /// <summary>Total GL balance for the bank account as of report date (SUM debit - SUM credit)</summary>
    public decimal GlBalance { get; set; }

    /// <summary>Total debit of uncleared entries (outstanding deposits)</summary>
    public decimal OutstandingDeposits { get; set; }

    /// <summary>Total credit of uncleared entries (outstanding payments/checks)</summary>
    public decimal OutstandingPayments { get; set; }

    /// <summary>Net outstanding = Deposits - Payments</summary>
    public decimal NetOutstanding => OutstandingDeposits - OutstandingPayments;

    /// <summary>Calculated bank statement balance = GL Balance - Net Outstanding</summary>
    public decimal CalculatedBankBalance => GlBalance - NetOutstanding;

    /// <summary>Individual uncleared entries for the statement listing</summary>
    public List<BankStatementEntryDto> UnclearedEntries { get; set; } = new();

    public string CurrencyCode { get; set; } = "MYR";
    public DateTime ReportDate { get; set; }
    public string BankAccountName { get; set; } = string.Empty;
}

public class BankTransactionDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid BankAccountId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
    public bool IsReconciled { get; set; }
    public Guid? PaymentEntryId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? MatchedDocumentRef { get; set; }
    public DateTime? ReconciledAt { get; set; }
}

public class GetBankTransactionsDto : PagedAndSortedResultRequestDto
{
    [Required]
    public Guid BankAccountId { get; set; }

    public bool? IsReconciled { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

public class ReconcileBankTransactionDto
{
    [Required]
    public Guid TransactionId { get; set; }

    /// <summary>Exactly one of PaymentEntryId / JournalEntryId must be set.</summary>
    public Guid? PaymentEntryId { get; set; }

    public Guid? JournalEntryId { get; set; }

    public string? MatchedDocumentRef { get; set; }
}

public class ImportBankTransactionDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid BankAccountId { get; set; }

    [Required]
    public DateTime TransactionDate { get; set; }

    [Required]
    public string Description { get; set; } = null!;

    [Required]
    public decimal Amount { get; set; }

    public string? ReferenceNumber { get; set; }
}

public class BankReconciliationSummaryDto
{
    public int TotalTransactions { get; set; }
    public int ReconciledCount { get; set; }
    public int UnreconciledCount { get; set; }
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal UnreconciledBalance { get; set; }
}

public class AutoMatchResultDto
{
    public int MatchedCount { get; set; }
    public int PartiallyReconciledCount { get; set; }
    public int UnmatchedCount { get; set; }
}

public class MatchCandidateDto
{
    /// <summary>"PaymentEntry" or "JournalEntry" — which field below is populated.</summary>
    public string VoucherType { get; set; } = "PaymentEntry";
    public Guid? PaymentEntryId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? PaymentNumber { get; set; }
    public decimal Amount { get; set; }
    public DateTime PostingDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public int Rank { get; set; }
}

public class MirrorTransactionDto
{
    public Guid TransactionId { get; set; }
    public Guid BankAccountId { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Deposit { get; set; }
    public decimal Withdrawal { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
}

public class CreateInternalTransferDto
{
    [Required]
    public Guid BankTransactionId { get; set; }

    [Required]
    public Guid TargetBankAccountGlId { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    /// <summary>Optional: mirror transaction to also reconcile.</summary>
    public Guid? MirrorTransactionId { get; set; }
}

public class InternalTransferResultDto
{
    public Guid PaymentEntryId { get; set; }
    public string? PaymentNumber { get; set; }
    public Guid SourceTransactionId { get; set; }
    public Guid? MirrorTransactionId { get; set; }
}

/// <summary>
/// Creates a Payment Entry directly from a bank transaction.
/// Per ERPNext banking module: "Voucher Created" reconciliation type.
/// Deposit → Receive PE from Customer; Withdrawal → Pay PE to Supplier.
/// </summary>
public class CreatePEFromTransactionDto
{
    [Required]
    public Guid BankTransactionId { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    /// <summary>Customer or Supplier</summary>
    [Required]
    public string PartyType { get; set; } = null!;

    [Required]
    public Guid PartyId { get; set; }

    /// <summary>The bank GL account (Paid From for Pay, Paid To for Receive)</summary>
    [Required]
    public Guid BankAccountId { get; set; }

    /// <summary>The receivable/payable account (resolved from party or company default)</summary>
    [Required]
    public Guid PartyAccountId { get; set; }

    /// <summary>Optional: specific invoice to allocate against</summary>
    public Guid? AgainstInvoiceId { get; set; }

    /// <summary>Optional: mode of payment (Cash, Bank Transfer, etc.)</summary>
    public Guid? ModeOfPaymentId { get; set; }
}

public class VoucherCreatedResultDto
{
    public Guid PaymentEntryId { get; set; }
    public string PaymentNumber { get; set; } = null!;
    public decimal Amount { get; set; }
    public string PaymentType { get; set; } = null!;
    public Guid BankTransactionId { get; set; }
    public bool IsReconciled { get; set; }
}

/// <summary>
/// Creates a Journal Entry directly from a bank transaction (bank charges, interest income, tax, contra, etc.)
/// and auto-reconciles it with the bank transaction.
/// Per ERPNext: create_journal_entry_bts() in bank_reconciliation_tool.py.
/// </summary>
public class CreateJEFromTransactionDto
{
    [Required]
    public Guid BankTransactionId { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    /// <summary>The balancing GL account (e.g. Bank Charges expense, Interest income, etc.)</summary>
    [Required]
    public Guid SecondAccountId { get; set; }

    /// <summary>Voucher type (BankEntry, ContraEntry, JournalEntry, etc.). Defaults to BankEntry.</summary>
    public JournalEntryVoucherType VoucherType { get; set; } = JournalEntryVoucherType.BankEntry;

    /// <summary>Optional party type (Customer / Supplier / Employee) if second account is Receivable/Payable.</summary>
    public string? PartyType { get; set; }

    /// <summary>Optional party ID if second account is Receivable/Payable.</summary>
    public Guid? PartyId { get; set; }

    /// <summary>Optional cost center.</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Optional user narration override.</summary>
    public string? Narration { get; set; }
}

public class JournalEntryCreatedResultDto
{
    public Guid JournalEntryId { get; set; }
    public string EntryNumber { get; set; } = null!;
    public decimal Amount { get; set; }
    public string VoucherType { get; set; } = null!;
    public Guid BankTransactionId { get; set; }
    public bool IsReconciled { get; set; }
}

public class BankStatementEntryDto
{
    public DateTime PostingDate { get; set; }
    public string DocumentType { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public Guid DocumentId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime? ClearanceDate { get; set; }
    public string? PartyName { get; set; }
}
