using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class ProcessDeferredAccountingDto : FullAuditedEntityDto<Guid>
{
    public string ProcessNumber { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public DeferredAccountingType Type { get; set; }
    public Guid? AccountId { get; set; }
    public string? AccountName { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsSubmitted { get; set; }
    public bool IsCancelled { get; set; }
    public int EntriesProcessed { get; set; }
}

public class CreateProcessDeferredAccountingDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public DeferredAccountingType Type { get; set; }

    public Guid? AccountId { get; set; }

    [Required]
    public DateTime PostingDate { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}

public class UpdateProcessDeferredAccountingDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public DeferredAccountingType Type { get; set; }

    public Guid? AccountId { get; set; }

    [Required]
    public DateTime PostingDate { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}

public class ProcessDeferredAccountingGetListInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
    public DeferredAccountingType? Type { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
