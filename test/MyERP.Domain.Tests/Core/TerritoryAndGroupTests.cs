using System;
using MyERP.Core.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Core;

public class TerritoryAndGroupTests
{
    [Fact]
    public void Territory_DefaultState()
    {
        var t = new Territory(Guid.NewGuid(), "Malaysia");
        Assert.Equal("Malaysia", t.Name);
        Assert.Null(t.ParentId);
        Assert.False(t.IsGroup);
        Assert.Null(t.TerritoryManagerId);
    }

    [Fact]
    public void Territory_AsGroup()
    {
        var root = new Territory(Guid.NewGuid(), "All Territories", isGroup: true);
        Assert.True(root.IsGroup);
    }

    [Fact]
    public void Territory_WithParent()
    {
        var rootId = Guid.NewGuid();
        var child = new Territory(Guid.NewGuid(), "Selangor", parentId: rootId);
        Assert.Equal(rootId, child.ParentId);
    }

    [Fact]
    public void Territory_NameRequired()
    {
        Assert.Throws<ArgumentException>(() => new Territory(Guid.NewGuid(), ""));
    }

    [Fact]
    public void CustomerGroup_DefaultState()
    {
        var g = new CustomerGroup(Guid.NewGuid(), "Corporate");
        Assert.Equal("Corporate", g.Name);
        Assert.Null(g.ParentId);
        Assert.False(g.IsGroup);
        Assert.Equal(0m, g.DefaultCreditLimit);
        Assert.Null(g.DefaultPaymentTermsTemplateId);
        Assert.Null(g.DefaultPriceListId);
    }

    [Fact]
    public void CustomerGroup_AsGroupWithDefaults()
    {
        var g = new CustomerGroup(Guid.NewGuid(), "All Customer Groups", isGroup: true);
        g.DefaultCreditLimit = 50_000m;
        g.DefaultPriceListId = Guid.NewGuid();

        Assert.True(g.IsGroup);
        Assert.Equal(50_000m, g.DefaultCreditLimit);
    }

    [Fact]
    public void CustomerGroup_Hierarchy()
    {
        var rootId = Guid.NewGuid();
        var root = new CustomerGroup(rootId, "All Customer Groups", isGroup: true);
        var child = new CustomerGroup(Guid.NewGuid(), "Retail", parentId: rootId);

        Assert.Equal(rootId, child.ParentId);
    }

    [Fact]
    public void CustomerGroup_NameRequired()
    {
        Assert.Throws<ArgumentException>(() => new CustomerGroup(Guid.NewGuid(), ""));
    }

    [Fact]
    public void SupplierGroup_DefaultState()
    {
        var g = new SupplierGroup(Guid.NewGuid(), "Local Suppliers");
        Assert.Equal("Local Suppliers", g.Name);
        Assert.Null(g.ParentId);
        Assert.False(g.IsGroup);
        Assert.Null(g.DefaultPaymentTermsTemplateId);
    }

    [Fact]
    public void SupplierGroup_Hierarchy()
    {
        var rootId = Guid.NewGuid();
        var child = new SupplierGroup(Guid.NewGuid(), "Raw Material", parentId: rootId);
        Assert.Equal(rootId, child.ParentId);
    }

    [Fact]
    public void SupplierGroup_NameRequired()
    {
        Assert.Throws<ArgumentException>(() => new SupplierGroup(Guid.NewGuid(), ""));
    }

    [Fact]
    public void Customer_GroupAndTerritory_Defaults()
    {
        var customer = new MyERP.Sales.Entities.Customer(
            Guid.NewGuid(), Guid.NewGuid(), "Test Customer");
        Assert.Null(customer.CustomerGroupId);
        Assert.Null(customer.TerritoryId);
    }

    [Fact]
    public void Customer_GroupAndTerritory_CanBeSet()
    {
        var customer = new MyERP.Sales.Entities.Customer(
            Guid.NewGuid(), Guid.NewGuid(), "Test Customer");
        var groupId = Guid.NewGuid();
        var territoryId = Guid.NewGuid();

        customer.CustomerGroupId = groupId;
        customer.TerritoryId = territoryId;

        Assert.Equal(groupId, customer.CustomerGroupId);
        Assert.Equal(territoryId, customer.TerritoryId);
    }

    [Fact]
    public void Supplier_Group_Default()
    {
        var supplier = new MyERP.Purchasing.Entities.Supplier(
            Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        Assert.Null(supplier.SupplierGroupId);
    }

    [Fact]
    public void Supplier_Group_CanBeSet()
    {
        var supplier = new MyERP.Purchasing.Entities.Supplier(
            Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        var groupId = Guid.NewGuid();
        supplier.SupplierGroupId = groupId;
        Assert.Equal(groupId, supplier.SupplierGroupId);
    }
}
