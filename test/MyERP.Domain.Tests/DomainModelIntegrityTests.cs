using System;
using System.Linq;
using System.Reflection;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Accounting.Entities;
using MyERP.HumanResources.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Xunit;

namespace MyERP;

/// <summary>
/// Structural integrity tests that verify domain model conventions are followed.
/// Catches issues that would cause silent runtime failures.
/// </summary>
public class DomainModelIntegrityTests
{
    // === Entity Convention Tests ===

    [Fact]
    public void AllAggregateRoots_Have_TenantId()
    {
        // All business entities must implement IMultiTenant for data isolation
        var aggregateTypes = typeof(SalesInvoice).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.BaseType != null && t.BaseType.IsGenericType
                && t.BaseType.GetGenericTypeDefinition() == typeof(FullAuditedAggregateRoot<>))
            .ToList();

        Assert.True(aggregateTypes.Count > 20, $"Expected >20 aggregate roots, found {aggregateTypes.Count}");

        var missingTenant = aggregateTypes
            .Where(t => !t.GetInterfaces().Any(i => i.Name == "IMultiTenant"))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(missingTenant); // All should implement IMultiTenant
    }

    [Fact]
    public void CriticalEntities_Have_CompanyId()
    {
        // Transaction entities must have CompanyId for multi-company tenants
        var transactionTypes = new[]
        {
            typeof(SalesInvoice), typeof(SalesOrder), typeof(DeliveryNote),
            typeof(PurchaseOrder), typeof(PurchaseInvoice), typeof(PurchaseReceipt),
            typeof(JournalEntry), typeof(StockEntry), typeof(WorkOrder),
        };

        foreach (var type in transactionTypes)
        {
            var prop = type.GetProperty("CompanyId");
            Assert.NotNull(prop); // $"{type.Name} must have CompanyId"
            Assert.Equal(typeof(Guid), prop!.PropertyType);
        }
    }

    [Fact]
    public void SalesInvoice_Has_Required_Properties()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        Assert.NotNull(si.InvoiceNumber);
        Assert.Equal(DateTime.Today, si.IssueDate);
        Assert.Equal(0m, si.AmountPaid);
        Assert.Equal(0m, si.GrandTotal);
    }

    [Fact]
    public void PurchaseOrder_Has_Required_Properties()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        Assert.NotNull(po.OrderNumber);
        Assert.Equal(DateTime.Today, po.OrderDate);
    }

    [Fact]
    public void SalesOrder_Has_Required_Properties()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        Assert.NotNull(so.OrderNumber);
        Assert.Equal(DateTime.Today, so.OrderDate);
    }

    // === Status Machine Tests ===

    [Fact]
    public void SalesInvoice_DraftToPosted_Lifecycle()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-LC", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        Assert.Equal(DocumentStatus.Draft, si.Status);

        si.Submit();
        Assert.Equal(DocumentStatus.Submitted, si.Status);

        si.Post();
        Assert.Equal(DocumentStatus.Posted, si.Status);
    }

    [Fact]
    public void SalesInvoice_CannotSubmitTwice()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-DBL", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        si.Submit();
        Assert.Throws<Volo.Abp.BusinessException>(() => si.Submit());
    }

    [Fact]
    public void SalesOrder_CannotCancelAlreadyCancelled()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-CD", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        so.Cancel(); // first cancel works
        Assert.Throws<Volo.Abp.BusinessException>(() => so.Cancel()); // double cancel blocked
    }

    [Fact]
    public void PurchaseOrder_StatusProgression()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-SP", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Item", 10m, 50m, 0m, "Unit");
        Assert.Equal(DocumentStatus.Draft, po.Status);

        po.Submit();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
    }

    // === Computed Property Tests ===

    [Fact]
    public void SalesInvoice_OutstandingAmount_Computed()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-OA", DateTime.Today);
        si.GrandTotal = 5000m;
        si.AmountPaid = 2000m;
        Assert.Equal(3000m, si.OutstandingAmount);
    }

    [Fact]
    public void Bin_ProjectedQty_Computed()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 200m;
        bin.OrderedQty = 100m;
        bin.ReservedQty = 50m;
        // 200 + 100 - 50 = 250
        Assert.Equal(250m, bin.ProjectedQty);
    }

    [Fact]
    public void PurchaseOrderItem_PendingQty_Computed()
    {
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test", 100m, 25m, 0m, "Unit");
        item.ReceivedQty = 60m;
        Assert.Equal(40m, item.PendingReceiptQty);
    }

    // === Immutability/Guard Tests ===

    [Fact]
    public void WorkOrder_CannotProduceFromDraft()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-D", Guid.NewGuid(), Guid.NewGuid(), 100m);
        Assert.Throws<Volo.Abp.BusinessException>(() => wo.RecordProduction(10m, 0m));
    }

    [Fact]
    public void WorkOrder_CannotStartWithoutSubmit()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-NS", Guid.NewGuid(), Guid.NewGuid(), 50m);
        Assert.Throws<Volo.Abp.BusinessException>(() => wo.Start());
    }

    [Fact]
    public void SalesOrder_AddItem_RejectsZeroQty()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-ZQ", DateTime.Today);
        Assert.Throws<ArgumentException>(() => so.AddItem(Guid.NewGuid(), "Item", 0m, 100m, 0m));
    }

    [Fact]
    public void PurchaseOrder_AddItem_RejectsNegativeQty()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-NQ", DateTime.Today);
        Assert.Throws<ArgumentException>(() => po.AddItem(Guid.NewGuid(), "Item", -5m, 10m, 0m, "Unit"));
    }

    // === Entity Relationship Tests ===

    [Fact]
    public void SalesOrder_Items_Collection_Works()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-IT", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item A", 5m, 100m, 0m);
        so.AddItem(Guid.NewGuid(), "Item B", 10m, 50m, 0m);
        Assert.Equal(2, so.Items.Count);
    }

    [Fact]
    public void BillOfMaterials_Items_Collection_Works()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-COL", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Part A", 2m, 10m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Part B", 5m, 3m));
        Assert.Equal(2, bom.Items.Count);
    }

    [Fact]
    public void Workstation_Costs_Build_HourRate()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "WS-Rate");
        ws.AddCost("Labor", 40m);
        ws.AddCost("Power", 15m);
        ws.AddCost("Maintenance", 5m);
        Assert.Equal(60m, ws.HourRate);
    }

    // === Default Value Tests ===

    [Fact]
    public void NewEntities_HaveCorrectDefaults()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-DEF", DateTime.Today);
        Assert.Equal(DocumentStatus.Draft, si.Status);
        Assert.False(si.IsReturn);
        Assert.False(si.UpdateStock);
        Assert.False(si.IsOpening);
        Assert.Equal(1m, si.ExchangeRate);

        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-DEF", DateTime.Today);
        Assert.Equal(DocumentStatus.Draft, po.Status);
        Assert.Equal(0m, po.AdvancePaid);
    }

    [Fact]
    public void Customer_Defaults()
    {
        var c = new Customer(Guid.NewGuid(), Guid.NewGuid(), "New Customer");
        Assert.Equal(0m, c.CreditLimit);
        Assert.Null(c.LoyaltyProgramId);
        Assert.Null(c.RepresentsCompanyId);
    }

    [Fact]
    public void Supplier_Defaults()
    {
        var s = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "New Supplier");
        Assert.False(s.PreventPurchaseOrders);
        Assert.False(s.PreventRfqs);
        Assert.Null(s.TaxWithholdingCategory);
    }
}
