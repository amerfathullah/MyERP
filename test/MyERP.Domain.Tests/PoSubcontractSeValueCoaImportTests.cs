using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for PO IsSubcontracted, StockEntry value tracking,
/// PO cancel guard with SCO, and CoA import AppService DTOs.
/// </summary>
public class PoSubcontractSeValueCoaImportTests
{
    // --- PurchaseOrder.IsSubcontracted ---

    [Fact]
    public void PO_IsSubcontracted_Defaults_False()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        Assert.False(po.IsSubcontracted);
    }

    [Fact]
    public void PO_IsSubcontracted_CanBeSet()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        po.IsSubcontracted = true;
        Assert.True(po.IsSubcontracted);
    }

    [Fact]
    public void PO_IsSubcontracted_Independent_From_Status()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        po.IsSubcontracted = true;
        po.AddItem(Guid.NewGuid(), "Service Item", 10, 100, 0);
        po.Submit();
        Assert.True(po.IsSubcontracted);
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
    }

    // --- StockEntry.TotalIncomingValue / TotalOutgoingValue ---

    [Fact]
    public void SE_TotalIncomingValue_Empty_IsZero()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.Today);
        Assert.Equal(0, se.TotalIncomingValue);
        Assert.Equal(0, se.TotalOutgoingValue);
        Assert.Equal(0, se.TotalValueDifference);
    }

    [Fact]
    public void SE_TotalIncomingValue_ReceiptItems()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.Today);
        var wh = Guid.NewGuid();
        se.AddItem(Guid.NewGuid(), 10, null, wh, 50m); // 10 × 50 = 500 incoming
        se.AddItem(Guid.NewGuid(), 5, null, wh, 100m);  // 5 × 100 = 500 incoming

        Assert.Equal(1000m, se.TotalIncomingValue);
        Assert.Equal(0m, se.TotalOutgoingValue);
        Assert.Equal(1000m, se.TotalValueDifference);
    }

    [Fact]
    public void SE_TotalOutgoingValue_IssueItems()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialIssue, DateTime.Today);
        var wh = Guid.NewGuid();
        se.AddItem(Guid.NewGuid(), 8, wh, null, 25m); // 8 × 25 = 200 outgoing

        Assert.Equal(0m, se.TotalIncomingValue);
        Assert.Equal(200m, se.TotalOutgoingValue);
        Assert.Equal(-200m, se.TotalValueDifference);
    }

    [Fact]
    public void SE_TotalValueDifference_Transfer_Balanced()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialTransfer, DateTime.Today);
        var src = Guid.NewGuid();
        var tgt = Guid.NewGuid();
        // Transfer: item moves from src → tgt at same rate
        se.AddItem(Guid.NewGuid(), 10, src, tgt, 30m); // both source AND target → counted in both

        // Item has both source and target → counts as both incoming and outgoing
        Assert.Equal(300m, se.TotalIncomingValue); // target warehouse
        Assert.Equal(300m, se.TotalOutgoingValue);  // source warehouse
        Assert.Equal(0m, se.TotalValueDifference);  // balanced transfer
    }

    [Fact]
    public void SE_TotalIncomingValue_NullRate_TreatedAsZero()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.Today);
        se.AddItem(Guid.NewGuid(), 10, null, Guid.NewGuid(), null); // rate is null

        Assert.Equal(0m, se.TotalIncomingValue);
    }

    [Fact]
    public void SE_TotalIncomingValue_MultiItem_Sum()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.Today);
        var wh = Guid.NewGuid();
        se.AddItem(Guid.NewGuid(), 1, null, wh, 1000m);
        se.AddItem(Guid.NewGuid(), 2, null, wh, 500m);
        se.AddItem(Guid.NewGuid(), 3, null, wh, 200m);

        // 1×1000 + 2×500 + 3×200 = 1000 + 1000 + 600 = 2600
        Assert.Equal(2600m, se.TotalIncomingValue);
    }

    // --- StockEntry IAccountableDocument ---

    [Fact]
    public void SE_IAccountableDocument_GrandTotal_Matches_NetTotal()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.Today);
        var wh = Guid.NewGuid();
        se.AddItem(Guid.NewGuid(), 10, null, wh, 50m);

        var doc = (MyERP.Accounting.DomainServices.IAccountableDocument)se;
        Assert.Equal(doc.NetTotal, doc.GrandTotal);
        Assert.Equal(500m, doc.GrandTotal);
        Assert.Equal(0m, doc.TaxAmount);
    }

    // --- SCO cancel guard concept ---

    [Fact]
    public void SCO_Status_Enum_HasCorrectValues()
    {
        Assert.Equal(0, (int)SubcontractingOrderStatus.Draft);
        Assert.Equal(1, (int)SubcontractingOrderStatus.Open);
        Assert.Equal(5, (int)SubcontractingOrderStatus.Cancelled);
    }

    [Fact]
    public void SCO_PurchaseOrderId_CanBeLinked()
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), Guid.NewGuid(), "SCO-001", DateTime.Today, Guid.NewGuid());
        var poId = Guid.NewGuid();
        sco.PurchaseOrderId = poId;
        Assert.Equal(poId, sco.PurchaseOrderId);
    }

    // --- CoA Import DTO tests ---

    [Fact]
    public void CoaImportResultDto_Properties()
    {
        var result = new MyERP.Accounting.CoaImportResultDto
        {
            AccountsCreated = 45,
            CompanyId = Guid.NewGuid()
        };
        Assert.Equal(45, result.AccountsCreated);
        Assert.NotEqual(Guid.Empty, result.CompanyId);
    }

    [Fact]
    public void ImportCoaDto_HasRowsCollection()
    {
        var dto = new MyERP.Accounting.ImportCoaDto
        {
            CompanyId = Guid.NewGuid(),
            Rows = new()
            {
                new MyERP.Accounting.ImportCoaRowDto
                {
                    AccountCode = "1000",
                    AccountName = "Assets",
                    AccountType = MyERP.Accounting.AccountType.Asset,
                    IsGroup = true
                },
                new MyERP.Accounting.ImportCoaRowDto
                {
                    AccountCode = "1100",
                    AccountName = "Current Assets",
                    AccountType = MyERP.Accounting.AccountType.Asset,
                    IsGroup = true,
                    ParentCode = "1000"
                }
            }
        };
        Assert.Equal(2, dto.Rows.Count);
        Assert.Equal("1000", dto.Rows[0].AccountCode);
        Assert.Equal("1100", dto.Rows[1].AccountCode);
        Assert.Equal("1000", dto.Rows[1].ParentCode);
    }

    [Fact]
    public void CoaTemplateRowDto_SupportsSubType()
    {
        var dto = new MyERP.Accounting.CoaTemplateRowDto
        {
            AccountCode = "1130",
            AccountName = "Accounts Receivable",
            AccountType = MyERP.Accounting.AccountType.Asset,
            SubType = MyERP.Accounting.AccountSubType.AccountsReceivable
        };
        Assert.Equal(MyERP.Accounting.AccountSubType.AccountsReceivable, dto.SubType);
        Assert.False(dto.IsGroup);
    }

    // --- PO AdvancePaid ---

    [Fact]
    public void PO_AdvancePaid_CanBeSet()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Item A", 10, 100m, 0); // GrandTotal = 1000
        po.AdvancePaid = 300m;
        Assert.Equal(300m, po.AdvancePaid);
        Assert.Equal(1000m, po.GrandTotal);
    }

    // --- StockEntry value with Manufacture ---

    [Fact]
    public void SE_Manufacture_IncomingFG_OutgoingRM()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.Today);
        var wipWh = Guid.NewGuid();
        var fgWh = Guid.NewGuid();

        // RM consumed (outgoing from WIP)
        se.AddItem(Guid.NewGuid(), 5, wipWh, null, 20m);  // 5 × 20 = 100 outgoing
        se.AddItem(Guid.NewGuid(), 3, wipWh, null, 30m);  // 3 × 30 = 90 outgoing

        // FG produced (incoming to FG warehouse)
        se.AddItem(Guid.NewGuid(), 1, null, fgWh, 190m); // 1 × 190 = 190 incoming

        Assert.Equal(190m, se.TotalIncomingValue);
        Assert.Equal(190m, se.TotalOutgoingValue);
        Assert.Equal(0m, se.TotalValueDifference); // cost absorbed
    }

    // --- PO cancel guard concept test ---

    [Fact]
    public void PO_Cancel_From_ToDeliverAndBill_Succeeds()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Item", 10, 50m, 0);
        po.Submit();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
        po.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, po.Status);
    }
}
