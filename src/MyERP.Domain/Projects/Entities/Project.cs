using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Projects.Entities;

public class Project : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string ProjectNumber { get; set; } = null!;
    public string ProjectName { get; set; } = null!;
    public ProjectStatus Status { get; private set; }
    public ProjectPriority Priority { get; set; }
    public PercentCompleteMethod PercentCompleteMethod { get; set; }
    public decimal PercentComplete { get; private set; }

    public Guid CompanyId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? ProjectTypeId { get; set; }

    public DateTime? ExpectedStartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    // Costing
    public decimal EstimatedCost { get; set; }
    public decimal TotalCostingAmount { get; set; }
    public decimal TotalBillingAmount { get; set; }
    public decimal TotalBilledAmount { get; set; }
    public decimal TotalConsumedMaterialCost { get; set; }

    public string? Notes { get; set; }
    public string? CostCenter { get; set; }
    public Guid? CostCenterId { get; set; }

    /// <summary>Whether to periodically collect progress updates from team members via email.</summary>
    public bool CollectProgress { get; set; }

    /// <summary>Email subject for progress collection (defaults to "For project {ProjectName}, update your status"). ERPNext PR #48368 / commit 407fdab487.</summary>
    public string? Subject { get; set; }

    /// <summary>Email message / instructions for progress collection.</summary>
    public string? Message { get; set; }

    public List<ProjectTask> Tasks { get; private set; } = new();

    /// <summary>Team members assigned to this project, with per-member access flags.</summary>
    public ICollection<ProjectUser> Users { get; private set; } = new List<ProjectUser>();

    protected Project() { }

    public Project(Guid id, Guid companyId, string projectNumber, string projectName, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ProjectNumber = projectNumber;
        ProjectName = projectName;
        Status = ProjectStatus.Open;
        Priority = ProjectPriority.Medium;
        PercentCompleteMethod = PercentCompleteMethod.TaskCompletion;
        TenantId = tenantId;
    }

    public void UpdateProgress() => UpdateProgress(Tasks);

    /// <summary>
    /// Recalculates PercentComplete from an explicit task list rather than the <see cref="Tasks"/>
    /// navigation. Callers that just inserted/deleted a task in the same unit of work should query
    /// tasks fresh via the task repository and pass them here — the <see cref="Tasks"/> AutoInclude
    /// navigation has been observed (confirmed on both SQLite and PostgreSQL) to still reflect the
    /// pre-mutation state immediately after a same-request task delete, even though a direct
    /// repository query for the same tasks correctly reflects it.
    /// </summary>
    public void UpdateProgress(IReadOnlyList<ProjectTask> currentTasks)
    {
        if (currentTasks.Count == 0)
        {
            PercentComplete = 0;
            return;
        }

        PercentComplete = PercentCompleteMethod switch
        {
            PercentCompleteMethod.TaskCompletion => CalculateByTaskCompletion(currentTasks),
            PercentCompleteMethod.TaskProgress => CalculateByTaskProgress(currentTasks),
            PercentCompleteMethod.TaskWeight => CalculateByTaskWeight(currentTasks),
            _ => PercentComplete, // Manual — don't change
        };

        if (PercentComplete >= 100 && Status == ProjectStatus.Open)
        {
            Status = ProjectStatus.Completed;
        }
    }

    public void Complete()
    {
        Status = ProjectStatus.Completed;
        PercentComplete = 100;
        ActualEndDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Manually set percent complete (only allowed when PercentCompleteMethod == Manual).
    /// Per ERPNext PR #57274: must be between 0 and 100; auto-sets to 100 when Completed.
    /// </summary>
    public void SetPercentComplete(decimal value)
    {
        if (PercentCompleteMethod != PercentCompleteMethod.Manual)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("reason", "Percent complete can only be set manually when method is Manual");

        if (Status == ProjectStatus.Completed)
        {
            PercentComplete = 100;
            return;
        }

        if (value < 0 || value > 100)
            throw new BusinessException("MyERP:13003")
                .WithData("value", value);

        PercentComplete = value;
    }

    public void Cancel()
    {
        if (Status == ProjectStatus.Completed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProjectStatus.Cancelled;
    }

    public void Reopen()
    {
        if (Status != ProjectStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProjectStatus.Open;
    }

    public decimal GrossMargin =>
        TotalBilledAmount - TotalCostingAmount;

    private static decimal CalculateByTaskCompletion(IReadOnlyList<ProjectTask> tasks)
    {
        var total = tasks.Count;
        var completed = 0;
        foreach (var t in tasks)
        {
            if (t.Status is ProjectTaskStatus.Completed or ProjectTaskStatus.Cancelled)
                completed++;
        }
        return total > 0 ? Math.Round((decimal)completed / total * 100, 1) : 0;
    }

    private static decimal CalculateByTaskProgress(IReadOnlyList<ProjectTask> tasks)
    {
        var total = tasks.Count;
        var sum = 0m;
        foreach (var t in tasks)
            sum += t.Progress;
        return total > 0 ? Math.Round(sum / total, 1) : 0;
    }

    private static decimal CalculateByTaskWeight(IReadOnlyList<ProjectTask> tasks)
    {
        var totalWeight = 0m;
        var weightedProgress = 0m;
        foreach (var t in tasks)
        {
            var w = t.TaskWeight > 0 ? t.TaskWeight : 1;
            totalWeight += w;
            weightedProgress += t.Progress * w;
        }
        return totalWeight > 0 ? Math.Round(weightedProgress / totalWeight, 1) : 0;
    }

    /// <summary>
    /// Resolves the effective project ID for a child row (Sales Invoice Item, Purchase Invoice Item, or Sales Order Item),
    /// falling back to the document header's <paramref name="headerProjectId"/> when the row's <paramref name="rowProjectId"/>
    /// is null or empty per ERPNext commits e1ed14c303, 4b368ed5d4, and fdf421605d.
    /// </summary>
    public static Guid? ResolveEffectiveRowProjectId(Guid? rowProjectId, Guid? headerProjectId)
    {
        if (rowProjectId.HasValue && rowProjectId.Value != Guid.Empty)
        {
            return rowProjectId.Value;
        }

        if (headerProjectId.HasValue && headerProjectId.Value != Guid.Empty)
        {
            return headerProjectId.Value;
        }

        return null;
    }

    /// <summary>
    /// Computes the directly expensed non-stock purchase amount of a Purchase Invoice row for Project costing
    /// after applying the invoice's Landed Cost Voucher claim (<paramref name="claimedLandedCostAmount"/>)
    /// against valuation rows (<c>Expenses Included In Valuation</c>) first
    /// per ERPNext commits 31689c27ba and b2bf715e2d (<c>project/costing/queries.py</c>).
    /// </summary>
    public static decimal CalculateNonStockPurchaseRowExpensedAmount(
        decimal rowBaseNetAmount,
        string? expenseAccountType,
        bool isStockItem,
        bool deliveredBySupplier,
        bool isSubcontracted,
        decimal invoiceBaseNetTotal,
        decimal claimedLandedCostAmount,
        decimal valuationRowsBaseNetTotal)
    {
        if (isSubcontracted)
        {
            return 0m;
        }

        var neverStocked = !isStockItem || deliveredBySupplier;
        if (!neverStocked)
        {
            return 0m;
        }

        if (string.Equals(expenseAccountType, "Expenses Included In Valuation", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        if (claimedLandedCostAmount > 0m && claimedLandedCostAmount >= invoiceBaseNetTotal)
        {
            return 0m;
        }

        decimal expensedShare = 1m;
        if (claimedLandedCostAmount > 0m && claimedLandedCostAmount > valuationRowsBaseNetTotal)
        {
            var nonValuationTotal = invoiceBaseNetTotal - valuationRowsBaseNetTotal;
            expensedShare = nonValuationTotal != 0m
                ? (invoiceBaseNetTotal - claimedLandedCostAmount) / nonValuationTotal
                : 1m;
        }

        return rowBaseNetAmount * expensedShare;
    }

    /// <summary>
    /// Collects the distinct effective project IDs affected by a Purchase Invoice for project purchase-cost
    /// refresh per ERPNext PR #68323 (<c>PurchaseInvoice.update_project</c> in <c>purchase_invoice.py</c>):
    /// each row falls back to the header <paramref name="invoiceProjectId"/> when the row's project is blank,
    /// and empty/null project IDs are excluded.
    /// </summary>
    public static IReadOnlySet<Guid> CollectEffectivePurchaseInvoiceProjectIds(
        Guid? invoiceProjectId,
        IEnumerable<Guid?>? itemProjectIds)
    {
        var projects = new HashSet<Guid>();
        if (itemProjectIds == null)
        {
            return projects;
        }

        var headerId = invoiceProjectId.HasValue && invoiceProjectId.Value != Guid.Empty
            ? invoiceProjectId
            : null;

        foreach (var rowProjectId in itemProjectIds)
        {
            var effective = rowProjectId.HasValue && rowProjectId.Value != Guid.Empty
                ? rowProjectId
                : headerId;

            if (effective.HasValue)
            {
                projects.Add(effective.Value);
            }
        }

        return projects;
    }

    /// <summary>
    /// Computes a Project's <c>TotalSalesAmount</c> by summing submitted Sales Order row base net amounts
    /// whose effective project (<see cref="ResolveEffectiveRowProjectId"/>) equals <paramref name="projectId"/>
    /// per ERPNext PR #68326 (<c>fix(projects): split total sales amount by sales order row project</c>).
    /// </summary>
    public static decimal CalculateProjectTotalSalesAmountByRowProject(
        Guid projectId,
        IEnumerable<(bool IsSubmitted, Guid? HeaderProjectId, IEnumerable<(Guid? RowProjectId, decimal BaseNetAmount)> Rows)>? salesOrders,
        int precision = 2)
    {
        if (projectId == Guid.Empty || salesOrders == null)
        {
            return 0m;
        }

        decimal total = 0m;
        foreach (var order in salesOrders)
        {
            if (!order.IsSubmitted || order.Rows == null)
            {
                continue;
            }

            foreach (var row in order.Rows)
            {
                if (ResolveEffectiveRowProjectId(row.RowProjectId, order.HeaderProjectId) == projectId)
                {
                    total += row.BaseNetAmount;
                }
            }
        }

        return Math.Round(total, precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Collects all distinct project IDs affected when a submitted Sales Order's header or row projects
    /// are updated after submit per ERPNext PR #68326 (<c>SalesOrder.on_update_after_submit</c>):
    /// returns the union of previous and current effective project IDs so both old and new projects recalculate.
    /// </summary>
    public static IReadOnlySet<Guid> CollectAffectedProjectsOnSalesOrderUpdate(
        Guid? previousHeaderProjectId,
        IEnumerable<Guid?>? previousRowProjectIds,
        Guid? currentHeaderProjectId,
        IEnumerable<Guid?>? currentRowProjectIds)
    {
        var affected = new HashSet<Guid>();
        foreach (var id in CollectEffectivePurchaseInvoiceProjectIds(previousHeaderProjectId, previousRowProjectIds))
        {
            affected.Add(id);
        }

        foreach (var id in CollectEffectivePurchaseInvoiceProjectIds(currentHeaderProjectId, currentRowProjectIds))
        {
            affected.Add(id);
        }

        return affected;
    }

    /// <summary>
    /// Computes billed Sales Invoice amounts for a project where rows with a cleared/blank project string
    /// (empty, whitespace, or <see cref="Guid.Empty"/>) fall back to the invoice header project
    /// per ERPNext PR #68325 (<c>ProjectCostQueries.billed_items</c>: <c>NullIf(item.project, "").isnull()</c>).
    /// </summary>
    public static decimal CalculateProjectBilledAmountWithBlankRowFallback(
        Guid projectId,
        IEnumerable<(bool IsSubmitted, Guid? HeaderProjectId, IEnumerable<(string? RawRowProjectId, decimal BaseNetAmount)> Rows)>? salesInvoices,
        int precision = 2)
    {
        if (projectId == Guid.Empty || salesInvoices == null)
        {
            return 0m;
        }

        decimal total = 0m;
        foreach (var invoice in salesInvoices)
        {
            if (!invoice.IsSubmitted || invoice.Rows == null)
            {
                continue;
            }

            foreach (var row in invoice.Rows)
            {
                Guid? parsedRowProject = null;
                if (!string.IsNullOrWhiteSpace(row.RawRowProjectId)
                    && Guid.TryParse(row.RawRowProjectId.Trim(), out var parsed)
                    && parsed != Guid.Empty)
                {
                    parsedRowProject = parsed;
                }

                if (ResolveEffectiveRowProjectId(parsedRowProject, invoice.HeaderProjectId) == projectId)
                {
                    total += row.BaseNetAmount;
                }
            }
        }

        return Math.Round(total, precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Builds the Project Billing Card Sales Orders summary per ERPNext PR #68327
    /// (<c>ProjectBilling.build</c> / <c>sales_orders</c> / <c>order_filter</c> in <c>project/costing/billing.py</c>):
    /// - Computes each submitted Sales Order's contract amount contributed to <paramref name="projectId"/>
    ///   (from both header-attributed rows and row-attributed rows), ordered newest first (<c>TransactionDate desc, OrderNumber desc</c>).
    /// - Sets <c>OrderFilter</c> to <c>"HeaderProject"</c> when every order names the project on its header,
    ///   <c>"RowProject"</c> when every order names the project on its rows, or <c>null</c> for a mix.
    /// - Truncates <c>ListedOrders</c> to <paramref name="listLimit"/> ONLY when <c>OrderFilter</c> is non-null;
    ///   when mixed (<c>OrderFilter == null</c>), lists every order so no order is hidden behind an unusable filter link.
    /// </summary>
    public static ProjectSalesOrderBillingCardResult BuildProjectSalesOrderBillingCard(
        Guid projectId,
        IEnumerable<ProjectBillingSalesOrderInput>? salesOrders,
        int listLimit = 5,
        int precision = 2)
    {
        if (projectId == Guid.Empty || salesOrders == null)
        {
            return new ProjectSalesOrderBillingCardResult(
                Array.Empty<ProjectBillingSalesOrderSummary>(),
                0,
                OrderFilter: null);
        }

        var itemOrderedOrderIds = new HashSet<Guid>();
        var matched = new List<(ProjectBillingSalesOrderInput Order, decimal Amount)>();

        foreach (var order in salesOrders)
        {
            if (!order.IsSubmitted || order.Rows == null)
            {
                continue;
            }

            decimal fromOrder = 0m;
            decimal fromItems = 0m;
            bool hasRowMatch = false;

            foreach (var row in order.Rows)
            {
                if (row.RowProjectId.HasValue && row.RowProjectId.Value != Guid.Empty)
                {
                    if (row.RowProjectId.Value == projectId)
                    {
                        fromItems += row.BaseNetAmount;
                        hasRowMatch = true;
                    }
                }
                else if (order.HeaderProjectId == projectId)
                {
                    fromOrder += row.BaseNetAmount;
                }
            }

            if (hasRowMatch)
            {
                itemOrderedOrderIds.Add(order.OrderId);
            }

            var combinedAmount = Math.Round(fromOrder + fromItems, precision, MidpointRounding.AwayFromZero);
            if (hasRowMatch || fromOrder != 0m)
            {
                matched.Add((order, combinedAmount));
            }
        }

        var ordered = matched
            .OrderByDescending(m => m.Order.TransactionDate)
            .ThenByDescending(m => m.Order.OrderNumber, StringComparer.Ordinal)
            .Select(m => new ProjectBillingSalesOrderSummary(
                m.Order.OrderId,
                m.Order.OrderNumber,
                m.Order.TransactionDate,
                m.Order.HeaderProjectId,
                m.Amount))
            .ToList();

        string? orderFilter = null;
        if (ordered.Count > 0)
        {
            if (ordered.All(o => o.HeaderProjectId == projectId))
            {
                orderFilter = "HeaderProject";
            }
            else if (ordered.All(o => itemOrderedOrderIds.Contains(o.OrderId)))
            {
                orderFilter = "RowProject";
            }
        }

        var listedOrders = orderFilter != null && listLimit > 0
            ? ordered.Take(listLimit).ToList()
            : ordered;

        return new ProjectSalesOrderBillingCardResult(listedOrders, ordered.Count, orderFilter);
    }

    /// <summary>
    /// Restricts custom <c>Employee</c> permission rules on <c>Project</c> to select-only when the rule
    /// is at <c>PermLevel == 0</c> and does not grant <c>Read</c>, clearing rights that would otherwise
    /// expose Project records (<c>Print</c>, <c>Email</c>, <c>Export</c>, <c>Report</c>, <c>Share</c>)
    /// per ERPNext PR #66329 (<c>restrict_employee_project_perms_to_select.py</c>).
    /// </summary>
    public static (IReadOnlyList<ProjectRolePermissionRule> UpdatedRules, bool AnyChanged)
        RestrictNonReadEmployeeProjectPermissionsToSelect(
            IEnumerable<ProjectRolePermissionRule>? rules)
    {
        if (rules == null)
        {
            return (Array.Empty<ProjectRolePermissionRule>(), false);
        }

        var updated = new List<ProjectRolePermissionRule>();
        var anyChanged = false;

        foreach (var rule in rules)
        {
            var isTarget = string.Equals(rule.Role, "Employee", StringComparison.OrdinalIgnoreCase)
                && rule.PermLevel == 0
                && !rule.Read;

            if (isTarget && (rule.Print || rule.Email || rule.Export || rule.Report || rule.Share))
            {
                updated.Add(rule with
                {
                    Print = false,
                    Email = false,
                    Export = false,
                    Report = false,
                    Share = false,
                });
                anyChanged = true;
            }
            else
            {
                updated.Add(rule);
            }
        }

        return (updated, anyChanged);
    }
}

public sealed record ProjectRolePermissionRule(
    string Role,
    int PermLevel,
    bool Select,
    bool Read,
    bool Print,
    bool Email,
    bool Export,
    bool Report,
    bool Share);

public sealed record ProjectBillingSalesOrderInput(
    Guid OrderId,
    string OrderNumber,
    DateTime TransactionDate,
    bool IsSubmitted,
    Guid? HeaderProjectId,
    IReadOnlyList<(Guid? RowProjectId, decimal BaseNetAmount)> Rows);

public sealed record ProjectBillingSalesOrderSummary(
    Guid OrderId,
    string OrderNumber,
    DateTime TransactionDate,
    Guid? HeaderProjectId,
    decimal Amount);

public sealed record ProjectSalesOrderBillingCardResult(
    IReadOnlyList<ProjectBillingSalesOrderSummary> ListedOrders,
    int TotalOrderCount,
    string? OrderFilter);
