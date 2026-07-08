using System;
using System.ComponentModel.DataAnnotations;
using MyERP.ImportExport;
using Volo.Abp.Application.Dtos;

namespace MyERP.ImportExport.DTOs;

public class ImportJobDto : EntityDto<Guid>
{
    public string FileName { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public ImportStatus Status { get; set; }
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string? ErrorDetails { get; set; }
    public Guid? CompanyId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreationTime { get; set; }
}

public class StartImportDto
{
    [Required]
    [StringLength(ImportJobConsts.MaxEntityTypeLength)]
    public string EntityType { get; set; } = null!;

    [Required]
    [StringLength(ImportJobConsts.MaxFileNameLength)]
    public string FileName { get; set; } = null!;

    /// <summary>Base64-encoded file content.</summary>
    [Required]
    public string FileContent { get; set; } = null!;

    public Guid? CompanyId { get; set; }
}

public class ExportRequestDto
{
    [Required]
    [StringLength(ImportJobConsts.MaxEntityTypeLength)]
    public string EntityType { get; set; } = null!;

    public ExportFormat Format { get; set; } = ExportFormat.Csv;

    public Guid? CompanyId { get; set; }

    /// <summary>Optional filter (JSON).</summary>
    public string? FilterJson { get; set; }
}

public class ExportResultDto
{
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    /// <summary>Base64-encoded file content.</summary>
    public string FileContent { get; set; } = null!;
}
