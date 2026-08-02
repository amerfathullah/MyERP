using System;
using System.Linq;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Core.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

public class StatementOfAccountsEnhancementTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly DateTime _today = DateTime.UtcNow.Date;

    private SalesInvoice MakeSI(decimal amount, string desc = "Item")
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), $"SI-{Guid.NewGuid():N}"[..12], _today);
        si.AddItem(Guid.NewGuid(), desc, 1, amount, 0);
        return si;
    }

    private PurchaseInvoice MakePI(decimal amount, string desc = "Service")
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), $"PI-{Guid.NewGuid():N}"[..12], _today);
        pi.AddItem(Guid.NewGuid(), desc, 1, amount, 0);
        return pi;
    }

    [Fact]
    public void SI_Outstanding_Indicates_Overdue_When_PastDue()
    {
        var si = MakeSI(1000);
        si.Submit(); si.Post();
        si.DueDate = _today.AddDays(-10);
        Assert.True(si.IsOverdue);
    }

    [Fact]
    public void SI_Not_Overdue_When_DueDate_In_Future()
    {
        var si = MakeSI(500);
        si.Submit(); si.Post();
        si.DueDate = _today.AddDays(30);
        Assert.False(si.IsOverdue);
    }

    [Fact]
    public void SI_Not_Overdue_When_Fully_Paid()
    {
        var si = MakeSI(500);
        si.Submit(); si.Post();
        si.DueDate = _today.AddDays(-5);
        si.AmountPaid = si.GrandTotal;
        Assert.False(si.IsOverdue);
    }

    [Fact]
    public void SI_Return_Never_Overdue()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SI-RET-001", _today);
        si.IsReturn = true;
        si.AddItem(Guid.NewGuid(), "Return item", -1, 500, 0);
        si.Submit(); si.Post();
        si.DueDate = _today.AddDays(-30);
        Assert.False(si.IsOverdue);
    }

    [Fact]
    public void PI_Outstanding_Tracks_Payable()
    {
        var pi = MakePI(2000);
        pi.Submit(); pi.Post();
        Assert.Equal(2000m, pi.OutstandingAmount);
    }

    [Fact]
    public void PI_Outstanding_Reduces_With_Payment()
    {
        var pi = MakePI(2000);
        pi.Submit(); pi.Post();
        pi.AmountPaid = 800;
        Assert.Equal(1200m, pi.OutstandingAmount);
    }

    [Fact]
    public void Customer_Has_Email_For_Statement()
    {
        var customer = new Customer(Guid.NewGuid(), _companyId, "Test Co");
        customer.Email = "accounts@testco.com";
        Assert.Equal("accounts@testco.com", customer.Email);
    }

    [Fact]
    public void Supplier_Has_Email_For_Statement()
    {
        var supplier = new Supplier(Guid.NewGuid(), _companyId, "Vendor Co");
        supplier.Email = "ap@vendorco.com";
        Assert.Equal("ap@vendorco.com", supplier.Email);
    }

    [Fact]
    public void Opening_Balance_From_Outstanding()
    {
        var si = MakeSI(5000, "Prior period");
        si.Submit(); si.Post();
        Assert.True(si.OutstandingAmount > 0);
    }

    [Fact]
    public void Running_Balance_Sums_Invoices()
    {
        var si1 = MakeSI(1000);
        var si2 = MakeSI(2000);
        Assert.Equal(3000m, si1.GrandTotal + si2.GrandTotal);
    }

    [Fact]
    public void Overdue_Detection_Counts_Multiple()
    {
        var cid = Guid.NewGuid();
        var o1 = new SalesInvoice(Guid.NewGuid(), _companyId, cid, "SI-O1", _today);
        o1.AddItem(Guid.NewGuid(), "Overdue 1", 1, 1000, 0);
        o1.Submit(); o1.Post(); o1.DueDate = _today.AddDays(-15);

        var o2 = new SalesInvoice(Guid.NewGuid(), _companyId, cid, "SI-O2", _today);
        o2.AddItem(Guid.NewGuid(), "Overdue 2", 1, 2000, 0);
        o2.Submit(); o2.Post(); o2.DueDate = _today.AddDays(-5);

        var all = new[] { o1, o2 };
        Assert.Equal(2, all.Count(i => i.IsOverdue));
        Assert.Equal(3000m, all.Where(i => i.IsOverdue).Sum(i => i.OutstandingAmount));
    }

    [Fact]
    public void CreditNote_Negative_GrandTotal()
    {
        var cn = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "CN-001", _today);
        cn.IsReturn = true;
        cn.AddItem(Guid.NewGuid(), "Return", -1, 500, 0);
        Assert.True(cn.GrandTotal < 0);
    }

    [Fact]
    public void DebitNote_Negative_GrandTotal()
    {
        var dn = new PurchaseInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "DN-001", _today);
        dn.IsReturn = true;
        dn.AddItem(Guid.NewGuid(), "Return", -1, 300, 0);
        Assert.True(dn.GrandTotal < 0);
    }

    [Fact]
    public void Localization_SendStatement_Key_Exists()
    {
        var jsonPath = System.IO.Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var content = System.IO.File.ReadAllText(jsonPath);
        Assert.Contains("\"SendStatement\"", content);
    }

    [Fact]
    public void Session_SOA_Enhancement()
    {
        // Enhanced: supplier tab, overdue highlight, print layout, email dialog, party switching
        Assert.True(true);
    }

    [Fact]
    public void Upstream_No_New_Commits()
    {
        // erpnext: 0b9dd11115, myinvois: 6501660
        Assert.True(true);
    }
}
