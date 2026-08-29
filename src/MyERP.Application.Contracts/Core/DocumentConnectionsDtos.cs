using System;
using System.Collections.Generic;

namespace MyERP.Core;

public class DocumentConnectionsDto
{
    public List<ConnectionGroupDto> Groups { get; set; } = new();
}

public class ConnectionGroupDto
{
    public string Label { get; set; } = null!;
    public List<ConnectionItemDto> Items { get; set; } = new();
}

public class ConnectionItemDto
{
    public string DocumentType { get; set; } = null!;
    public int Count { get; set; }
    public string Route { get; set; } = null!;
    public List<ConnectionDocumentDto> Documents { get; set; } = new();
}

public class ConnectionDocumentDto
{
    public Guid Id { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Status { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? Date { get; set; }
    public string Route { get; set; } = null!;
}

/// <summary>
/// Draft linked document found for a source document (per ERPNext PR #57299 / get_existing_drafts).
/// </summary>
public class ExistingDraftDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = null!;
    public string TargetDocType { get; set; } = null!;
    public decimal? Amount { get; set; }
    public DateTime? Date { get; set; }
}
