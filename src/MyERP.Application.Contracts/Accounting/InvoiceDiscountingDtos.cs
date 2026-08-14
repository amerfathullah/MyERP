using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class InvoiceDiscountingDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal DiscountCharge { get; set; }
    public decimal DisbursementAmount { get; set; }
    public int Status { get; set; }
    public Guid? DisbursementJournalEntryId { get; set; }
    public Guid? SettlementJournalEntryId { get; set; }
}

public class CreateInvoiceDiscountingDto
{
    public Guid CompanyId { get; set; }
    public decimal AnnualDiscountRate { get; set; }
    public int DaysToMaturity { get; set; }
    public Guid BankAccountId { get; set; }
    public Guid DiscountExpenseAccountId { get; set; }
    public Guid ShortTermLoanAccountId { get; set; }
    public Guid ReceivableAccountId { get; set; }
    public List<InvoiceForDiscountingDto> Invoices { get; set; } = new();
}

public class InvoiceForDiscountingDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public decimal OutstandingAmount { get; set; }
    public bool IsAlreadyDiscounted { get; set; }
}
