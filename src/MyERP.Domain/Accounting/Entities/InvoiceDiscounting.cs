using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Invoice Discounting — selling Sales Invoice receivables to a bank at a discount, in exchange for
/// immediate cash (a short-term loan secured against the invoices). Maps to ERPNext
/// accounts/doctype/invoice_discounting.
///
/// Lifecycle: Draft -&gt; Sanctioned (Submit, moves each invoice's AR balance to the
/// AccountsReceivableCreditAccountId holding account) -&gt; Disbursed (bank pays out, a loan JE is posted) -&gt;
/// Settled (loan repaid from customer collections, a settlement JE is posted). Draft/Sanctioned/Disbursed
/// can all be Cancelled, which reverses whichever GL entries were posted so far.
/// </summary>
public class InvoiceDiscounting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; private set; }

    public DateTime PostingDate { get; private set; }
    public DateTime? LoanStartDate { get; set; }
    public int LoanPeriodDays { get; set; }
    public DateTime? LoanEndDate { get; private set; }

    public InvoiceDiscountingStatus Status { get; private set; } = InvoiceDiscountingStatus.Draft;

    public decimal TotalAmount { get; private set; }
    public decimal BankCharges { get; set; }

    public Guid ShortTermLoanAccountId { get; private set; }
    public Guid BankAccountId { get; private set; }
    public Guid BankChargesAccountId { get; private set; }
    public Guid AccountsReceivableCreditAccountId { get; private set; }
    public Guid AccountsReceivableDiscountedAccountId { get; private set; }
    public Guid AccountsReceivableUnpaidAccountId { get; private set; }

    /// <summary>The AR-swap Journal Entry posted when this document is Submitted (Draft -&gt; Sanctioned).</summary>
    public Guid? SanctionJournalEntryId { get; private set; }

    /// <summary>The loan-disbursement Journal Entry posted on Disburse (Sanctioned -&gt; Disbursed).</summary>
    public Guid? DisbursementJournalEntryId { get; private set; }

    /// <summary>The loan-settlement Journal Entry posted on Settle (Disbursed -&gt; Settled).</summary>
    public Guid? SettlementJournalEntryId { get; private set; }

    private readonly List<InvoiceDiscountingInvoice> _invoices = new();
    public IReadOnlyList<InvoiceDiscountingInvoice> Invoices => _invoices.AsReadOnly();

    protected InvoiceDiscounting() { }

    public InvoiceDiscounting(
        Guid id, Guid companyId, DateTime postingDate,
        Guid shortTermLoanAccountId, Guid bankAccountId, Guid bankChargesAccountId,
        Guid accountsReceivableCreditAccountId, Guid accountsReceivableDiscountedAccountId,
        Guid accountsReceivableUnpaidAccountId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        PostingDate = postingDate;
        ShortTermLoanAccountId = Check.NotDefaultOrNull<Guid>(shortTermLoanAccountId, nameof(shortTermLoanAccountId));
        BankAccountId = Check.NotDefaultOrNull<Guid>(bankAccountId, nameof(bankAccountId));
        BankChargesAccountId = Check.NotDefaultOrNull<Guid>(bankChargesAccountId, nameof(bankChargesAccountId));
        AccountsReceivableCreditAccountId = Check.NotDefaultOrNull<Guid>(accountsReceivableCreditAccountId, nameof(accountsReceivableCreditAccountId));
        AccountsReceivableDiscountedAccountId = Check.NotDefaultOrNull<Guid>(accountsReceivableDiscountedAccountId, nameof(accountsReceivableDiscountedAccountId));
        AccountsReceivableUnpaidAccountId = Check.NotDefaultOrNull<Guid>(accountsReceivableUnpaidAccountId, nameof(accountsReceivableUnpaidAccountId));
        TenantId = tenantId;
    }

    /// <summary>Replaces the invoice set. Only allowed while Draft — mirrors ERPNext's editable-grid-until-submit behavior.</summary>
    public void SetInvoices(IEnumerable<InvoiceDiscountingInvoice> invoices)
    {
        if (Status != InvoiceDiscountingStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _invoices.Clear();
        _invoices.AddRange(invoices);
        TotalAmount = _invoices.Sum(i => i.OutstandingAmount);
    }

    /// <summary>
    /// Draft -&gt; Sanctioned. Per ERPNext validate_mandatory(): loan start date/period are only
    /// required at submit time, not while Draft.
    /// </summary>
    public void Submit()
    {
        if (Status != InvoiceDiscountingStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (_invoices.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.InvoiceDiscountingNoInvoices);

        if (LoanStartDate == null || LoanPeriodDays <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.InvoiceDiscountingLoanTermsRequired);

        LoanEndDate = LoanStartDate.Value.AddDays(LoanPeriodDays);
        Status = InvoiceDiscountingStatus.Sanctioned;
    }

    public void MarkSanctionPosted(Guid journalEntryId)
    {
        if (Status != InvoiceDiscountingStatus.Sanctioned)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        SanctionJournalEntryId = journalEntryId;
    }

    /// <summary>Sanctioned -&gt; Disbursed: the bank has paid out the loan.</summary>
    public void MarkDisbursed(Guid journalEntryId, decimal bankCharges)
    {
        if (Status != InvoiceDiscountingStatus.Sanctioned)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        BankCharges = bankCharges;
        DisbursementJournalEntryId = journalEntryId;
        Status = InvoiceDiscountingStatus.Disbursed;
    }

    /// <summary>Disbursed -&gt; Settled: the loan has been repaid.</summary>
    public void MarkSettled(Guid journalEntryId)
    {
        if (Status != InvoiceDiscountingStatus.Disbursed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        SettlementJournalEntryId = journalEntryId;
        Status = InvoiceDiscountingStatus.Settled;
    }

    /// <summary>
    /// Cancel from Draft, Sanctioned, or Disbursed (not Settled — matches every other document type's
    /// "cannot cancel a fully-realized document" rule in this codebase, e.g. PaymentOrder/SalesInvoice).
    /// </summary>
    public void Cancel()
    {
        if (Status is InvoiceDiscountingStatus.Cancelled or InvoiceDiscountingStatus.Settled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = InvoiceDiscountingStatus.Cancelled;
    }
}

/// <summary>One Sales Invoice's receivable balance pledged into an Invoice Discounting loan.</summary>
public class InvoiceDiscountingInvoice : Entity<Guid>
{
    public Guid InvoiceDiscountingId { get; set; }
    public Guid SalesInvoiceId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal OutstandingAmount { get; set; }

    protected InvoiceDiscountingInvoice() { }

    public InvoiceDiscountingInvoice(Guid id, Guid invoiceDiscountingId, Guid salesInvoiceId, Guid customerId, decimal outstandingAmount)
        : base(id)
    {
        InvoiceDiscountingId = invoiceDiscountingId;
        SalesInvoiceId = salesInvoiceId;
        CustomerId = customerId;
        OutstandingAmount = outstandingAmount;
    }
}
