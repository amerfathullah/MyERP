using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.EInvoice;

public class EInvoiceSubmissionDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string? SubmissionUid { get; set; }
    public string? DocumentUuid { get; set; }
    public string? LongId { get; set; }
    public string SourceDocumentType { get; set; } = null!;
    public Guid SourceDocumentId { get; set; }
    public string DocumentTypeCode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? Reason { get; set; }
    public string? QrCodeUrl { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

public class SubmitEInvoiceDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required][StringLength(50)] public string SourceDocumentType { get; set; } = null!;
    [Required] public Guid SourceDocumentId { get; set; }
    [StringLength(5)] public string DocumentTypeCode { get; set; } = "01";
}

public class CancelEInvoiceDto
{
    [Required] public Guid SubmissionId { get; set; }
    [Required][StringLength(500)] public string Reason { get; set; } = null!;
}

public class BatchSubmitEInvoiceDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public string SourceDocumentType { get; set; } = "SalesInvoice";
    [Required] public List<Guid> DocumentIds { get; set; } = new();
}

public class BatchSubmitResultDto
{
    public int TotalRequested { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<BatchSubmitItemResult> Results { get; set; } = new();
}

public class BatchSubmitItemResult
{
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; } = null!;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? LhdnUuid { get; set; }
    public string? Status { get; set; }
}

public class ConsolidateInvoicesDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public List<Guid> InvoiceIds { get; set; } = new();
}

public class SearchTaxpayerDto
{
    [Required]
    [StringLength(20)]
    public string IdType { get; set; } = "BRN";

    [Required]
    [StringLength(50)]
    public string IdValue { get; set; } = null!;
}

public class LhdnStatusReportRequestDto
{
    public Guid? CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
}

public class LhdnStatusReportItemDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public string PartyName { get; set; } = null!;
    public decimal GrandTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public string Status { get; set; } = null!;
    public string? DocumentUuid { get; set; }
    public string? QrCodeUrl { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

public class LhdnVatReportRequestDto
{
    public Guid? CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class LhdnVatCategorySummaryDto
{
    public string CategoryCode { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal Adjustment { get; set; }
    public decimal VatAmount { get; set; }
}

public class LhdnVatReportDto
{
    public List<LhdnVatCategorySummaryDto> SalesCategories { get; set; } = new();
    public List<LhdnVatCategorySummaryDto> PurchaseCategories { get; set; } = new();
    public decimal TotalSalesAmount { get; set; }
    public decimal TotalSalesAdjustment { get; set; }
    public decimal TotalSalesVat { get; set; }
    public decimal TotalPurchaseAmount { get; set; }
    public decimal TotalPurchaseAdjustment { get; set; }
    public decimal TotalPurchaseVat { get; set; }
    public decimal NetVatPayable { get; set; }
}

public class LhdnDashboardStatsDto
{
    public int SalesValid { get; set; }
    public int SalesInvalid { get; set; }
    public int SalesSubmitted { get; set; }
    public int SalesCancelled { get; set; }
    public int SalesFailed { get; set; }
    public int SalesNotSubmitted { get; set; }

    public int PurchaseValid { get; set; }
    public int PurchaseInvalid { get; set; }
    public int PurchaseSubmitted { get; set; }
    public int PurchaseCancelled { get; set; }
    public int PurchaseFailed { get; set; }
    public int PurchaseNotSubmitted { get; set; }
}

public class LhdnMonthlyTrendDto
{
    public string Month { get; set; } = string.Empty;
    public int Valid { get; set; }
    public int Invalid { get; set; }
    public int Submitted { get; set; }
}

public class GetConsolidationCandidatesInputDto
{
    public Guid? CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public decimal? MaxAmount { get; set; }
}

public class ConsolidationCandidateDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public decimal GrandTotal { get; set; }
    public int ItemCount { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
    public bool IsEligible { get; set; } = true;
}

public class EInvoiceConsolidationDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid ConsolidatedInvoiceId { get; set; }
    public string ConsolidatedInvoiceNumber { get; set; } = null!;
    public DateTime ConsolidatedIssueDate { get; set; }
    public decimal ConsolidatedGrandTotal { get; set; }
    public string? LhdnUuid { get; set; }
    public string? EInvoiceStatus { get; set; }
    public string? QrCodeUrl { get; set; }
    public List<ConsolidationCandidateDto> OriginalInvoices { get; set; } = new();
    public DateTime CreationTime { get; set; }
}

public class GetConsolidationsInputDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class LhdnSuccessLogDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid SubmissionId { get; set; }
    public string DocumentUuid { get; set; } = null!;
    public string? LongId { get; set; }
    public string SourceDocumentType { get; set; } = null!;
    public Guid SourceDocumentId { get; set; }
    public string? SourceDocumentNumber { get; set; }
    public string DocumentTypeCode { get; set; } = "01";
    public DateTime SubmittedAt { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string? ResponseJson { get; set; }
    public string? QrCodeUrl { get; set; }
    public decimal GrandTotal { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
}

public class GetLhdnSuccessLogsInputDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public string? SourceDocumentType { get; set; }
    public string? SearchFilter { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
