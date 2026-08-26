using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Automation;

public class BulkTransactionLogDto : FullAuditedEntityDto<Guid>
{
    public string Title { get; set; } = null!;
    public DateTime BatchDate { get; set; }
    public int TotalEntries { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public List<BulkTransactionLogDetailDto> Details { get; set; } = new();
}

public class BulkTransactionLogDetailDto : FullAuditedEntityDto<Guid>
{
    public Guid BulkTransactionLogId { get; set; }
    public string TransactionName { get; set; } = null!;
    public string FromDocType { get; set; } = null!;
    public string ToDocType { get; set; } = null!;
    public BulkTransactionStatus Status { get; set; }
    public string? ErrorDescription { get; set; }
    public DateTime? ExecutedTime { get; set; }
    public int RetriedCount { get; set; }
}

public class CreateBulkTransactionLogDto
{
    [Required]
    [StringLength(BulkTransactionConsts.MaxTitleLength)]
    public string Title { get; set; } = null!;

    public DateTime BatchDate { get; set; } = DateTime.UtcNow;

    public List<CreateBulkTransactionLogDetailDto> Details { get; set; } = new();
}

public class CreateBulkTransactionLogDetailDto
{
    [Required]
    [StringLength(BulkTransactionConsts.MaxTransactionNameLength)]
    public string TransactionName { get; set; } = null!;

    [Required]
    [StringLength(BulkTransactionConsts.MaxDocTypeLength)]
    public string FromDocType { get; set; } = null!;

    [Required]
    [StringLength(BulkTransactionConsts.MaxDocTypeLength)]
    public string ToDocType { get; set; } = null!;
}

public class RecordBulkTransactionResultDto
{
    public bool IsSuccess { get; set; }
    public string? ErrorDescription { get; set; }
}

public class GetBulkTransactionLogListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
