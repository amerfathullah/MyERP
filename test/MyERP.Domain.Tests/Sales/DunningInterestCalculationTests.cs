using System;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Dunning interest calculations (Gotcha #2713):
/// interest_amount = outstanding × (rate_of_interest / 100 / 365) × overdue_days
/// </summary>
public class DunningInterestCalculationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    [Fact]
    public void Dunning_CalculateInterest_CalculatesCorrectly()
    {
        var dunning = new Dunning(Guid.NewGuid(), _companyId, _customerId, DateTime.UtcNow, 1);
        // Overdue payment: 10,000 outstanding, 30 days overdue, 10% yearly interest
        // Expected: 10000 * 0.10 / 365 * 30 = 82.1917... => 82.19
        dunning.AddOverduePayment(Guid.NewGuid(), 10000m, DateTime.UtcNow.AddDays(-30), 30);
        dunning.DunningFee = 50m;

        dunning.CalculateInterest(10m);

        Assert.Equal(82.19m, dunning.InterestAmount);
        Assert.Equal(10000m + 50m + 82.19m, dunning.GrandTotal);
    }

    [Fact]
    public void Dunning_CalculateInterest_ZeroOrNegativeRate_SetsZeroInterest()
    {
        var dunning = new Dunning(Guid.NewGuid(), _companyId, _customerId, DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 5000m, DateTime.UtcNow.AddDays(-15), 15);

        dunning.CalculateInterest(0m);

        Assert.Equal(0m, dunning.InterestAmount);
    }
}
