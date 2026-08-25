using System;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class SalesPartnerTypeTests
{
    [Fact]
    public void SalesPartnerType_Creation_SetsPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var type = new SalesPartnerType(id, "Regional Distributor", "Distributor operating across Southeast Asia", isActive: true);

        Assert.Equal(id, type.Id);
        Assert.Equal("Regional Distributor", type.PartnerTypeName);
        Assert.Equal("Distributor operating across Southeast Asia", type.Description);
        Assert.True(type.IsActive);
    }
}
