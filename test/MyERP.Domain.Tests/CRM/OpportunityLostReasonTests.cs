using System;
using MyERP.CRM.Entities;
using Xunit;

namespace MyERP.Domain.Tests.CRM;

public class OpportunityLostReasonTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void OpportunityLostReason_Creation_SetsPropertiesCorrectly()
    {
        var reason = new OpportunityLostReason(Guid.NewGuid(), _companyId, "Price Too High")
        {
            Description = "Customer found competitor cheaper by >20%",
            IsDisabled = false
        };

        Assert.Equal("Price Too High", reason.Reason);
        Assert.Equal("Customer found competitor cheaper by >20%", reason.Description);
        Assert.False(reason.IsDisabled);
    }

    [Fact]
    public void OpportunityLostReason_Constructor_ThrowsOnEmptyReason()
    {
        Assert.Throws<ArgumentException>(() =>
            new OpportunityLostReason(Guid.NewGuid(), _companyId, "")
        );
    }
}
