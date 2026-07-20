using System;
using System.Linq;
using System.Linq.Expressions;

namespace MyERP;

/// <summary>
/// Safe sorting helper that maps field names to entity properties.
/// Prevents SQL injection by only allowing whitelisted field names.
/// </summary>
public static class SortingHelper
{
    /// <summary>
    /// Applies sorting to an IQueryable based on a "fieldName asc|desc" string.
    /// Falls back to defaultSort if the field is not in the allowed list.
    /// </summary>
    public static IQueryable<T> ApplySorting<T>(
        IQueryable<T> query,
        string? sorting,
        Func<IQueryable<T>, IQueryable<T>> defaultSort,
        params (string fieldName, Expression<Func<T, object>> expression)[] allowedFields)
    {
        if (string.IsNullOrWhiteSpace(sorting))
            return defaultSort(query);

        var parts = sorting.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var fieldName = parts[0];
        var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        var match = allowedFields.FirstOrDefault(f =>
            f.fieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

        if (match.expression == null)
            return defaultSort(query);

        return descending
            ? query.OrderByDescending(match.expression)
            : query.OrderBy(match.expression);
    }
}
