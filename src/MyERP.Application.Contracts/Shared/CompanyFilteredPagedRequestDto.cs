using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Shared;

/// <summary>
/// Base request DTO for paginated list endpoints with optional company filtering.
/// When CompanyId is provided, only records for that company are returned.
/// Without CompanyId, all records for the current tenant are returned (backwards compatible).
/// </summary>
public class CompanyFilteredPagedRequestDto : PagedAndSortedResultRequestDto
{
    /// <summary>
    /// Optional company filter. When null, returns data across all companies in the tenant.
    /// </summary>
    public Guid? CompanyId { get; set; }

    /// <summary>
    /// Optional text search filter. Applied to document number, party name, or reference fields.
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// Optional document status filter (e.g., "Draft", "Submitted", "Posted", "ToDeliverAndBill").
    /// </summary>
    public string? Status { get; set; }
}
