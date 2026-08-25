using System;
using MyERP.Core.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Core;

public class TerritoryAndGroupsTests
{
    [Fact]
    public void CustomerGroup_Creation_SetsPropertiesCorrectly()
    {
        var group = new CustomerGroup(Guid.NewGuid(), "Commercial Wholesale", null, false)
        {
            DefaultCreditLimit = 50000m
        };

        Assert.Equal("Commercial Wholesale", group.Name);
        Assert.Null(group.ParentId);
        Assert.False(group.IsGroup);
        Assert.Equal(50000m, group.DefaultCreditLimit);
    }

    [Fact]
    public void SupplierGroup_Creation_SetsPropertiesCorrectly()
    {
        var group = new SupplierGroup(Guid.NewGuid(), "Raw Materials", null, true);

        Assert.Equal("Raw Materials", group.Name);
        Assert.True(group.IsGroup);
    }

    [Fact]
    public void Territory_Creation_SetsPropertiesCorrectly()
    {
        var root = new Territory(Guid.NewGuid(), "Asia Pacific", null, true);
        var child = new Territory(Guid.NewGuid(), "Malaysia", root.Id, false);

        Assert.Equal("Asia Pacific", root.Name);
        Assert.True(root.IsGroup);
        Assert.Equal(root.Id, child.ParentId);
        Assert.False(child.IsGroup);
    }

    [Fact]
    public void Group_Constructors_ThrowOnEmptyName()
    {
        Assert.Throws<ArgumentException>(() => new CustomerGroup(Guid.NewGuid(), ""));
        Assert.Throws<ArgumentException>(() => new SupplierGroup(Guid.NewGuid(), ""));
        Assert.Throws<ArgumentException>(() => new Territory(Guid.NewGuid(), ""));
    }
}
