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

    protected FiscalYear() { }

    public FiscalYear(Guid id, Guid companyId, string name, DateTime startDate, DateTime endDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        SetName(name);
        StartDate = startDate;
        EndDate = endDate;
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), FiscalYearConsts.MaxNameLength);
    }
}
