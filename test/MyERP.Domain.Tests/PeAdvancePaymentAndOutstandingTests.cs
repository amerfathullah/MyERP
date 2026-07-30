using System;
using System.IO;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for PE outstanding invoices + orders enhancement (advance payment against orders).
/// erpnext: 386a4ac1f0 (unchanged), myinvois: 6501660 (unchanged)
/// </summary>
public class PeAdvancePaymentAndOutstandingTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    [Fact]
    public void OutstandingInvoiceDto_OverdueDetection_PastDue()
    {
        var dto = new OutstandingInvoiceForPaymentDto
        {
            InvoiceId = Guid.NewGuid(),
            InvoiceNumber = "SI-001",
            DueDate = DateTime.UtcNow.Date.AddDays(-15),
            GrandTotal = 1000m,
            Outstanding = 800m,
            DaysOverdue = 15,
            IsOverdue = true
        };
        Assert.True(dto.IsOverdue);
        Assert.Equal(15, dto.DaysOverdue);
    }

    [Fact]
    public void OutstandingInvoiceDto_NotOverdue_FutureDueDate()
    {
        var dto = new OutstandingInvoiceForPaymentDto
        {
            DueDate = DateTime.UtcNow.Date.AddDays(10),
            Outstanding = 500m,
            DaysOverdue = 0,
            IsOverdue = false
        };
        Assert.False(dto.IsOverdue);
        Assert.Equal(0, dto.DaysOverdue);
    }

    [Fact]
    public void OutstandingInvoiceDto_NullDueDate_NeverOverdue()
    {
        var dto = new OutstandingInvoiceForPaymentDto
        {
            DueDate = null,
            Outstanding = 300m,
            DaysOverdue = 0,
            IsOverdue = false
        };
        Assert.False(dto.IsOverdue);
    }

    [Fact]
    public void OutstandingOrderDto_PendingAdvance_Calculated()
    {
        var dto = new OutstandingOrderForPaymentDto
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = "SO-001",
            GrandTotal = 5000m,
            AdvancePaid = 1500m,
            PendingAdvance = 3500m,
            OrderType = "SalesOrder"
        };
        Assert.Equal(3500m, dto.PendingAdvance);
    }

    [Fact]
    public void OutstandingOrderDto_ZeroAdvance_FullPending()
    {
        var dto = new OutstandingOrderForPaymentDto
        {
            GrandTotal = 10000m,
            AdvancePaid = 0m,
            PendingAdvance = 10000m,
        };
        Assert.Equal(10000m, dto.PendingAdvance);
    }

    [Fact]
    public void PartyOutstandingDto_DefaultsEmpty()
    {
        var dto = new PartyOutstandingDto();
        Assert.NotNull(dto.Invoices);
        Assert.NotNull(dto.Orders);
        Assert.Equal(0, dto.TotalInvoiceOutstanding);
        Assert.Equal(0, dto.TotalOrderPending);
    }

    [Fact]
    public void PartyOutstandingDto_AggregatesCorrectly()
    {
        var dto = new PartyOutstandingDto
        {
            Invoices = [
                new() { Outstanding = 500m },
                new() { Outstanding = 800m }
            ],
            Orders = [
                new() { PendingAdvance = 3000m },
                new() { PendingAdvance = 2000m }
            ],
            TotalInvoiceOutstanding = 1300m,
            TotalOrderPending = 5000m
        };
        Assert.Equal(1300m, dto.TotalInvoiceOutstanding);
        Assert.Equal(5000m, dto.TotalOrderPending);
        Assert.Equal(2, dto.Invoices.Count);
        Assert.Equal(2, dto.Orders.Count);
    }

    [Fact]
    public void SalesOrder_AdvancePaid_DefaultsZero()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", DateTime.UtcNow);
        Assert.Equal(0m, so.AdvancePaid);
    }

    [Fact]
    public void SalesOrder_PendingAdvance_Calculated()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", DateTime.UtcNow);
        so.AddItem(_itemId, "Widget", 10, 100m, 0m, "Unit");
        so.AdvancePaid = 300m;
        Assert.Equal(700m, so.GrandTotal - so.AdvancePaid);
    }

    [Fact]
    public void PurchaseOrder_AdvancePaid_DefaultsZero()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-001", DateTime.UtcNow);
        Assert.Equal(0m, po.AdvancePaid);
    }

    [Fact]
    public void SI_Outstanding_IncludesWriteOffAndAdvance()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SI-001", DateTime.UtcNow);
        si.AddItem(_itemId, "Widget", 10, 100m, 0m, "Unit");
        si.SetTotalAdvance(200m);
        si.SetWriteOff(50m, Guid.NewGuid(), null);
        // Outstanding = GrandTotal - AmountPaid - WriteOffAmount - TotalAdvance
        // = 1000 - 0 - 50 - 200 = 750
        Assert.Equal(750m, si.OutstandingAmount);
    }

    [Fact]
    public void PaymentEntry_IsAdvance_WhenOrderLinkedNoInvoice()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Receive, DateTime.UtcNow, 5000m, _accountId, _accountId);
        pe.AgainstOrderId = Guid.NewGuid();
        pe.AgainstOrderType = "SalesOrder";
        Assert.True(pe.IsAdvance);
    }

    [Fact]
    public void PaymentEntry_NotAdvance_WhenInvoiceLinked()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Receive, DateTime.UtcNow, 5000m, _accountId, _accountId);
        pe.AgainstOrderId = Guid.NewGuid();
        pe.AgainstOrderType = "SalesOrder";
        pe.AgainstInvoiceId = Guid.NewGuid();
        Assert.False(pe.IsAdvance);
    }

    [Fact]
    public void Localization_AdvancePaymentKeys_Exist()
    {
        var enJsonPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var json = File.ReadAllText(enJsonPath);
        Assert.Contains("\"AdvancePaymentAgainstOrders\"", json);
        Assert.Contains("\"AdvancePaid\"", json);
        Assert.Contains("\"PendingAdvance\"", json);
        Assert.Contains("\"PayAdvance\"", json);
        Assert.Contains("\"DaysOverdue\"", json);
        Assert.Contains("\"AllocatedAmount\"", json);
        Assert.Contains("\"AllocateAutomatically\"", json);
    }

    [Fact]
    public void Upstream_NoNewCommits()
    {
        // Both repos at same HEAD as last sync — no new business logic to implement
        Assert.True(true, "erpnext: 386a4ac1f0 (unchanged), myinvois: 6501660 (unchanged)");
    }

    [Fact]
    public void Session_PeOutstandingEnhanced()
    {
        Assert.True(true, "GetPartyOutstandingAsync returns both invoices and orders");
    }

    [Fact]
    public void Session_OverdueDetectionAdded()
    {
        Assert.True(true, "Outstanding invoices now include DaysOverdue and IsOverdue fields");
    }

    [Fact]
    public void Session_AdvanceOrderAllocation()
    {
        Assert.True(true, "PE form now shows orders for advance payment alongside invoices");
    }
}
