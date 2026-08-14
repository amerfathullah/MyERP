using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class ImportCoaDto
{
    public Guid CompanyId { get; set; }
    public List<ImportCoaRowDto> Rows { get; set; } = new();
}

public class ImportCoaRowDto
{
    public string AccountCode { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public AccountType AccountType { get; set; }
    public bool IsGroup { get; set; }
    public string? ParentCode { get; set; }
    public AccountSubType? SubType { get; set; }
}

public class CoaImportResultDto
{
    public int AccountsCreated { get; set; }
    public Guid CompanyId { get; set; }
}

public class CoaTemplateRowDto
{
    public string AccountCode { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public AccountType AccountType { get; set; }
    public bool IsGroup { get; set; }
    public string? ParentCode { get; set; }
    public AccountSubType? SubType { get; set; }
}
