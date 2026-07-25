using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Financial Report Template — configurable financial report builder (v16 feature).
/// Replaces hardcoded P&L/BS/CF report builders with template-driven formulas.
/// Features: formula engine with circular dependency detection, 6 standard IFRS templates,
/// reference code system for cross-line-item formulas.
/// Per gotcha #159: Account Category entity, formula safe_eval, topological sort via Kahn's algorithm.
/// Per gotcha #427: growth from zero = 100% (v16 behavior).
/// Per gotcha #428: PCV gap bridging for opening balances.
/// Per gotcha #430: multi-segment layout (Column Break / Section Break).
/// Per gotcha #431: Custom API data source.
/// </summary>
public class FinancialReportTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Template name (e.g., "Standard P&L", "IFRS Balance Sheet").</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Report type: ProfitAndLoss, BalanceSheet, CashFlow, Custom.</summary>
    public FinancialReportType ReportType { get; set; }

    /// <summary>Optional company scope (null = global, available to all companies).</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Whether this is a system-provided standard template (read-only).</summary>
    public bool IsStandard { get; set; }

    /// <summary>Whether template is active and available for selection.</summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>Optional description for admin display.</summary>
    public string? Description { get; set; }

    /// <summary>Template rows defining the report structure.</summary>
    public ICollection<FinancialReportRow> Rows { get; private set; } = new List<FinancialReportRow>();

    protected FinancialReportTemplate() { }

    public FinancialReportTemplate(Guid id, string name, FinancialReportType reportType) : base(id)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Name = name;
        ReportType = reportType;
    }

    public FinancialReportRow AddRow(
        string label,
        FinancialReportDataSource dataSource,
        int sortOrder,
        string? referenceCode = null,
        string? calculationFormula = null,
        string? accountCategoryFilter = null,
        string? customApiPath = null,
        bool hideWhenEmpty = false,
        bool isBold = false)
    {
        var row = new FinancialReportRow(
            Guid.NewGuid(),
            Id,
            label,
            dataSource,
            sortOrder,
            referenceCode,
            calculationFormula,
            accountCategoryFilter,
            customApiPath,
            hideWhenEmpty,
            isBold
        );
        Rows.Add(row);
        return row;
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;

    /// <summary>
    /// Validates formula dependencies for circular references using Kahn's algorithm (topological sort).
    /// Returns list of error messages (empty = valid).
    /// </summary>
    public IReadOnlyList<string> ValidateFormulas()
    {
        var errors = new List<string>();
        var formulaRows = Rows.Where(r => r.DataSource == FinancialReportDataSource.CalculatedAmount && !string.IsNullOrEmpty(r.CalculationFormula)).ToList();

        if (!formulaRows.Any()) return errors;

        // Build dependency graph from reference codes used in formulas
        var refCodeToRow = Rows.Where(r => !string.IsNullOrEmpty(r.ReferenceCode))
            .ToDictionary(r => r.ReferenceCode!, r => r.Id);

        var inDegree = new Dictionary<Guid, int>();
        var adjacency = new Dictionary<Guid, List<Guid>>();

        foreach (var row in formulaRows)
        {
            inDegree.TryAdd(row.Id, 0);
            adjacency.TryAdd(row.Id, new List<Guid>());
        }

        foreach (var row in formulaRows)
        {
            var deps = ExtractReferenceCodes(row.CalculationFormula!);
            foreach (var dep in deps)
            {
                if (refCodeToRow.TryGetValue(dep, out var depRowId) && depRowId != row.Id && inDegree.ContainsKey(depRowId))
                {
                    if (!adjacency.ContainsKey(depRowId))
                        adjacency[depRowId] = new List<Guid>();
                    adjacency[depRowId].Add(row.Id);
                    inDegree[row.Id] = inDegree.GetValueOrDefault(row.Id) + 1;
                }
            }
        }

        // Kahn's algorithm — topological sort
        var queue = new Queue<Guid>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var processed = 0;

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            processed++;
            if (adjacency.TryGetValue(node, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0) queue.Enqueue(neighbor);
                }
            }
        }

        if (processed < formulaRows.Count)
        {
            errors.Add("Circular dependency detected in formula references. Check reference codes used in calculated rows.");
        }

        return errors;
    }

    /// <summary>Extracts reference codes from a formula string (looks for identifiers that match existing ref codes).</summary>
    private static IEnumerable<string> ExtractReferenceCodes(string formula)
    {
        // Reference codes in formulas are plain identifiers (alphanumeric + underscore)
        // e.g., "REVENUE - COGS" references REVENUE and COGS
        var tokens = formula.Split(new[] { '+', '-', '*', '/', '(', ')', ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Where(t => !decimal.TryParse(t, out _) && !IsBuiltInFunction(t));
    }

    private static bool IsBuiltInFunction(string token) =>
        token is "abs" or "round" or "min" or "max" or "sum" or "sqrt" or "pow" or "ceil" or "floor";
}

/// <summary>
/// Individual row in a financial report template.
/// Defines what data to show and how to calculate it.
/// </summary>
public class FinancialReportRow : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid FinancialReportTemplateId { get; private set; }

    /// <summary>Display label for this row (e.g., "Revenue", "Total Assets").</summary>
    public string Label { get; set; } = null!;

    /// <summary>Data source for this row's values.</summary>
    public FinancialReportDataSource DataSource { get; set; }

    /// <summary>Display order within the template.</summary>
    public int SortOrder { get; set; }

    /// <summary>Unique code for cross-row formula references (e.g., "REVENUE", "TOTAL_ASSETS").</summary>
    public string? ReferenceCode { get; set; }

    /// <summary>Formula expression for CalculatedAmount rows (e.g., "REVENUE - COGS").</summary>
    public string? CalculationFormula { get; set; }

    /// <summary>Account category filter for AccountData rows (e.g., "Revenue from Operations").</summary>
    public string? AccountCategoryFilter { get; set; }

    /// <summary>API endpoint for CustomAPI rows.</summary>
    public string? CustomApiPath { get; set; }

    /// <summary>Hide this row when all values are zero (threshold: 0.01).</summary>
    public bool HideWhenEmpty { get; set; }

    /// <summary>Render row label in bold (for totals/headers).</summary>
    public bool IsBold { get; set; }

    /// <summary>Row indent level (0=header, 1=detail, 2=sub-detail).</summary>
    public int IndentLevel { get; set; }

    /// <summary>Sign adjustment: 1 = normal, -1 = invert (for expense shown as positive).</summary>
    public int SignMultiplier { get; set; } = 1;

    protected FinancialReportRow() { }

    public FinancialReportRow(
        Guid id,
        Guid templateId,
        string label,
        FinancialReportDataSource dataSource,
        int sortOrder,
        string? referenceCode = null,
        string? calculationFormula = null,
        string? accountCategoryFilter = null,
        string? customApiPath = null,
        bool hideWhenEmpty = false,
        bool isBold = false) : base(id)
    {
        FinancialReportTemplateId = templateId;
        Label = label;
        DataSource = dataSource;
        SortOrder = sortOrder;
        ReferenceCode = referenceCode;
        CalculationFormula = calculationFormula;
        AccountCategoryFilter = accountCategoryFilter;
        CustomApiPath = customApiPath;
        HideWhenEmpty = hideWhenEmpty;
        IsBold = isBold;
    }
}

/// <summary>Financial report types.</summary>
public enum FinancialReportType
{
    ProfitAndLoss = 0,
    BalanceSheet = 1,
    CashFlow = 2,
    Custom = 3
}

/// <summary>Data source for a financial report row.</summary>
public enum FinancialReportDataSource
{
    /// <summary>Row pulls GL data filtered by account categories.</summary>
    AccountData = 0,
    /// <summary>Row calculates value from formula referencing other rows.</summary>
    CalculatedAmount = 1,
    /// <summary>Row fetches data from a custom API endpoint.</summary>
    CustomApi = 2,
    /// <summary>Visual separator — blank line.</summary>
    BlankLine = 3,
    /// <summary>Visual separator — column break (multi-segment layout).</summary>
    ColumnBreak = 4,
    /// <summary>Visual separator — section break (multi-segment layout).</summary>
    SectionBreak = 5
}
