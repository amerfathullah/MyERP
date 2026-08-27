using System;
using System.Collections.Generic;
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

public class PreviewDeferredAccountingInput
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public DeferredAccountingType Type { get; set; }

    public Guid? AccountId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}

public class DeferredAccountingPreviewItemDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string ItemDescription { get; set; } = null!;
    public DateTime ServiceStartDate { get; set; }
    public DateTime ServiceEndDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountToRecognize { get; set; }
    public Guid DeferredAccountId { get; set; }
    public DateTime PostingDate { get; set; }
}

public class DeferredAccountingPreviewDto
{
    public Guid CompanyId { get; set; }
    public DeferredAccountingType Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<DeferredAccountingPreviewItemDto> Items { get; set; } = new();
    public decimal TotalAmountToRecognize { get; set; }
    public int TotalInvoicesCount { get; set; }
}

public class ProcessDeferredAccountingSummaryDto
{
    public Guid Id { get; set; }
    public string ProcessNumber { get; set; } = null!;
    public bool IsSubmitted { get; set; }
    public bool IsCancelled { get; set; }
    public int EntriesProcessed { get; set; }
}
