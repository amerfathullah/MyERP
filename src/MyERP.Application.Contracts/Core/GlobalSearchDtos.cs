using System;

namespace MyERP.Core;

public class GlobalSearchInput
{
    public string Query { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public int MaxResults { get; set; } = 20;
}

public class SearchResultDto
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!;
    public string Route { get; set; } = null!;
}
