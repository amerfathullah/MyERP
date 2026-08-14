using System;
using System.Collections.Generic;

namespace MyERP.Core;

public class CompanyRestrictionDto
{
    public string ParentType { get; set; } = null!;
    public Guid ParentId { get; set; }
    public bool RestrictToCompanies { get; set; }
    public List<CompanyRestrictionEntryDto> AllowedCompanies { get; set; } = new();
}

public class CompanyRestrictionEntryDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
}

public class SaveCompanyRestrictionDto
{
    public string ParentType { get; set; } = null!;
    public Guid ParentId { get; set; }
    public bool RestrictToCompanies { get; set; }
    public List<Guid>? AllowedCompanyIds { get; set; }
}
