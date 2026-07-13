using System;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Core;

public class InterCompanyTransactionTests
{
    [Fact]
    public void Customer_RepresentsCompanyId_DefaultNull()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "ABC Corp");
        customer.RepresentsCompanyId.ShouldBeNull();
    }

    [Fact]
    public void Customer_CanSetRepresentsCompanyId()
    {
        var companyId = Guid.NewGuid();
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Branch B");
        customer.RepresentsCompanyId = companyId;
        customer.RepresentsCompanyId.ShouldBe(companyId);
    }

    [Fact]
    public void Supplier_RepresentsCompanyId_DefaultNull()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "XYZ Ltd");
        supplier.RepresentsCompanyId.ShouldBeNull();
    }

    [Fact]
    public void Supplier_CanSetRepresentsCompanyId()
    {
        var companyId = Guid.NewGuid();
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Parent Corp");
        supplier.RepresentsCompanyId = companyId;
        supplier.RepresentsCompanyId.ShouldBe(companyId);
    }

    [Fact]
    public void PurchaseInvoice_InterCompanyInvoiceId_DefaultNull()
    {
        var pi = new PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-001", DateTime.UtcNow);
        pi.InterCompanyInvoiceId.ShouldBeNull();
    }

    [Fact]
    public void PurchaseInvoice_CanSetInterCompanyInvoiceId()
    {
        var siId = Guid.NewGuid();
        var pi = new PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "IC-SI-001", DateTime.UtcNow);
        pi.InterCompanyInvoiceId = siId;
        pi.InterCompanyInvoiceId.ShouldBe(siId);
    }

    [Fact]
    public void InterCompany_BidirectionalLink_Pattern()
    {
        // Company A customer "Branch B" represents Company B
        // Company B supplier "Head Office" represents Company A
        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();

        var customerInA = new Customer(Guid.NewGuid(), companyAId, "Branch B");
        customerInA.RepresentsCompanyId = companyBId;

        var supplierInB = new Supplier(Guid.NewGuid(), companyBId, "Head Office");
        supplierInB.RepresentsCompanyId = companyAId;

        // Bidirectional validation: customer's company matches supplier's RepresentsCompany
        customerInA.RepresentsCompanyId.ShouldBe(supplierInB.CompanyId);
        supplierInB.RepresentsCompanyId.ShouldBe(customerInA.CompanyId);
    }
}
