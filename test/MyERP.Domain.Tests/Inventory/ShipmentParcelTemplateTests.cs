using System;
using MyERP.Inventory.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class ShipmentParcelTemplateTests
{
    [Fact]
    public void ShipmentParcelTemplate_Creation_SetsPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var template = new ShipmentParcelTemplate(
            id,
            "Medium Carton",
            length: 40.5m,
            width: 30.0m,
            height: 25.0m,
            weight: 15.0m,
            description: "Standard medium corrugated box");

        Assert.Equal(id, template.Id);
        Assert.Equal("Medium Carton", template.ParcelTemplateName);
        Assert.Equal(40.5m, template.Length);
        Assert.Equal(30.0m, template.Width);
        Assert.Equal(25.0m, template.Height);
        Assert.Equal(15.0m, template.Weight);
        Assert.Equal("Standard medium corrugated box", template.Description);
        Assert.True(template.IsActive);
    }
}
