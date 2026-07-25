using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

/// <summary>
/// Checks for existing draft documents before document conversion.
/// Per ERPNext PR #57299: warns when a draft linked document already exists.
/// </summary>
public interface IDraftLinkGuardAppService : IApplicationService
{
    /// <summary>
    /// Gets existing draft documents of targetDocType that link to the source document.
    /// Returns empty list if no drafts exist (safe to proceed).
    /// </summary>
    Task<List<DraftLinkDto>> GetExistingDraftsAsync(string sourceDocType, Guid sourceId, string targetDocType);
}

/// <summary>
/// Represents a draft document that already exists for a source→target conversion path.
/// </summary>
public class DraftLinkDto
{
    public Guid DocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? Url { get; set; }
}
