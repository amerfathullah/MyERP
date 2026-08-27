using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// BisectAccountingStatements — Diagnostic tool to bisect date ranges and pinpoint P&L and Balance Sheet discrepancies.
/// Maps to ERPNext accounts/doctype/bisect_accounting_statements.
/// </summary>
public class BisectAccountingStatements : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public BisectAlgorithm Algorithm { get; set; }

    public Guid? CurrentNodeId { get; set; }
    public DateTime? CurrentFromDate { get; set; }
    public DateTime? CurrentToDate { get; set; }
    public decimal PlSummary { get; set; }
    public decimal BsSummary { get; set; }
    public decimal Difference { get; set; }

    private readonly List<BisectNode> _nodes = new();
    public virtual IReadOnlyCollection<BisectNode> Nodes => new ReadOnlyCollection<BisectNode>(_nodes);

    protected BisectAccountingStatements() { }

    public BisectAccountingStatements(
        Guid id,
        Guid companyId,
        DateTime fromDate,
        DateTime toDate,
        BisectAlgorithm algorithm = BisectAlgorithm.BFS,
        Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        FromDate = fromDate.Date;
        ToDate = toDate.Date;
        Algorithm = algorithm;
        TenantId = tenantId;

        ValidateDates();
    }

    public void ValidateDates()
    {
        if (FromDate > ToDate)
        {
            throw new BusinessException("MyERP:BisectAccountingStatements:FromDateGreaterThanToDate", "From Date cannot be greater than To Date.");
        }
    }

    public void SetCurrentNode(Guid nodeId, DateTime fromDate, DateTime toDate, decimal plSummary, decimal bsSummary)
    {
        CurrentNodeId = nodeId;
        CurrentFromDate = fromDate.Date;
        CurrentToDate = toDate.Date;
        PlSummary = plSummary;
        BsSummary = bsSummary;
        Difference = Math.Abs(plSummary - bsSummary);
    }

    public void AddNode(BisectNode node)
    {
        _nodes.Add(node);
    }

    public void ClearNodes()
    {
        _nodes.Clear();
    }
}
