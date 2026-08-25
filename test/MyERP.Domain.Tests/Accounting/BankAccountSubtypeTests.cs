using System;
using MyERP.Accounting.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class BankAccountSubtypeTests
{
    [Fact]
    public void BankAccountSubtype_Creation_SetsPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var subtype = new BankAccountSubtype(id, "Fixed Deposit", "Term deposit account with interest yield", isActive: true);

        Assert.Equal(id, subtype.Id);
        Assert.Equal("Fixed Deposit", subtype.AccountSubtypeName);
        Assert.Equal("Term deposit account with interest yield", subtype.Description);
        Assert.True(subtype.IsActive);
    }
}
