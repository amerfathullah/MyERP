using System;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Packing Slip case number overlap rules (Gotcha #128).
/// 3-condition overlap check:
/// (from1 >= from2 && from1 <= to2) || (to1 >= from2 && to1 <= to2) || (from2 >= from1 && from2 <= to1)
/// </summary>
public class PackingSlipOverlapTests
{
    [Theory]
    // Exact match
    [InlineData(1, 5, 1, 5, true)]
    // Partial overlap - from1 inside range 2
    [InlineData(3, 7, 1, 5, true)]
    // Partial overlap - to1 inside range 2
    [InlineData(1, 4, 3, 8, true)]
    // Range 1 completely contains Range 2
    [InlineData(1, 10, 3, 5, true)]
    // Range 2 completely contains Range 1
    [InlineData(3, 5, 1, 10, true)]
    // Boundary adjacent (no overlap)
    [InlineData(1, 5, 6, 10, false)]
    [InlineData(6, 10, 1, 5, false)]
    // Completely disjoint
    [InlineData(1, 2, 10, 12, false)]
    public void HasOverlap_EvaluatesCorrectly(int from1, int to1, int from2, int to2, bool expectedOverlap)
    {
        var result = PackingSlip.HasOverlap(from1, to1, from2, to2);
        Assert.Equal(expectedOverlap, result);
    }
}
