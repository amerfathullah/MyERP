using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class StatementOfAccountsDto
{
    public Guid CustomerId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public List<StatementEntryDto> Entries { get; set; } = new();
}

public class StatementEntryDto
{
    public DateTime Date { get; set; }
    public string DocumentType { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public Guid DocumentId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RunningBalance { get; set; }
}

public class SupplierStatementDto
{
    public Guid SupplierId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal TotalInvoiced { get; set; }
    public decimal TotalPaid { get; set; }
    public List<StatementEntryDto> Entries { get; set; } = new();
}

public class AgingBucketDto
{
    public decimal Current_0_30 { get; set; }
    public decimal Age_31_60 { get; set; }
    public decimal Age_61_90 { get; set; }
    public decimal Age_91_120 { get; set; }
    public decimal Age_120_Plus { get; set; }
    public decimal TotalOutstanding { get; set; }
}

public class PartyStatementSummaryDto
{
    public Guid PartyId { get; set; }
    public string PartyName { get; set; } = null!;
    public string PartyType { get; set; } = null!; // "Customer" or "Supplier"
    public decimal OpeningBalance { get; set; }
    public decimal InvoicedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ClosingBalance { get; set; }
    public AgingBucketDto? Aging { get; set; }
}

public class BatchStatementOfAccountsInput
{
    public Guid CompanyId { get; set; }
    public string PartyType { get; set; } = "Customer"; // "Customer" or "Supplier"
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<Guid>? PartyIds { get; set; }
    public bool IncludeZeroBalance { get; set; }
    public bool IncludeAging { get; set; } = true;
}

public class BatchStatementOfAccountsResultDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<PartyStatementSummaryDto> Statements { get; set; } = new();
    public decimal TotalOpeningBalance { get; set; }
    public decimal TotalInvoiced { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalClosingBalance { get; set; }
    public AgingBucketDto? GrandTotalAging { get; set; }
}
