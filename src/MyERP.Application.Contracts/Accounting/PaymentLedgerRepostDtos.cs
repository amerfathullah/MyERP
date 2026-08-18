using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class RepostPaymentLedgerDto
{
    public Guid CompanyId { get; set; }
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
}

public class RepostPaymentLedgerForCompanyDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
}

public class PaymentLedgerRepostResultDto
{
    public int TotalVouchers { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public bool HasErrors { get; set; }
    public List<string> Errors { get; set; } = [];
}
