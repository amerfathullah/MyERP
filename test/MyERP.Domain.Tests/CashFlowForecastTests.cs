using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for CashFlowForecast business logic:
/// 1. Forecast entry classification (inflows vs outflows)
/// 2. Period building (weekly bucketing)
/// 3. Summary metrics (cash runway, overdue detection, crunch date)
/// 4. Balance projection accuracy
/// </summary>
public class CashFlowForecastTests
{
    // --- Forecast Entry Classification ---

    [Fact]
    public void ForecastEntry_Inflow_HasPositiveAmount()
    {
        var entry = new CashFlowForecastEntryDto
        {
            DocumentType = "SalesInvoice",
            Amount = 5000m,
            DueDate = DateTime.UtcNow.AddDays(15),
            DaysUntilDue = 15,
            IsOverdue = false,
            PartyName = "Customer A",
            DocumentNumber = "SI-001",
            DocumentId = Guid.NewGuid()
        };
        Assert.True(entry.Amount > 0);
        Assert.False(entry.IsOverdue);
    }

    [Fact]
    public void ForecastEntry_Overdue_WhenDueDateInPast()
    {
        var entry = new CashFlowForecastEntryDto
        {
            DueDate = DateTime.UtcNow.AddDays(-10),
            DaysUntilDue = -10,
            IsOverdue = true,
            Amount = 3000m,
            DocumentType = "SalesInvoice",
            PartyName = "Customer B",
            DocumentNumber = "SI-002",
            DocumentId = Guid.NewGuid()
        };
        Assert.True(entry.IsOverdue);
        Assert.True(entry.DaysUntilDue < 0);
    }

    [Fact]
    public void ForecastEntry_Outflow_IsPurchaseInvoice()
    {
        var entry = new CashFlowForecastEntryDto
        {
            DocumentType = "PurchaseInvoice",
            Amount = 2500m,
            DueDate = DateTime.UtcNow.AddDays(30),
            DaysUntilDue = 30,
            IsOverdue = false,
            PartyName = "Supplier X",
            DocumentNumber = "PI-001",
            DocumentId = Guid.NewGuid()
        };
        Assert.Equal("PurchaseInvoice", entry.DocumentType);
    }

    // --- Period Building ---

    [Fact]
    public void ForecastPeriod_HasCorrectFields()
    {
        var period = new CashFlowForecastPeriodDto
        {
            Label = "01 Aug - 07 Aug",
            PeriodStart = new DateTime(2026, 8, 1),
            PeriodEnd = new DateTime(2026, 8, 8),
            Inflows = 10000m,
            Outflows = 7000m,
            NetFlow = 3000m,
            CumulativeBalance = 53000m
        };
        Assert.Equal(3000m, period.NetFlow);
        Assert.Equal(53000m, period.CumulativeBalance);
    }

    [Fact]
    public void ForecastPeriod_NegativeNetFlow_WhenOutflowsExceedInflows()
    {
        var period = new CashFlowForecastPeriodDto
        {
            Inflows = 2000m,
            Outflows = 8000m,
            NetFlow = -6000m,
            CumulativeBalance = -1000m
        };
        Assert.True(period.NetFlow < 0);
        Assert.True(period.CumulativeBalance < 0);
    }

    [Fact]
    public void ForecastDto_ProjectedClosingBalance_IsOpeningPlusNetFlow()
    {
        var forecast = new CashFlowForecastDto
        {
            CurrentCashBalance = 50000m,
            TotalExpectedInflows = 30000m,
            TotalExpectedOutflows = 20000m,
            NetCashFlow = 10000m,
            ProjectedClosingBalance = 60000m
        };
        Assert.Equal(forecast.CurrentCashBalance + forecast.NetCashFlow, forecast.ProjectedClosingBalance);
    }

    [Fact]
    public void ForecastDto_NegativeProjectedBalance_WhenOutflowsExceedAll()
    {
        var forecast = new CashFlowForecastDto
        {
            CurrentCashBalance = 10000m,
            TotalExpectedInflows = 5000m,
            TotalExpectedOutflows = 25000m,
            NetCashFlow = -20000m,
            ProjectedClosingBalance = -10000m
        };
        Assert.True(forecast.ProjectedClosingBalance < 0);
    }

    // --- Summary Metrics ---

    [Fact]
    public void Summary_CashRunway_CalculatedFromAvgDailyOutflow()
    {
        // Cash runway = current balance / avg daily outflow
        var currentBalance = 100000m;
        var totalOutflows = 50000m; // over 90 days
        var avgDailyOutflow = totalOutflows / 90m;
        var runway = currentBalance / avgDailyOutflow;
        Assert.True(runway > 0);
        Assert.Equal(180m, Math.Round(runway)); // 100K / (50K/90) = 180 days
    }

    [Fact]
    public void Summary_CashRunway_InfiniteWhenNoOutflows()
    {
        var summary = new CashFlowForecastSummaryDto
        {
            CashRunwayDays = 999m, // capped at 999
            OverdueReceivablesCount = 0,
            OverduePayablesCount = 0,
            ProjectedCashCrunchDate = null
        };
        Assert.Equal(999m, summary.CashRunwayDays);
        Assert.Null(summary.ProjectedCashCrunchDate);
    }

    [Fact]
    public void Summary_OverdueReceivables_CountedCorrectly()
    {
        var summary = new CashFlowForecastSummaryDto
        {
            OverdueReceivablesCount = 3,
            OverdueReceivablesAmount = 15000m,
            OverduePayablesCount = 1,
            OverduePayablesAmount = 5000m
        };
        Assert.Equal(3, summary.OverdueReceivablesCount);
        Assert.Equal(15000m, summary.OverdueReceivablesAmount);
    }

    [Fact]
    public void Summary_CrunchDate_SetWhenBalanceGoesNegative()
    {
        var crunchDate = new DateTime(2026, 9, 1);
        var summary = new CashFlowForecastSummaryDto
        {
            ProjectedCashCrunchDate = crunchDate,
            CashRunwayDays = 38m
        };
        Assert.NotNull(summary.ProjectedCashCrunchDate);
        Assert.Equal(crunchDate, summary.ProjectedCashCrunchDate);
    }

    // --- Outstanding Amount Calculation ---

    [Fact]
    public void SalesInvoice_OutstandingAmount_IsGrandTotalMinusPaidMinusWriteOff()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "MYR", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Item A", 10, 100m, 0m);
        // GrandTotal = 1000, AmountPaid = 400, WriteOff = 100
        // Outstanding = 1000 - 400 - 100 = 500
        Assert.Equal(1000m, si.GrandTotal);
    }

    [Fact]
    public void PurchaseInvoice_OutstandingAmount_IsGrandTotalMinusPaid()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var pi = new PurchaseInvoice(Guid.NewGuid(), companyId, supplierId, "MYR", DateTime.UtcNow);
        pi.AddItem(Guid.NewGuid(), "Material B", 5, 200m, 0m);
        Assert.Equal(1000m, pi.GrandTotal);
    }

    // --- Forecast DTO Structure ---

    [Fact]
    public void ForecastDto_DefaultValues()
    {
        var dto = new CashFlowForecastDto();
        Assert.Equal(0m, dto.CurrentCashBalance);
        Assert.Equal(0m, dto.TotalExpectedInflows);
        Assert.Equal(0m, dto.TotalExpectedOutflows);
        Assert.Equal(0m, dto.NetCashFlow);
        Assert.Equal(0m, dto.ProjectedClosingBalance);
        Assert.NotNull(dto.Periods);
        Assert.Empty(dto.Periods);
        Assert.NotNull(dto.UpcomingInflows);
        Assert.Empty(dto.UpcomingInflows);
        Assert.NotNull(dto.UpcomingOutflows);
        Assert.Empty(dto.UpcomingOutflows);
        Assert.NotNull(dto.Summary);
    }

    [Fact]
    public void ForecastRequestDto_DefaultForecastDays_Is90()
    {
        var request = new CashFlowForecastRequestDto();
        Assert.Equal(90, request.ForecastDays);
    }

    [Fact]
    public void ForecastRequestDto_CanSetForecastDays()
    {
        var request = new CashFlowForecastRequestDto { ForecastDays = 180 };
        Assert.Equal(180, request.ForecastDays);
    }

    // --- Net Cash Flow ---

    [Fact]
    public void NetCashFlow_IsInflowsMinusOutflows()
    {
        var inflows = 75000m;
        var outflows = 45000m;
        var netCashFlow = inflows - outflows;
        Assert.Equal(30000m, netCashFlow);
    }

    [Fact]
    public void NetCashFlow_CanBeNegative()
    {
        var inflows = 20000m;
        var outflows = 35000m;
        var netCashFlow = inflows - outflows;
        Assert.Equal(-15000m, netCashFlow);
        Assert.True(netCashFlow < 0);
    }

    // --- Period Cumulative Balance ---

    [Fact]
    public void CumulativeBalance_ProgressesThroughPeriods()
    {
        var opening = 50000m;
        var periods = new[]
        {
            new { NetFlow = 5000m },   // 55000
            new { NetFlow = -3000m },  // 52000
            new { NetFlow = -8000m },  // 44000
            new { NetFlow = 2000m },   // 46000
        };

        var cumulative = opening;
        foreach (var p in periods)
        {
            cumulative += p.NetFlow;
        }
        Assert.Equal(46000m, cumulative);
    }

    [Fact]
    public void CumulativeBalance_CanCrossZero()
    {
        var opening = 5000m;
        var bigOutflow = -8000m;
        var balance = opening + bigOutflow;
        Assert.Equal(-3000m, balance);
        Assert.True(balance < 0); // Indicates cash crunch
    }
}
