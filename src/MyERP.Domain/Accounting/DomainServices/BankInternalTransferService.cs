using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Handles internal bank transfer creation from bank transactions.
/// Per ERPNext bank_reconciliation_tool.py: create_internal_transfer() + search_for_transfer_transaction().
///
/// Key behaviors:
/// - Discovers mirror transactions (opposite-sign in different bank account, ±N days)
/// - Creates Payment Entry (Internal Transfer) from bank transaction
/// - Reconciles BOTH sides (source + mirror) with the same PE
/// </summary>
public class BankInternalTransferService : DomainService
{
    private readonly IRepository<BankTransaction, Guid> _transactionRepository;

    /// <summary>
    /// Default number of days to search for mirror transaction.
    /// Per ERPNext: Accounts Settings.transfer_match_days, default 3.
    /// </summary>
    public const int DefaultTransferMatchDays = 3;

    public BankInternalTransferService(
        IRepository<BankTransaction, Guid> transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    /// <summary>
    /// Searches for the opposite-side bank transaction for an inter-bank transfer.
    /// Per ERPNext search_for_transfer_transaction():
    /// - For a withdrawal of X: find deposit of X in a DIFFERENT bank account
    /// - For a deposit of X: find withdrawal of X in a DIFFERENT bank account
    /// - Date range: ±transferMatchDays (default 3)
    /// - Must be same company, Unreconciled, submitted
    /// - Returns ONLY if exactly 1 match found (ambiguity → null)
    /// </summary>
    public async Task<MirrorTransactionResult?> SearchForMirrorTransactionAsync(
        Guid transactionId,
        int transferMatchDays = DefaultTransferMatchDays)
    {
        var query = await _transactionRepository.GetQueryableAsync();
        var tx = query.First(t => t.Id == transactionId);

        var minDate = tx.TransactionDate.AddDays(-transferMatchDays);
        var maxDate = tx.TransactionDate.AddDays(transferMatchDays);

        // Search for opposite-sign transaction in a different bank account
        var mirrorCandidates = query
            .Where(t => t.CompanyId == tx.CompanyId
                     && t.BankAccountId != tx.BankAccountId
                     && t.TransactionDate >= minDate
                     && t.TransactionDate <= maxDate
                     && !t.IsReconciled
                     && t.Id != tx.Id)
            .ToList();

        // Match opposite direction: withdrawal ↔ deposit
        var matches = mirrorCandidates
            .Where(m =>
                (tx.Withdrawal > 0 && Math.Abs(m.Deposit - tx.Withdrawal) < 0.01m) ||
                (tx.Deposit > 0 && Math.Abs(m.Withdrawal - tx.Deposit) < 0.01m))
            .ToList();

        // Per ERPNext: return only if exactly 1 match (ambiguity → null)
        if (matches.Count != 1)
            return null;

        var mirror = matches[0];
        return new MirrorTransactionResult
        {
            TransactionId = mirror.Id,
            BankAccountId = mirror.BankAccountId,
            ReferenceNumber = mirror.ReferenceNumber,
            TransactionDate = mirror.TransactionDate,
            Deposit = mirror.Deposit,
            Withdrawal = mirror.Withdrawal,
            CurrencyCode = mirror.CurrencyCode
        };
    }

    /// <summary>
    /// Builds an Internal Transfer Payment Entry from a bank transaction.
    /// Per ERPNext create_internal_transfer():
    /// - Determines direction from withdrawal/deposit
    /// - Withdrawal: paid_from = source bank, paid_to = target
    /// - Deposit: paid_from = target, paid_to = source bank
    /// - Amount = bank transaction unallocated amount
    /// Returns the configured PE (caller must insert + submit + reconcile).
    /// </summary>
    public InternalTransferSpec BuildInternalTransfer(
        BankTransaction sourceTransaction,
        Guid sourceBankGlAccountId,
        Guid targetBankGlAccountId,
        Guid companyId)
    {
        bool isWithdrawal = sourceTransaction.Withdrawal > 0;
        decimal amount = isWithdrawal ? sourceTransaction.Withdrawal : sourceTransaction.Deposit;

        // If using legacy Amount field
        if (amount == 0)
            amount = Math.Abs(sourceTransaction.Amount);

        return new InternalTransferSpec
        {
            CompanyId = companyId,
            PaidFromAccountId = isWithdrawal ? sourceBankGlAccountId : targetBankGlAccountId,
            PaidToAccountId = isWithdrawal ? targetBankGlAccountId : sourceBankGlAccountId,
            Amount = amount,
            PostingDate = sourceTransaction.TransactionDate,
            ReferenceNumber = (sourceTransaction.ReferenceNumber ?? sourceTransaction.Description ?? "")
                .Length > 140
                ? (sourceTransaction.ReferenceNumber ?? sourceTransaction.Description ?? "")[..140]
                : sourceTransaction.ReferenceNumber ?? sourceTransaction.Description ?? "",
            SourceTransactionId = sourceTransaction.Id,
        };
    }

    /// <summary>
    /// Reconciles a bank transaction with a payment entry.
    /// If mirrorTransactionId is provided, ALSO reconciles the mirror
    /// with the same PE (per ERPNext: one PE represents both sides).
    /// </summary>
    public async Task ReconcileBothSidesAsync(
        Guid sourceTransactionId,
        Guid? mirrorTransactionId,
        Guid paymentEntryId,
        string? paymentNumber)
    {
        var source = await _transactionRepository.GetAsync(sourceTransactionId);
        source.Reconcile(paymentEntryId, paymentNumber);
        await _transactionRepository.UpdateAsync(source);

        if (mirrorTransactionId.HasValue)
        {
            var mirror = await _transactionRepository.GetAsync(mirrorTransactionId.Value);
            mirror.Reconcile(paymentEntryId, paymentNumber);
            await _transactionRepository.UpdateAsync(mirror);
        }
    }
}

/// <summary>
/// Result of searching for a mirror bank transaction.
/// </summary>
public class MirrorTransactionResult
{
    public Guid TransactionId { get; set; }
    public Guid BankAccountId { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Deposit { get; set; }
    public decimal Withdrawal { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
}

/// <summary>
/// Specification for creating an Internal Transfer Payment Entry.
/// Caller uses this to create + submit the PE, then reconcile.
/// </summary>
public class InternalTransferSpec
{
    public Guid CompanyId { get; set; }
    public Guid PaidFromAccountId { get; set; }
    public Guid PaidToAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PostingDate { get; set; }
    public string ReferenceNumber { get; set; } = "";
    public Guid SourceTransactionId { get; set; }
}
