using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class InvalidLedgerEntriesRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<Guid>? AccountIds { get; set; }
    public string? VoucherNo { get; set; }
}

public class InvalidLedgerEntryRowDto
{
    public string VoucherType { get; set; } = string.Empty;
    public Guid VoucherId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public string LedgerSource { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class InvalidLedgerEntriesReportDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<InvalidLedgerEntryRowDto> Rows { get; set; } = new();
}
