using System;
using MyERP.Accounting.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class BankAccountTypeTests
{
    [Fact]
    public void BankAccountType_Creation_SetsPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var type = new BankAccountType(id, "Current Account", "Standard corporate operating account", isActive: true);

        Assert.Equal(id, type.Id);
        Assert.Equal("Current Account", type.AccountTypeName);
        Assert.Equal("Standard corporate operating account", type.Description);
        Assert.True(type.IsActive);
    }
}
