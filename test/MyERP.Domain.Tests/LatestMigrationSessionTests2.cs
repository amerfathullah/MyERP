using System;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Accounting;
using MyERP.Assets;
using MyERP.Assets.Entities;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Xunit;

namespace MyERP.Domain.Tests;

public class LatestMigrationSessionTests2
{
    // === 3-Way Matching ===

    [Fact]
    public void ThreeWayMatching_WithinReceived_Passes()
    {
        var pi = CreateTestPI();
        pi.AddItem(Guid.NewGuid(), "Item A", 10, 100, 0);
        pi.Items.First().PurchaseOrderItemId = Guid.NewGuid();
        var poItemId = pi.Items.First().PurchaseOrderItemId!.Value;
        Func<Guid, decimal> getReceived = id => id == poItemId ? 15m : 0m;
        MyERP.Purchasing.DomainServices.PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: true);
    }

    [Fact]
    public void ThreeWayMatching_ExceedsReceived_Throws()
    {
        var pi = CreateTestPI();
        pi.AddItem(Guid.NewGuid(), "Item A", 20, 100, 0);
        pi.Items.First().PurchaseOrderItemId = Guid.NewGuid();
        var poItemId = pi.Items.First().PurchaseOrderItemId!.Value;
        Func<Guid, decimal> getReceived = id => id == poItemId ? 10m : 0m;
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            MyERP.Purchasing.DomainServices.PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: true));
    }

    [Fact]
    public void ThreeWayMatching_ExactMatch_Passes()
    {
        var pi = CreateTestPI();
        pi.AddItem(Guid.NewGuid(), "Item A", 10, 100, 0);
        pi.Items.First().PurchaseOrderItemId = Guid.NewGuid();
        var poItemId = pi.Items.First().PurchaseOrderItemId!.Value;
        Func<Guid, decimal> getReceived = id => id == poItemId ? 10m : 0m;
        MyERP.Purchasing.DomainServices.PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: true);
    }

    [Fact]
    public void ThreeWayMatching_PRNotRequired_AlwaysPasses()
    {
        var pi = CreateTestPI();
        pi.AddItem(Guid.NewGuid(), "Item A", 100, 100, 0);
        pi.Items.First().PurchaseOrderItemId = Guid.NewGuid();
        Func<Guid, decimal> getReceived = id => 0m;
        MyERP.Purchasing.DomainServices.PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: false);
    }

    [Fact]
    public void ThreeWayMatching_NoPOLink_Passes()
    {
        var pi = CreateTestPI();
        pi.AddItem(Guid.NewGuid(), "Item A", 10, 100, 0);
        Func<Guid, decimal> getReceived = id => 0m;
        MyERP.Purchasing.DomainServices.PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: true);
    }

    [Fact]
    public void ThreeWayMatching_MultiItem_SecondExceeds()
    {
        var pi = CreateTestPI();
        pi.AddItem(Guid.NewGuid(), "Item A", 5, 100, 0);
        pi.AddItem(Guid.NewGuid(), "Item B", 20, 50, 0);
        var po1 = Guid.NewGuid(); var po2 = Guid.NewGuid();
        pi.Items.ElementAt(0).PurchaseOrderItemId = po1;
        pi.Items.ElementAt(1).PurchaseOrderItemId = po2;
        Func<Guid, decimal> getReceived = id => id == po1 ? 10m : id == po2 ? 10m : 0m;
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            MyERP.Purchasing.DomainServices.PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: true));
    }

    // === Account Category ===

    [Fact]
    public void AccountCategory_Create()
    {
        var cat = new AccountCategory(Guid.NewGuid(), "Cash and Cash Equivalents", "Asset");
        Assert.Equal("Cash and Cash Equivalents", cat.Name);
        Assert.Equal("Asset", cat.RootType);
    }

    [Fact]
    public void AccountCategory_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AccountCategory(Guid.NewGuid(), "", "Asset"));
    }

    [Fact]
    public void AccountCategory_EmptyRootType_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AccountCategory(Guid.NewGuid(), "COGS", ""));
    }

    [Fact]
    public void AccountCategory_Rename()
    {
        var cat = new AccountCategory(Guid.NewGuid(), "Old", "Expense");
        cat.Rename("Cost of Goods Sold");
        Assert.Equal("Cost of Goods Sold", cat.Name);
    }

    [Fact]
    public void Account_AccountCategoryId_DefaultsNull()
    {
        var a = new Account(Guid.NewGuid(), Guid.NewGuid(), "1100", "Cash", AccountType.Asset);
        Assert.Null(a.AccountCategoryId);
    }

    [Fact]
    public void Account_AccountCategoryId_CanBeSet()
    {
        var a = new Account(Guid.NewGuid(), Guid.NewGuid(), "1100", "Cash", AccountType.Asset);
        var catId = Guid.NewGuid();
        a.AccountCategoryId = catId;
        Assert.Equal(catId, a.AccountCategoryId);
    }

    // === Multi-Book Depreciation ===

    [Fact]
    public void DepreciationScheduleEntry_FinanceBookId_DefaultsNull()
    {
        var e = new DepreciationScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.Date, 1000m, 0m);
        Assert.Null(e.FinanceBookId);
    }

    [Fact]
    public void DepreciationScheduleEntry_FinanceBookId_CanBeSet()
    {
        var bookId = Guid.NewGuid();
        var e = new DepreciationScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.Date, 1000m, 0m);
        e.FinanceBookId = bookId;
        Assert.Equal(bookId, e.FinanceBookId);
    }

    [Fact]
    public void AssetDepreciationDetail_PerBookValueTracking()
    {
        var d = new AssetDepreciationDetail(Guid.NewGuid(), Guid.NewGuid(), Assets.DepreciationMethod.StraightLine, 60, 12, 100_000m);
        d.ValueAfterDepreciation -= 1_500m;
        Assert.Equal(98_500m, d.ValueAfterDepreciation);
    }

    [Fact]
    public void Asset_MultiBook_TwoDetails()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "LAPTOP-001", "Laptop", DateTime.UtcNow, 5_000m);
        asset.DepreciationDetails.Add(new AssetDepreciationDetail(Guid.NewGuid(), asset.Id, Assets.DepreciationMethod.StraightLine, 60, 12, 5_000m) { FinanceBookId = Guid.NewGuid() });
        asset.DepreciationDetails.Add(new AssetDepreciationDetail(Guid.NewGuid(), asset.Id, Assets.DepreciationMethod.WrittenDownValue, 96, 12, 5_000m) { FinanceBookId = Guid.NewGuid(), Rate = 25m });
        Assert.Equal(2, asset.DepreciationDetails.Count);
    }

    // === Delivery Schedule ===

    [Fact]
    public void DeliveryScheduleEntry_Defaults()
    {
        var e = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(30), 100m, null);
        Assert.Equal(100m, e.ScheduledQty);
        Assert.Equal(0m, e.DeliveredQty);
        Assert.Equal(100m, e.PendingQty);
        Assert.False(e.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_RecordDelivery()
    {
        var e = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(30), 100m, null);
        e.RecordDelivery(40m);
        Assert.Equal(40m, e.DeliveredQty);
        Assert.Equal(60m, e.PendingQty);
    }

    [Fact]
    public void DeliveryScheduleEntry_FullDelivery()
    {
        var e = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(30), 50m, null);
        e.RecordDelivery(50m);
        Assert.True(e.IsFullyDelivered);
    }

    // === Payment Entry Tax ===

    [Fact]
    public void PaymentEntryTax_OnPaidAmount()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()) { Rate = 6m, ChargeType = PaymentTaxChargeType.OnPaidAmount };
        tax.Calculate(10_000m, 1m);
        Assert.Equal(600m, tax.TaxAmount);
    }

    [Fact]
    public void PaymentEntryTax_WithExchangeRate()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()) { Rate = 10m, ChargeType = PaymentTaxChargeType.OnPaidAmount };
        tax.Calculate(1_000m, 4.72m);
        Assert.Equal(100m, tax.TaxAmount);
        Assert.Equal(472m, tax.BaseTaxAmount);
    }

    [Fact]
    public void PaymentEntryTax_Actual()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()) { TaxAmount = 250m, ChargeType = PaymentTaxChargeType.Actual };
        tax.Calculate(10_000m, 1m);
        Assert.Equal(250m, tax.TaxAmount);
    }

    [Fact]
    public void PaymentEntryTax_Defaults()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.False(tax.IncludedInPaidAmount);
        Assert.False(tax.IsExchangeGainLoss);
    }

    // === FinanceBook ===

    [Fact]
    public void FinanceBook_Create()
    {
        var fb = new FinanceBook(Guid.NewGuid(), Guid.NewGuid(), "Tax Book");
        Assert.Equal("Tax Book", fb.Name);
        Assert.False(fb.IsDefault);
    }

    [Fact]
    public void JournalEntryLine_FinanceBook_DefaultsNull()
    {
        var l = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1000m, true);
        Assert.Null(l.FinanceBook);
    }

    [Fact]
    public void JournalEntryLine_FinanceBook_CanBeSet()
    {
        var l = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1000m, true);
        l.FinanceBook = "Tax Book";
        Assert.Equal("Tax Book", l.FinanceBook);
    }

    // === Helpers ===

    private PurchaseInvoice CreateTestPI()
    {
        return new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-TEST", DateTime.UtcNow);
    }
}
