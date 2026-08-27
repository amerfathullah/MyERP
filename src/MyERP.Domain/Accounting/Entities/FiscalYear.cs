using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Fiscal year definition for a company.
/// Maps to ERPNext accounts/doctype/fiscal_year.
/// </summary>
public class FiscalYear : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Name { get; private set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }

    /// <summary>
    /// Short Fiscal Year flag — when true, bypasses the standard 365/366-day length rule.
    /// Maps to ERPNext is_short_year.
    /// </summary>
    public bool IsShortYear { get; set; }

    protected FiscalYear() { }

    public FiscalYear(Guid id, Guid companyId, string name, DateTime startDate, DateTime endDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        SetName(name);
        StartDate = startDate;
        EndDate = endDate;
        IsShortYear = false;
        TenantId = tenantId;
    }

    public FiscalYear(Guid id, Guid companyId, string name, DateTime startDate, DateTime endDate, bool isShortYear, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        SetName(name);
        StartDate = startDate;
        EndDate = endDate;
        IsShortYear = isShortYear;
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), FiscalYearConsts.MaxNameLength);
    }
}
