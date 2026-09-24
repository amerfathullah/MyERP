using System;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Customer on-hold blocking behavior (ERPNext PR #59303 / commit d75b957ce0).
/// Customer on hold blocks SO, DN, SI, and POS transactions, while exempting Quotations.
/// </summary>
public class CustomerHoldAndBlockingTests
{
    private static Customer CreateCustomer(string name = "Acme Corp")
        => new(Guid.NewGuid(), Guid.NewGuid(), name);

    [Fact]
    public void DefaultCustomer_NotOnHold_NotBlocked()
    {
        var customer = CreateCustomer();

        customer.OnHold.ShouldBeFalse();
        customer.ReleaseDate.ShouldBeNull();
        customer.IsBlocked.ShouldBeFalse();
        customer.IsBlockedOn(DateTime.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void CustomerOnHold_WithoutReleaseDate_IsBlockedIndefinitely()
    {
        var customer = CreateCustomer();
        customer.OnHold = true;
        customer.ReleaseDate = null;

        customer.IsBlocked.ShouldBeTrue();
        customer.IsBlockedOn(DateTime.UtcNow).ShouldBeTrue();
        customer.IsBlockedOn(DateTime.UtcNow.AddYears(10)).ShouldBeTrue();
    }

    [Fact]
    public void CustomerOnHold_WithFutureReleaseDate_IsBlockedUntilReleaseDate()
    {
        var customer = CreateCustomer();
        var releaseDate = new DateTime(2026, 8, 1);
        customer.OnHold = true;
        customer.ReleaseDate = releaseDate;

        // Transaction before release date -> blocked
        customer.IsBlockedOn(new DateTime(2026, 7, 15)).ShouldBeTrue();

        // Transaction on release date -> blocked
        customer.IsBlockedOn(new DateTime(2026, 8, 1)).ShouldBeTrue();

        // Transaction after release date -> unblocked
        customer.IsBlockedOn(new DateTime(2026, 8, 2)).ShouldBeFalse();
    }

    [Fact]
    public void CustomerOnHold_WithPastReleaseDate_IsBlockedIsFalse()
    {
        var customer = CreateCustomer();
        customer.OnHold = true;
        customer.ReleaseDate = DateTime.UtcNow.AddDays(-1);

        // Current time is after release date -> no longer blocked
        customer.IsBlocked.ShouldBeFalse();
        customer.IsBlockedOn(DateTime.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void CustomerNotOnHold_EvenWithReleaseDateSet_IsNotBlocked()
    {
        var customer = CreateCustomer();
        customer.OnHold = false;
        customer.ReleaseDate = DateTime.UtcNow.AddDays(30);

        customer.IsBlocked.ShouldBeFalse();
        customer.IsBlockedOn(DateTime.UtcNow).ShouldBeFalse();
    }
}
