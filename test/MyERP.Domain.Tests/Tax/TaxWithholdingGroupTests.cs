using System;
using MyERP.Tax.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Tax;

public class TaxWithholdingGroupTests
{
    [Fact]
    public void Should_Create_Valid_TaxWithholdingGroup()
    {
        var id = Guid.NewGuid();
        var group = new TaxWithholdingGroup(id, "Individual / Sole Proprietor", "Withholding group for individual vendors", true);

        group.Id.ShouldBe(id);
        group.GroupName.ShouldBe("Individual / Sole Proprietor");
        group.Description.ShouldBe("Withholding group for individual vendors");
        group.IsActive.ShouldBeTrue();

        group.Disable();
        group.IsActive.ShouldBeFalse();

        group.Enable();
        group.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Should_Throw_On_Invalid_GroupName(string? invalidName)
    {
        Should.Throw<ArgumentException>(() =>
        {
            new TaxWithholdingGroup(Guid.NewGuid(), invalidName!);
        });
    }
}
