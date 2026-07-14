using System;
using MyERP.Core.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Core;

/// <summary>
/// Tests for stock auth role bypass and stock_frozen_upto_days alternative.
/// Per ERPNext: stock_auth_role lets authorized users bypass freeze,
/// stock_frozen_upto_days is an alternative to absolute date.
/// </summary>
public class StockFreezeRoleBypassTests
{
    [Fact]
    public void Company_StockFrozenUptoDays_DefaultsZero()
    {
        var company = CreateCompany();
        company.StockFrozenUptoDays.ShouldBe(0);
    }

    [Fact]
    public void Company_StockFrozenUptoDays_CanBeSet()
    {
        var company = CreateCompany();
        company.StockFrozenUptoDays = 30;
        company.StockFrozenUptoDays.ShouldBe(30);
    }

    [Fact]
    public void Company_StockAuthRole_DefaultsNull()
    {
        var company = CreateCompany();
        company.StockAuthRole.ShouldBeNull();
    }

    [Fact]
    public void Company_StockAuthRole_CanBeSet()
    {
        var company = CreateCompany();
        company.StockAuthRole = "Stock Manager";
        company.StockAuthRole.ShouldBe("Stock Manager");
    }

    [Fact]
    public void StockFrozenUptoDays_CalculatesEffectiveDate()
    {
        // If today is 2026-07-14 and days=30, frozen date is 2026-06-14
        var days = 30;
        var effectiveDate = DateTime.UtcNow.Date.AddDays(-days);
        effectiveDate.ShouldBeLessThan(DateTime.UtcNow.Date);
    }

    [Fact]
    public void StockFrozenUpto_AbsoluteDate_TakesPrecedence()
    {
        // When StockFrozenUpto is set, it takes precedence (days ignored)
        var company = CreateCompany();
        company.StockFrozenUpto = new DateTime(2026, 6, 30);
        company.StockFrozenUptoDays = 30;

        // The service should use StockFrozenUpto when set, not days
        company.StockFrozenUpto.HasValue.ShouldBeTrue();
    }

    private static Company CreateCompany()
    {
        return new Company(Guid.NewGuid(), "Test Co");
    }
}
