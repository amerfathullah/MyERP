using System;
using MyERP.CRM.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.CRM;

public class OpportunityTypeTests
{
    [Fact]
    public void Should_Create_Valid_OpportunityType()
    {
        var id = Guid.NewGuid();
        var type = new Entities.OpportunityType(id, "Annual Maintenance Contract", "Recurring AMC opportunity", true);

        type.Id.ShouldBe(id);
        type.Name.ShouldBe("Annual Maintenance Contract");
        type.Description.ShouldBe("Recurring AMC opportunity");
        type.IsActive.ShouldBeTrue();

        type.Disable();
        type.IsActive.ShouldBeFalse();

        type.Enable();
        type.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Should_Throw_On_Invalid_Name(string? invalidName)
    {
        Should.Throw<ArgumentException>(() =>
        {
            new Entities.OpportunityType(Guid.NewGuid(), invalidName!);
        });
    }
}
