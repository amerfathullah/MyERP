using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class RepostGlDto
{
    public Guid CompanyId { get; set; }
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
}

public class RepostBatchGlDto
{
    public Guid CompanyId { get; set; }
    public List<RepostVoucherRefDto> Vouchers { get; set; } = [];
}

public class RepostVoucherRefDto
{
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
}

public class GlRepostResultDto
{
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalProcessed { get; set; }
    public bool HasErrors { get; set; }
    public List<string> Errors { get; set; } = [];
}

public class GlRepostHistoryDto
{
    public Guid Id { get; set; }
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
    public string? VoucherNumber { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime RepostedAt { get; set; }
    public string RepostedBy { get; set; } = null!;
}
