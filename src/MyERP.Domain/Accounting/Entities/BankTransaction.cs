using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Represents a bank transaction imported from a bank statement.
/// Used for matching against Payment Entries for reconciliation.
/// Migrated from ERPNext banking module (bank_transaction doctype).
/// </summary>
public class BankTransaction : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BankAccountId { get; set; }

    /// <summary>Transaction date from the bank statement.</summary>
    public DateTime TransactionDate { get; set; }

    /// <summary>Description/narration from bank statement.</summary>
    public string Description { get; set; } = null!;

    /// <summary>Amount (positive = credit/deposit, negative = debit/withdrawal).</summary>
    public decimal Amount { get; set; }

    /// <summary>Bank reference number.</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>Whether this transaction has been reconciled with a payment entry.</summary>
    public bool IsReconciled { get; set; }

    /// <summary>Linked Payment Entry ID (set when reconciled).</summary>
    public Guid? PaymentEntryId { get; set; }

    /// <summary>Matched document reference (e.g., invoice number).</summary>
    public string? MatchedDocumentRef { get; set; }

    /// <summary>Date when reconciliation was performed.</summary>
    public DateTime? ReconciledAt { get; set; }

    /// <summary>Transaction currency (must match bank account's GL account currency).</summary>
    public string CurrencyCode { get; set; } = "MYR";

    /// <summary>Deposit amount (money in). Only one of Deposit/Withdrawal should be non-zero.</summary>
    public decimal Deposit { get; set; }

    /// <summary>Withdrawal amount (money out).</summary>
    public decimal Withdrawal { get; set; }

    /// <summary>Fee included within the deposit/withdrawal amount.</summary>
    public decimal IncludedFee { get; set; }

    /// <summary>Fee excluded from the deposit/withdrawal (transformed to IncludedFee on save).</summary>
    public decimal ExcludedFee { get; set; }

    /// <summary>Whether rule evaluation has been performed on this transaction.</summary>
    public bool IsRuleEvaluated { get; set; }

    /// <summary>The matched Bank Transaction Rule ID (set during rule evaluation).</summary>
    public Guid? MatchedTransactionRuleId { get; set; }

    protected BankTransaction() { }

    public BankTransaction(Guid id, Guid companyId, Guid bankAccountId,
        DateTime transactionDate, string description, decimal amount, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        BankAccountId = bankAccountId;
        TransactionDate = transactionDate;
        Description = description;
        Amount = amount;
        TenantId = tenantId;
    }

    public void Reconcile(Guid paymentEntryId, string? matchedDocRef)
    {
        IsReconciled = true;
        PaymentEntryId = paymentEntryId;
        MatchedDocumentRef = matchedDocRef;
        ReconciledAt = DateTime.UtcNow;
    }

    public void Unreconcile()
    {
        IsReconciled = false;
        PaymentEntryId = null;
        MatchedDocumentRef = null;
        ReconciledAt = null;
    }

    /// <summary>
    /// Transforms excluded fees into included fees by adjusting the transaction amount.
    /// Per ERPNext: handle_excluded_fee() runs in before_validate.
    /// Deposit: fee subtracted from deposit. Withdrawal: fee added to withdrawal.
    /// After transform, only IncludedFee is non-zero.
    /// </summary>
    public void NormalizeFees()
    {
        if (ExcludedFee <= 0) return;

        // Cannot have both deposit and withdrawal when fee applies
        if (Deposit > 0 && Withdrawal > 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.BidirectionalFeeTransaction);
        }

        // Fee cannot exceed deposit
        if (Deposit > 0 && (Deposit - ExcludedFee) < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ExcludedFeeExceedsDeposit)
                .WithData("fee", ExcludedFee)
                .WithData("deposit", Deposit);
        }

        if (Deposit > 0)
            Deposit -= ExcludedFee;
        else
            Withdrawal += ExcludedFee;

        IncludedFee += ExcludedFee;
        ExcludedFee = 0;
    }

    /// <summary>
    /// Validates included fee does not exceed withdrawal amount.
    /// Per ERPNext: included_fee is only meaningful for withdrawals.
    /// </summary>
    public void ValidateIncludedFee()
    {
        if (IncludedFee > 0 && Withdrawal > 0 && IncludedFee > Withdrawal)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.IncludedFeeExceedsWithdrawal)
                .WithData("fee", IncludedFee)
                .WithData("withdrawal", Withdrawal);
        }
    }

    /// <summary>
    /// Validates transaction currency matches bank account GL currency.
    /// Must be called with the resolved bank account currency.
    /// </summary>
    public void ValidateCurrency(string bankAccountCurrency)
    {
        if (!string.IsNullOrEmpty(CurrencyCode) && !string.IsNullOrEmpty(bankAccountCurrency)
            && CurrencyCode != bankAccountCurrency)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.BankTransactionCurrencyMismatch)
                .WithData("transactionCurrency", CurrencyCode)
                .WithData("bankAccountCurrency", bankAccountCurrency);
        }
    }
}
