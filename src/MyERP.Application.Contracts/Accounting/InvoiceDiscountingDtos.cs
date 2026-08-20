using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class InvoiceDiscountingDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime? LoanStartDate { get; set; }
    public int LoanPeriodDays { get; set; }
    public DateTime? LoanEndDate { get; set; }
    public int Status { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BankCharges { get; set; }

    public Guid ShortTermLoanAccountId { get; set; }
    public Guid BankAccountId { get; set; }
    public Guid BankChargesAccountId { get; set; }
    public Guid AccountsReceivableCreditAccountId { get; set; }
    public Guid AccountsReceivableDiscountedAccountId { get; set; }
    public Guid AccountsReceivableUnpaidAccountId { get; set; }

    public Guid? SanctionJournalEntryId { get; set; }
    public Guid? DisbursementJournalEntryId { get; set; }
    public Guid? SettlementJournalEntryId { get; set; }

    public List<InvoiceDiscountingInvoiceDto> Invoices { get; set; } = new();
}

public class InvoiceDiscountingInvoiceDto
{
    public Guid SalesInvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal OutstandingAmount { get; set; }
}

/// <summary>A Sales Invoice eligible (or not) to be pledged into a new Invoice Discounting document.</summary>
public class InvoiceForDiscountingDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public decimal OutstandingAmount { get; set; }
}

public class CreateInvoiceDiscountingDto
{
    public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; }
    public Guid ShortTermLoanAccountId { get; set; }
    public Guid BankAccountId { get; set; }
    public Guid BankChargesAccountId { get; set; }
    public Guid AccountsReceivableCreditAccountId { get; set; }
    public Guid AccountsReceivableDiscountedAccountId { get; set; }
    public Guid AccountsReceivableUnpaidAccountId { get; set; }
    public List<CreateInvoiceDiscountingInvoiceDto> Invoices { get; set; } = new();
}

public class CreateInvoiceDiscountingInvoiceDto
{
    public Guid SalesInvoiceId { get; set; }
    public decimal OutstandingAmount { get; set; }
}

public class SubmitInvoiceDiscountingDto
{
    public DateTime LoanStartDate { get; set; }
    public int LoanPeriodDays { get; set; }
}

public class DisburseInvoiceDiscountingDto
{
    public decimal BankCharges { get; set; }
}

public class CalculateDiscountingDto
{
    public decimal TotalOutstanding { get; set; }
    public decimal AnnualDiscountRate { get; set; }
    public int DaysToMaturity { get; set; }
}

public class DiscountingCalculationResultDto
{
    public decimal DiscountCharge { get; set; }
    public decimal DisbursementAmount { get; set; }
    public decimal EffectiveRate { get; set; }
}
