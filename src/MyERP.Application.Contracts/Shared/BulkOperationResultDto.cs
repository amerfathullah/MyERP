using System;
using System.Collections.Generic;

namespace MyERP.Shared;

/// <summary>
/// Result DTO for bulk operations (submit, post, cancel).
/// Reports per-item success/failure for proper UX feedback.
/// </summary>
public class BulkOperationResultDto
{
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Total => Succeeded + Failed;
    public List<BulkOperationError> Errors { get; set; } = new();
}

public class BulkOperationError
{
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
