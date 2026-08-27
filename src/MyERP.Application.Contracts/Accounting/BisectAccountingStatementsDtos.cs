using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class BisectNodeDto : FullAuditedEntityDto<Guid>
{
    public Guid BisectAccountingStatementsId { get; set; }
    public Guid? ParentNodeId { get; set; }
    public Guid? LeftChildId { get; set; }
    public Guid? RightChildId { get; set; }
    public DateTime PeriodFromDate { get; set; }
    public DateTime PeriodToDate { get; set; }
    public decimal PlSummary { get; set; }
    public decimal BsSummary { get; set; }
    public decimal Difference { get; set; }
    public bool IsGenerated { get; set; }
}

public class BisectAccountingStatementsDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public BisectAlgorithm Algorithm { get; set; }
    public Guid? CurrentNodeId { get; set; }
    public DateTime? CurrentFromDate { get; set; }
    public DateTime? CurrentToDate { get; set; }
    public decimal PlSummary { get; set; }
    public decimal BsSummary { get; set; }
    public decimal Difference { get; set; }
    public List<BisectNodeDto> Nodes { get; set; } = new();
}

public class CreateBisectAccountingStatementsDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public DateTime FromDate { get; set; }

    [Required]
    public DateTime ToDate { get; set; }

    public BisectAlgorithm Algorithm { get; set; } = BisectAlgorithm.BFS;
}

public class BisectAccountingStatementsGetListInput : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
