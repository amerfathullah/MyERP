using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class BankStatementImportInput
{
    public Guid CompanyId { get; set; }
    public Guid BankAccountId { get; set; }
    public string CsvContent { get; set; } = null!;
    public Guid? TenantId { get; set; }
    public string? CurrencyCode { get; set; }
}

public class BankStatementImportResult
{
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0 || ImportedCount > 0;
}

public class Mt940ImportInput
{
    public Guid CompanyId { get; set; }
    public Guid BankAccountId { get; set; }
    public string Mt940Content { get; set; } = null!;
    public Guid? TenantId { get; set; }
    public string? CurrencyCode { get; set; }
}
