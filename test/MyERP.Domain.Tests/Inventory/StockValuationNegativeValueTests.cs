using System;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Unit tests verifying ERPNext PR #59291 (commit 8793ad8264):
/// Non-negative stock value for moving average items and batch valuation pool fallback.
/// </summary>
public class StockValuationNegativeValueTests
{
    [Fact]
    public void CalculateMovingAverage_StockIn_ClampsNegativeRateAndValue()
    {
        // Existing: 10 units with 0 rate
        var prev = MakeSle(10, 0);

        // Incoming with negative rate (e.g. invalid adjustment)
        var (rate, qty, value) = StockValuationService.CalculateMovingAverage(prev, 5, -20);

        qty.ShouldBe(15);
        rate.ShouldBeGreaterThanOrEqualTo(0m);
        value.ShouldBeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public void CalculateMovingAverage_StockOut_ClampsNegativeBalanceValue()
    {
        // Existing: 5 units with value 10 (rate = 2)
        var prev = MakeSle(5, 10);

        // Stock out that exhausts the stock
        var (rate, qty, value) = StockValuationService.CalculateMovingAverage(prev, -5, 0);

        qty.ShouldBe(0);
        rate.ShouldBe(2m); // outgoing rate
        value.ShouldBe(0);
    }

    [Fact]
    public void CalculateMovingAverage_ZeroBalance_ClearsQtyAndValue()
    {
        var prev = MakeSle(10, 250);

        var (rate, qty, value) = StockValuationService.CalculateMovingAverage(prev, -10, 0);

        qty.ShouldBe(0m);
        value.ShouldBe(0m);
        rate.ShouldBe(25m); // outgoing rate
    }

    [Fact]
    public void CalculateMovingAverage_StockOut_RemainingPositiveQty_HasNonNegativeValue()
    {
        var prev = MakeSle(20, 200);

        var (rate, qty, value) = StockValuationService.CalculateMovingAverage(prev, -15, 0);

        qty.ShouldBe(5);
        rate.ShouldBe(10);
        value.ShouldBe(50);
        value.ShouldBeGreaterThanOrEqualTo(0m);
    }

    private static StockLedgerEntry MakeSle(decimal balanceQty, decimal balanceValue)
    {
        var rate = balanceQty != 0 ? balanceValue / balanceQty : 0;
        return new StockLedgerEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            balanceQty,
            rate,
            balanceQty,
            balanceValue);
    }
}
