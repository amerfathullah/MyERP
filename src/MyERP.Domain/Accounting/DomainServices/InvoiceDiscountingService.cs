using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Domain service for Invoice Discounting lifecycle management.
/// Per ERPNext: Invoice Discounting = selling receivables to a bank at a discount.
///
/// State machine: Draft → Sanctioned → Disbursed → Settled / Cancelled
/// Transitions driven by Journal Entry submit/cancel events:
/// - JE with credit on short_term_loan → Sanctioned → Disbursed
/// - JE with debit on short_term_loan → Disbursed → Settled
///
/// GL pattern:
/// - On sanction: DR Bank / CR Short-Term Loan (discounted amount)
/// - On settlement: DR Short-Term Loan / CR Customer Receivable
/// - Discount charges: DR Discount Expense / CR Bank
///
/// Per DO-NOT rules:
/// - Allow invoice discounting on already-discounted invoices (blocked)
/// - Skip Invoice Discounting status transition on JE submit/cancel
/// </summary>
public class InvoiceDiscountingService : DomainService
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;

    public InvoiceDiscountingService(
        IRepository<JournalEntry, Guid> journalEntryRepository)
    {
        _journalEntryRepository = journalEntryRepository;
    }

    /// <summary>
    /// Determine the new Invoice Discounting status based on a JE event.
    /// Per ERPNext: JE submit/cancel drives the state machine.
    ///
    /// On JE submit:
    ///   - JE row CREDITS short_term_loan → Sanctioned → Disbursed
    ///   - JE row DEBITS short_term_loan → Disbursed → Settled
    /// On JE cancel:
    ///   - Reverse: Disbursed → Sanctioned or Settled → Disbursed
    /// </summary>
    public InvoiceDiscountingStatus DetermineStatusFromJournalEntry(
        InvoiceDiscountingStatus currentStatus,
        Guid shortTermLoanAccountId,
        IReadOnlyList<JournalEntryLine> jeLines,
        bool isJeSubmit)
    {
        // Find the JE row that references the short-term loan account
        var loanRow = jeLines.FirstOrDefault(l => l.AccountId == shortTermLoanAccountId);
        if (loanRow == null)
            return currentStatus; // JE doesn't affect invoice discounting

        // JournalEntryLine uses Amount + IsDebit (true=debit, false=credit)
        var isCredit = !loanRow.IsDebit && loanRow.Amount > 0;
        var isDebit = loanRow.IsDebit && loanRow.Amount > 0;

        if (isJeSubmit)
        {
            // Credit on loan account = disbursement (bank gives money)
            if (isCredit && currentStatus == InvoiceDiscountingStatus.Sanctioned)
                return InvoiceDiscountingStatus.Disbursed;

            // Debit on loan account = settlement (repaying the loan)
            if (isDebit && currentStatus == InvoiceDiscountingStatus.Disbursed)
                return InvoiceDiscountingStatus.Settled;
        }
        else // JE cancel — reverse transitions
        {
            if (isCredit && currentStatus == InvoiceDiscountingStatus.Disbursed)
                return InvoiceDiscountingStatus.Sanctioned;

            if (isDebit && currentStatus == InvoiceDiscountingStatus.Settled)
                return InvoiceDiscountingStatus.Disbursed;
        }

        return currentStatus;
    }

    /// <summary>
    /// Calculate the discount charge (bank's fee) for discounting invoices.
    /// Typically: total_outstanding × discount_rate × remaining_days / 365
    /// </summary>
    public decimal CalculateDiscountCharge(
        decimal totalOutstanding,
        decimal annualDiscountRate,
        int daysToMaturity)
    {
        if (daysToMaturity <= 0 || annualDiscountRate <= 0)
            return 0;

        return Math.Round(totalOutstanding * annualDiscountRate / 100m * daysToMaturity / 365m, 2);
    }

    /// <summary>
    /// Calculate the disbursement amount (what the bank pays).
    /// = Total Outstanding - Discount Charge
    /// </summary>
    public decimal CalculateDisbursementAmount(
        decimal totalOutstanding,
        decimal discountCharge)
    {
        return totalOutstanding - discountCharge;
    }

    /// <summary>
    /// Validate that invoices are eligible for discounting.
    /// Per DO-NOT: cannot discount already-discounted invoices.
    /// </summary>
    public static void ValidateInvoicesForDiscounting(
        IReadOnlyList<InvoiceForDiscounting> invoices)
    {
        foreach (var inv in invoices)
        {
            if (inv.IsAlreadyDiscounted)
                throw new BusinessException("MyERP:02019")
                    .WithData("invoiceNumber", inv.InvoiceNumber);

            if (inv.OutstandingAmount <= 0)
                throw new BusinessException("MyERP:02020")
                    .WithData("invoiceNumber", inv.InvoiceNumber);
        }
    }

    /// <summary>
    /// Build GL entries for the disbursement JE (bank pays company).
    /// DR Bank Account (disbursement amount)
    /// DR Discount Expense (discount charge)
    /// CR Short-Term Loan (total outstanding)
    /// </summary>
    public static List<DiscountingGlEntry> BuildDisbursementGlEntries(
        Guid bankAccountId,
        Guid discountExpenseAccountId,
        Guid shortTermLoanAccountId,
        decimal totalOutstanding,
        decimal discountCharge,
        decimal disbursementAmount)
    {
        return new List<DiscountingGlEntry>
        {
            new() { AccountId = bankAccountId, Debit = disbursementAmount, Credit = 0 },
            new() { AccountId = discountExpenseAccountId, Debit = discountCharge, Credit = 0 },
            new() { AccountId = shortTermLoanAccountId, Debit = 0, Credit = totalOutstanding },
        };
    }

    /// <summary>
    /// Build GL entries for the settlement JE (company repays loan from customer payment).
    /// DR Short-Term Loan (total amount)
    /// CR Customer Receivable (total amount)
    /// </summary>
    public static List<DiscountingGlEntry> BuildSettlementGlEntries(
        Guid shortTermLoanAccountId,
        Guid receivableAccountId,
        decimal amount)
    {
        return new List<DiscountingGlEntry>
        {
            new() { AccountId = shortTermLoanAccountId, Debit = amount, Credit = 0 },
            new() { AccountId = receivableAccountId, Debit = 0, Credit = amount },
        };
    }
}

/// <summary>Invoice Discounting status state machine.</summary>
public enum InvoiceDiscountingStatus
{
    Draft = 0,
    Sanctioned = 1,
    Disbursed = 2,
    Settled = 3,
    Cancelled = 4,
}

/// <summary>Invoice details for discounting validation.</summary>
public class InvoiceForDiscounting
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public decimal OutstandingAmount { get; set; }
    public bool IsAlreadyDiscounted { get; set; }
}

/// <summary>GL entry line for discounting journal entries.</summary>
public class DiscountingGlEntry
{
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}
