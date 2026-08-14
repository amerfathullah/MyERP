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
