using System;
using System.Linq;
using MyERP;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Shared;

public class SortingHelperTests
{
    private record TestItem(int Id, string Name, decimal Amount, DateTime Date);

    private static readonly TestItem[] Items =
    {
        new(1, "Alpha", 100m, new DateTime(2026, 1, 1)),
        new(2, "Charlie", 50m, new DateTime(2026, 3, 1)),
        new(3, "Bravo", 200m, new DateTime(2026, 2, 1)),
    };

    [Fact]
    public void NullSorting_UsesDefaultSort()
    {
        var query = Items.AsQueryable();
        var result = SortingHelper.ApplySorting(query, null,
            q => q.OrderBy(x => x.Id),
            ("name", x => (object)x.Name));

        result.First().Id.ShouldBe(1); // Default: by Id asc
    }

    [Fact]
    public void EmptySorting_UsesDefaultSort()
    {
        var query = Items.AsQueryable();
        var result = SortingHelper.ApplySorting(query, "  ",
            q => q.OrderBy(x => x.Id),
            ("name", x => (object)x.Name));

        result.First().Id.ShouldBe(1);
    }

    [Fact]
    public void ValidField_Ascending()
    {
        var query = Items.AsQueryable();
        var result = SortingHelper.ApplySorting(query, "name asc",
            q => q.OrderBy(x => x.Id),
            ("name", x => (object)x.Name));

        result.First().Name.ShouldBe("Alpha");
        result.Last().Name.ShouldBe("Charlie");
    }

    [Fact]
    public void ValidField_Descending()
    {
        var query = Items.AsQueryable();
        var result = SortingHelper.ApplySorting(query, "amount desc",
            q => q.OrderBy(x => x.Id),
            ("amount", x => (object)x.Amount));

        result.First().Amount.ShouldBe(200m);
        result.Last().Amount.ShouldBe(50m);
    }

    [Fact]
    public void CaseInsensitive_FieldMatching()
    {
        var query = Items.AsQueryable();
        var result = SortingHelper.ApplySorting(query, "NAME desc",
            q => q.OrderBy(x => x.Id),
            ("name", x => (object)x.Name));

        result.First().Name.ShouldBe("Charlie");
    }

    [Fact]
    public void UnknownField_FallsBackToDefault()
    {
        var query = Items.AsQueryable();
        var result = SortingHelper.ApplySorting(query, "unknownField desc",
            q => q.OrderByDescending(x => x.Amount),
            ("name", x => (object)x.Name));

        result.First().Amount.ShouldBe(200m); // Default: amount desc
    }

    [Fact]
    public void FieldOnly_NoDirection_DefaultsToDesc()
    {
        var query = Items.AsQueryable();
        var result = SortingHelper.ApplySorting(query, "amount",
            q => q.OrderBy(x => x.Id),
            ("amount", x => (object)x.Amount));

        // No explicit direction means not "desc", defaults to asc (since descending = false when parts.Length == 1)
        result.First().Amount.ShouldBe(50m);
    }

    [Fact]
    public void DateField_SortsCorrectly()
    {
        var query = Items.AsQueryable();
        var result = SortingHelper.ApplySorting(query, "date desc",
            q => q.OrderBy(x => x.Id),
            ("date", x => (object)x.Date));

        result.First().Date.ShouldBe(new DateTime(2026, 3, 1));
    }

    [Fact]
    public void PreventsInjection_UnknownStringIgnored()
    {
        var query = Items.AsQueryable();
        // Attempting SQL injection via sorting parameter
        var result = SortingHelper.ApplySorting(query, "name; DROP TABLE--",
            q => q.OrderBy(x => x.Id),
            ("name", x => (object)x.Name));

        // Falls through to default (no field match for "name;")
        result.First().Id.ShouldBe(1);
    }
}
