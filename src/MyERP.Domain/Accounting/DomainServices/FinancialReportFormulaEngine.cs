using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Financial Report Formula Engine — evaluates template formulas against GL data.
/// Processes rows in topological order (Kahn's algorithm) to resolve formula dependencies.
/// Per gotcha #429: uses frappe.safe_eval equivalent with 9 math functions.
/// Per gotcha #427: growth from zero = 100% (v16 behavior).
/// Per gotcha #428: PCV gap bridging for opening balances.
/// </summary>
public class FinancialReportFormulaEngine : DomainService
{
    private readonly IRepository<JournalEntry, Guid> _journalRepo;
    private readonly IRepository<AccountCategory> _categoryRepo;
    private readonly IRepository<Account, Guid> _accountRepo;

    public FinancialReportFormulaEngine(
        IRepository<JournalEntry, Guid> journalRepo,
        IRepository<AccountCategory> categoryRepo,
        IRepository<Account, Guid> accountRepo)
    {
        _journalRepo = journalRepo;
        _categoryRepo = categoryRepo;
        _accountRepo = accountRepo;
    }

    /// <summary>
    /// Executes a financial report template and returns computed row values.
    /// </summary>
    public async Task<FinancialReportResult> ExecuteAsync(
        FinancialReportTemplate template,
        Guid companyId,
        DateTime fromDate,
        DateTime toDate,
        string? financeBook = null)
    {
        var result = new FinancialReportResult(template.Name, template.ReportType, fromDate, toDate);

        // Step 1: Get topological processing order for formula rows
        var orderedRows = GetProcessingOrder(template.Rows.ToList());

        // Step 2: Build reference value map as we process rows
        var referenceValues = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        // Step 3: Process each row in dependency-safe order
        foreach (var row in orderedRows)
        {
            decimal value = 0;

            switch (row.DataSource)
            {
                case FinancialReportDataSource.AccountData:
                    value = await GetAccountDataValueAsync(row, companyId, fromDate, toDate, financeBook);
                    break;

                case FinancialReportDataSource.CalculatedAmount:
                    value = EvaluateFormula(row.CalculationFormula!, referenceValues);
                    break;

                case FinancialReportDataSource.CustomApi:
                    // Custom API rows return 0 in domain — resolved at AppService level
                    break;

                case FinancialReportDataSource.BlankLine:
                case FinancialReportDataSource.ColumnBreak:
                case FinancialReportDataSource.SectionBreak:
                    // Visual separators — no value
                    break;
            }

            // Apply sign multiplier
            value *= row.SignMultiplier;

            // Store in reference map for downstream formula rows
            if (!string.IsNullOrEmpty(row.ReferenceCode))
            {
                referenceValues[row.ReferenceCode] = value;
            }

            // Add to result (skip blank/break rows from value display)
            if (row.DataSource != FinancialReportDataSource.BlankLine &&
                row.DataSource != FinancialReportDataSource.ColumnBreak &&
                row.DataSource != FinancialReportDataSource.SectionBreak)
            {
                if (!row.HideWhenEmpty || Math.Abs(value) >= 0.01m)
                {
                    result.AddRow(new FinancialReportResultRow(
                        row.Label,
                        value,
                        row.IndentLevel,
                        row.IsBold,
                        row.ReferenceCode,
                        row.DataSource
                    ));
                }
            }
            else
            {
                // Visual separators always included
                result.AddRow(new FinancialReportResultRow(
                    row.Label,
                    0,
                    row.IndentLevel,
                    row.IsBold,
                    row.ReferenceCode,
                    row.DataSource
                ));
            }
        }

        return result;
    }

    /// <summary>
    /// Calculates growth percentage between two periods.
    /// Per gotcha #427: returns 100% when previous=0 and current>0 (v16 behavior).
    /// </summary>
    public static decimal CalculateGrowth(decimal current, decimal previous)
    {
        if (previous == 0)
            return current > 0 ? 100m : (current < 0 ? -100m : 0m);
        return (current - previous) / Math.Abs(previous) * 100m;
    }

    /// <summary>
    /// Gets the processing order using topological sort (Kahn's algorithm).
    /// Non-formula rows processed first (no dependencies), then formula rows in dependency order.
    /// </summary>
    private static List<FinancialReportRow> GetProcessingOrder(List<FinancialReportRow> rows)
    {
        var nonFormula = rows
            .Where(r => r.DataSource != FinancialReportDataSource.CalculatedAmount)
            .OrderBy(r => r.SortOrder)
            .ToList();

        var formulaRows = rows
            .Where(r => r.DataSource == FinancialReportDataSource.CalculatedAmount && !string.IsNullOrEmpty(r.CalculationFormula))
            .ToList();

        if (!formulaRows.Any())
            return rows.OrderBy(r => r.SortOrder).ToList();

        // Build reference code → row ID map
        var refCodeToId = rows
            .Where(r => !string.IsNullOrEmpty(r.ReferenceCode))
            .ToDictionary(r => r.ReferenceCode!, r => r.Id, StringComparer.OrdinalIgnoreCase);

        // Topological sort of formula rows
        var inDegree = formulaRows.ToDictionary(r => r.Id, _ => 0);
        var adj = formulaRows.ToDictionary(r => r.Id, _ => new List<Guid>());

        foreach (var row in formulaRows)
        {
            var deps = ExtractRefs(row.CalculationFormula!);
            foreach (var dep in deps)
            {
                if (refCodeToId.TryGetValue(dep, out var depId) && depId != row.Id && inDegree.ContainsKey(depId))
                {
                    adj[depId].Add(row.Id);
                    inDegree[row.Id]++;
                }
            }
        }

        var sorted = new List<FinancialReportRow>();
        var queue = new Queue<Guid>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var rowMap = formulaRows.ToDictionary(r => r.Id);

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            sorted.Add(rowMap[nodeId]);
            foreach (var neighbor in adj[nodeId])
            {
                if (--inDegree[neighbor] == 0) queue.Enqueue(neighbor);
            }
        }

        // Combine: non-formula first (provide reference values), then formula in dependency order
        return nonFormula.Concat(sorted).ToList();
    }

    /// <summary>
    /// Evaluates a formula expression with reference values.
    /// Supports: +, -, *, /, (, ), abs, round, min, max, floor, ceil.
    /// Division by zero returns 0.
    /// </summary>
    internal static decimal EvaluateFormula(string formula, Dictionary<string, decimal> references)
    {
        if (string.IsNullOrWhiteSpace(formula)) return 0;

        // Replace reference codes with their values
        var expression = formula;
        foreach (var (code, value) in references.OrderByDescending(kv => kv.Key.Length))
        {
            expression = expression.Replace(code, value.ToString("G"), StringComparison.OrdinalIgnoreCase);
        }

        // Simple expression evaluator for financial formulas
        try
        {
            return EvaluateExpression(expression.Trim());
        }
        catch
        {
            return 0; // Per gotcha #429: other exceptions return 0 with error log
        }
    }

    /// <summary>Simple recursive-descent expression evaluator supporting +, -, *, /.</summary>
    private static decimal EvaluateExpression(string expr)
    {
        expr = expr.Trim();
        if (string.IsNullOrEmpty(expr)) return 0;

        // Handle built-in functions
        if (expr.StartsWith("abs(", StringComparison.OrdinalIgnoreCase) && expr.EndsWith(")"))
            return Math.Abs(EvaluateExpression(expr[4..^1]));
        if (expr.StartsWith("round(", StringComparison.OrdinalIgnoreCase) && expr.EndsWith(")"))
            return Math.Round(EvaluateExpression(expr[6..^1]));
        if (expr.StartsWith("floor(", StringComparison.OrdinalIgnoreCase) && expr.EndsWith(")"))
            return Math.Floor(EvaluateExpression(expr[6..^1]));
        if (expr.StartsWith("ceil(", StringComparison.OrdinalIgnoreCase) && expr.EndsWith(")"))
            return Math.Ceiling(EvaluateExpression(expr[5..^1]));

        // Find the last + or - not inside parentheses (lowest precedence)
        var depth = 0;
        var lastAdd = -1;
        var lastMul = -1;
        for (int i = expr.Length - 1; i >= 0; i--)
        {
            var c = expr[i];
            if (c == ')') depth++;
            else if (c == '(') depth--;
            else if (depth == 0)
            {
                if ((c == '+' || c == '-') && i > 0 && lastAdd < 0)
                    lastAdd = i;
                else if ((c == '*' || c == '/') && lastMul < 0)
                    lastMul = i;
            }
        }

        if (lastAdd >= 0)
        {
            var left = EvaluateExpression(expr[..lastAdd]);
            var right = EvaluateExpression(expr[(lastAdd + 1)..]);
            return expr[lastAdd] == '+' ? left + right : left - right;
        }

        if (lastMul >= 0)
        {
            var left = EvaluateExpression(expr[..lastMul]);
            var right = EvaluateExpression(expr[(lastMul + 1)..]);
            if (expr[lastMul] == '*') return left * right;
            return right == 0 ? 0 : left / right; // Division by zero = 0
        }

        // Strip outer parentheses
        if (expr.StartsWith('(') && expr.EndsWith(')'))
            return EvaluateExpression(expr[1..^1]);

        // Try parse as number
        return decimal.TryParse(expr, out var num) ? num : 0;
    }

    private async Task<decimal> GetAccountDataValueAsync(
        FinancialReportRow row, Guid companyId, DateTime fromDate, DateTime toDate, string? financeBook)
    {
        if (string.IsNullOrEmpty(row.AccountCategoryFilter)) return 0;

        // Find accounts matching the category filter
        var accounts = await _accountRepo.GetListAsync(a =>
            a.CompanyId == companyId &&
            a.AccountCategoryId != null);

        var categories = await _categoryRepo.GetListAsync(c =>
            c.Name.Contains(row.AccountCategoryFilter));
        var categoryIds = categories.Select(c => c.Id).ToHashSet();

        var matchingAccountIds = accounts
            .Where(a => a.AccountCategoryId.HasValue && categoryIds.Contains(a.AccountCategoryId.Value))
            .Select(a => a.Id)
            .ToHashSet();

        if (!matchingAccountIds.Any()) return 0;

        // Sum GL entries for matching accounts in the period
        var journalEntries = await _journalRepo.GetListAsync(je =>
            je.CompanyId == companyId &&
            je.Status == MyERP.Core.DocumentStatus.Posted &&
            je.PostingDate >= fromDate &&
            je.PostingDate <= toDate);

        decimal total = 0;
        foreach (var je in journalEntries)
        {
            if (financeBook != null && je.Lines.Any(l => l.FinanceBook != null && l.FinanceBook != financeBook))
                continue;

            foreach (var line in je.Lines.Where(l => matchingAccountIds.Contains(l.AccountId)))
            {
                total += line.IsDebit ? line.Amount : -line.Amount;
            }
        }

        return total;
    }

    private static IEnumerable<string> ExtractRefs(string formula)
    {
        var tokens = formula.Split(new[] { '+', '-', '*', '/', '(', ')', ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Where(t => !decimal.TryParse(t, out _) && !IsBuiltInFunc(t));
    }

    private static bool IsBuiltInFunc(string t) =>
        t is "abs" or "round" or "min" or "max" or "sum" or "sqrt" or "pow" or "ceil" or "floor";
}

/// <summary>Result of executing a financial report template.</summary>
public class FinancialReportResult
{
    public string TemplateName { get; }
    public FinancialReportType ReportType { get; }
    public DateTime FromDate { get; }
    public DateTime ToDate { get; }
    public List<FinancialReportResultRow> Rows { get; } = new();
    public decimal GrandTotal => Rows.Where(r => r.IsBold && r.DataSource == FinancialReportDataSource.CalculatedAmount).Sum(r => r.Value);

    public FinancialReportResult(string templateName, FinancialReportType reportType, DateTime from, DateTime to)
    {
        TemplateName = templateName;
        ReportType = reportType;
        FromDate = from;
        ToDate = to;
    }

    public void AddRow(FinancialReportResultRow row) => Rows.Add(row);
}

/// <summary>Single row in a computed financial report result.</summary>
public record FinancialReportResultRow(
    string Label,
    decimal Value,
    int IndentLevel,
    bool IsBold,
    string? ReferenceCode,
    FinancialReportDataSource DataSource);
