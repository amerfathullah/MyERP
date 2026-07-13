using System;
using MyERP.Accounting.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Accounting;

public class FiscalYearTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        fy.Name.ShouldBe("FY2026");
        fy.IsClosed.ShouldBeFalse();
    }

    [Fact]
    public void ContainsDate_InsideRange_True()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        (fy.StartDate <= new DateTime(2026, 6, 15) && fy.EndDate >= new DateTime(2026, 6, 15))
            .ShouldBeTrue();
    }

    [Fact]
    public void ContainsDate_OutsideRange_False()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        (fy.StartDate <= new DateTime(2027, 1, 1) && fy.EndDate >= new DateTime(2027, 1, 1))
            .ShouldBeFalse();
    }
}

public class CurrencyExchangeTests
{
    [Fact]
    public void Create_SetsRate()
    {
        var ce = new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 4.72m, new DateTime(2026, 7, 1));
        ce.FromCurrency.ShouldBe("USD");
        ce.ToCurrency.ShouldBe("MYR");
        ce.ExchangeRate.ShouldBe(4.72m);
    }
}

public class AccountingPeriodTests
{
    [Fact]
    public void Create_NotClosed()
    {
        var ap = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(), "Q1 2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31));
        ap.IsClosed.ShouldBeFalse();
    }

    [Fact]
    public void Close_SetsClosed()
    {
        var ap = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(), "Q1 2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31));
        ap.Close();
        ap.IsClosed.ShouldBeTrue();
    }
}
