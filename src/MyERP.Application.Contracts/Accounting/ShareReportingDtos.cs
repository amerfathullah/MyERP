using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class ShareBalanceRequestDto
{
    public DateTime Date { get; set; }
    public Guid? ShareholderId { get; set; }
    public Guid? CompanyId { get; set; }
}

public class ShareBalanceRowDto
{
    public Guid ShareholderId { get; set; }
    public string ShareholderTitle { get; set; } = string.Empty;
    public Guid ShareTypeId { get; set; }
    public string ShareTypeTitle { get; set; } = string.Empty;
    public int NoOfShares { get; set; }
    public decimal AverageRate { get; set; }
    public decimal Amount { get; set; }
}

public class ShareBalanceReportDto
{
    public DateTime Date { get; set; }
    public Guid? ShareholderId { get; set; }
    public string ShareholderTitle { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public List<ShareBalanceRowDto> Rows { get; set; } = new();
    public int TotalNoOfShares { get; set; }
    public decimal TotalAmount { get; set; }
}

public class ShareLedgerRequestDto
{
    public DateTime Date { get; set; }
    public Guid? ShareholderId { get; set; }
    public Guid? CompanyId { get; set; }
}

public class ShareLedgerRowDto
{
    public Guid ShareholderId { get; set; }
    public string ShareholderTitle { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string TransferType { get; set; } = string.Empty;
    public ShareTransferType RawTransferType { get; set; }
    public Guid ShareTypeId { get; set; }
    public string ShareTypeTitle { get; set; } = string.Empty;
    public int FromNo { get; set; }
    public int ToNo { get; set; }
    public int NoOfShares { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public Guid ShareTransferId { get; set; }
}

public class ShareLedgerReportDto
{
    public DateTime Date { get; set; }
    public Guid? ShareholderId { get; set; }
    public string ShareholderTitle { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public List<ShareLedgerRowDto> Rows { get; set; } = new();
    public int TotalNoOfShares { get; set; }
    public decimal TotalAmount { get; set; }
}
