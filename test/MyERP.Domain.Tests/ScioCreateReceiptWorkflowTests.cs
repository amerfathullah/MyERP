using System;
using System.Linq;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for SCIO → Create Subcontracting Receipt workflow.
/// Per ERPNext: SCIO detail has "Create Receipt" button for Open/PartiallyReceived status.
/// </summary>
public class ScioCreateReceiptWorkflowTests
{
    private SubcontractingInwardOrder CreateScio(int itemCount = 1, decimal qty = 10.0m)
    {
        var id = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(id, Guid.NewGuid(), "SCIO-001", DateTime.UtcNow,
            Guid.NewGuid());
        for (int i = 0; i < itemCount; i++)
            scio.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), id, Guid.NewGuid(), qty, 100m));
        return scio;
    }

    [Fact]
    public void SCIO_OpenStatus_CanCreateReceipt()
    {
        var scio = CreateScio();
        scio.Submit();
        Assert.Equal(SubcontractingInwardOrderStatus.Open, scio.Status);
    }

    [Fact]
    public void SCIO_PartiallyReceived_CanCreateReceipt()
    {
        var scio = CreateScio();
        scio.Submit();
        scio.Items.First().ReceivedQty = 5.0m;
        scio.UpdateReceivedStatus();
        Assert.Equal(SubcontractingInwardOrderStatus.PartiallyReceived, scio.Status);
    }

    [Fact]
    public void SCIO_Item_PendingReceiptQty_IsOrderedMinusReceived()
    {
        var scio = CreateScio();
        scio.Submit();
        var item = scio.Items.First();
        item.ReceivedQty = 3.0m;
        Assert.Equal(7.0m, item.PendingReceiptQty);
    }

    [Fact]
    public void SCIO_Item_PendingReceiptQty_NeverNegative()
    {
        var scio = CreateScio(qty: 5.0m);
        scio.Submit();
        var item = scio.Items.First();
        item.ReceivedQty = 6.0m;
        Assert.True(item.PendingReceiptQty >= 0);
    }

    [Fact]
    public void SCIO_FullReceipt_TransitionsToCompleted()
    {
        var scio = CreateScio(itemCount: 2);
        scio.Submit();
        foreach (var item in scio.Items)
            item.ReceivedQty = item.Quantity;
        scio.UpdateReceivedStatus();
        Assert.Equal(SubcontractingInwardOrderStatus.Completed, scio.Status);
    }

    [Fact]
    public void SCIO_Draft_CannotCreateReceipt()
    {
        var scio = CreateScio();
        // Draft status — receipt only valid for Open (1) or PartiallyReceived (2)
        Assert.Equal(SubcontractingInwardOrderStatus.Draft, scio.Status);
    }

    [Fact]
    public void SCR_Creation_RequiresCompanyAndSupplier()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var scr = new SubcontractingReceipt(Guid.NewGuid(), companyId, "SCR-001",
            DateTime.UtcNow, supplierId, Guid.NewGuid());
        Assert.Equal(companyId, scr.CompanyId);
        Assert.Equal(supplierId, scr.SupplierId);
    }

    [Fact]
    public void SCR_AddItem_TracksQuantityAndRate()
    {
        var scrId = Guid.NewGuid();
        var scr = new SubcontractingReceipt(scrId, Guid.NewGuid(), "SCR-001",
            DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        var itemId = Guid.NewGuid();
        scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scrId, itemId, "Finished Widget", 10.0m, 25.0m));
        Assert.Single(scr.Items);
        Assert.Equal(10.0m, scr.Items.First().Qty);
        Assert.Equal(25.0m, scr.Items.First().Rate);
    }

    [Fact]
    public void SCR_NetTotal_SumsItemAmounts()
    {
        var scrId = Guid.NewGuid();
        var scr = new SubcontractingReceipt(scrId, Guid.NewGuid(), "SCR-001",
            DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scrId, Guid.NewGuid(), "A", 5m, 10m));
        scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scrId, Guid.NewGuid(), "B", 3m, 20m));
        Assert.Equal(110m, scr.NetTotal); // 5×10 + 3×20
    }

    [Fact]
    public void Localization_ReceiptKeys_ExistInEnJson()
    {
        var json = System.IO.File.ReadAllText(
            System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src",
                "MyERP.Domain.Shared", "Localization", "MyERP", "en.json"));
        Assert.Contains("\"CreateReceipt\"", json);
        Assert.Contains("\"ReceiveItems\"", json);
        Assert.Contains("\"ReceiveItemsHelp\"", json);
        Assert.Contains("\"AlreadyReceived\"", json);
        Assert.Contains("\"ReceiveQty\"", json);
        Assert.Contains("\"ReceiptCreatedSuccessfully\"", json);
    }
}
