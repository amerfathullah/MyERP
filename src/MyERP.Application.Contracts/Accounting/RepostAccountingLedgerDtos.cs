using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class RepostAccountingLedgerDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = null!;
    public string? ErrorLog { get; set; }
    public DateTime CreationTime { get; set; }
    public List<RepostAccountingLedgerVoucherDto> Vouchers { get; set; } = new();
}

public class RepostAccountingLedgerVoucherDto : EntityDto<Guid>
{
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
    public string VoucherNumber { get; set; } = null!;
    public int Status { get; set; }
    public string StatusName { get; set; } = null!;
    public string? ErrorMessage { get; set; }
}

public class CreateRepostAccountingLedgerDto
{
    public Guid CompanyId { get; set; }
    public List<RepostAccountingLedgerVoucherInputDto> Vouchers { get; set; } = new();
}

public class RepostAccountingLedgerVoucherInputDto
{
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
}

/// <summary>A voucher the current user could add to a repost — resolved server-side from a
/// voucher-type + free-text search, restricted to already-Posted documents of an allowed type.</summary>
public class RepostableVoucherDto
{
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
    public string VoucherNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
}
