using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// BisectNode — Represents an interval node in the binary tree for accounting statement bisection.
/// Maps to ERPNext accounts/doctype/bisect_nodes.
/// </summary>
public class BisectNode : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid BisectAccountingStatementsId { get; set; }
    public Guid? ParentNodeId { get; set; }
    public Guid? LeftChildId { get; set; }
    public Guid? RightChildId { get; set; }
    public DateTime PeriodFromDate { get; set; }
    public DateTime PeriodToDate { get; set; }
    public decimal PlSummary { get; set; }
    public decimal BsSummary { get; set; }
    public decimal Difference { get; set; }
    public bool IsGenerated { get; set; }

    protected BisectNode() { }

    public BisectNode(
        Guid id,
        Guid bisectAccountingStatementsId,
        DateTime periodFromDate,
        DateTime periodToDate,
        Guid? parentNodeId = null,
        Guid? tenantId = null)
        : base(id)
    {
        BisectAccountingStatementsId = bisectAccountingStatementsId;
        PeriodFromDate = periodFromDate.Date;
        PeriodToDate = periodToDate.Date;
        ParentNodeId = parentNodeId;
        TenantId = tenantId;
    }

    public void SetSummary(decimal plSummary, decimal bsSummary)
    {
        PlSummary = plSummary;
        BsSummary = bsSummary;
        Difference = Math.Abs(plSummary - bsSummary);
        IsGenerated = true;
    }
}
