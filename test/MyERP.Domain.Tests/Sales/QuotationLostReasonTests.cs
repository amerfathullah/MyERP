using System;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class QuotationLostReasonTests
{
    [Fact]
    public void QuotationLostReason_Creation_SetsPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var reason = new QuotationLostReason(id, "Price too high", "Lost due to pricing budget constraint");

        Assert.Equal(id, reason.Id);
        Assert.Equal("Price too high", reason.Reason);
        Assert.Equal("Lost due to pricing budget constraint", reason.Description);
        Assert.True(reason.IsActive);
    }
}
