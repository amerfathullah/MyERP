using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.ImportExport.Entities;

/// <summary>
/// Tracks the status of an import job (CSV/Excel file processing).
/// Supports background processing with progress tracking.
/// </summary>
public class ImportJob : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Original file name uploaded by the user.</summary>
    public string FileName { get; set; } = null!;

    /// <summary>Entity type being imported (e.g., "Customer", "Item", "Account").</summary>
    public string EntityType { get; set; } = null!;

    /// <summary>Current import status.</summary>
    public ImportStatus Status { get; private set; } = ImportStatus.Pending;

    /// <summary>Total rows detected in the import file.</summary>
    public int TotalRows { get; set; }

    /// <summary>Rows successfully imported.</summary>
    public int SuccessCount { get; set; }

    /// <summary>Rows that failed to import.</summary>
    public int FailureCount { get; set; }

    /// <summary>Error details (JSON array of row-level errors).</summary>
    public string? ErrorDetails { get; set; }

    /// <summary>Blob storage reference for the uploaded file.</summary>
    public string? BlobReference { get; set; }

    /// <summary>Company context for the import.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>When processing started.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>When processing completed.</summary>
    public DateTime? CompletedAt { get; set; }

    protected ImportJob() { }

    public ImportJob(Guid id, string fileName, string entityType, Guid? tenantId = null) : base(id)
    {
        FileName = fileName;
        EntityType = entityType;
        TenantId = tenantId;
    }

    public void MarkProcessing()
    {
        Status = ImportStatus.Processing;
        StartedAt = DateTime.UtcNow;
    }

    public void MarkCompleted(int successCount, int failureCount, string? errorDetails = null)
    {
        SuccessCount = successCount;
        FailureCount = failureCount;
        ErrorDetails = errorDetails;
        CompletedAt = DateTime.UtcNow;
        Status = failureCount == 0 ? ImportStatus.Completed :
                 successCount > 0 ? ImportStatus.PartialSuccess :
                 ImportStatus.Failed;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = ImportStatus.Failed;
        ErrorDetails = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }
}
