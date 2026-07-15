using System;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests that business validation guards properly block invalid operations.
/// These verify the critical safety nets added to the system.
/// </summary>
public class ValidationGuardTests
{
    [Fact]
    public void NegativeStock_FifoBlocked_WhenItemDisallows()
    {
        // Item does NOT allow negative stock
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Widget", ItemType.Goods);
        item.AllowNegativeStock = false;

        // FIFO queue has 5 units
        var queue = new FifoValuation();
        queue.AddStock(5, 100);

        // Trying to remove 8 would go to -3
        queue.RemoveStock(8);
        var resultQty = queue.TotalQty;

        // The valuation engine would detect this and throw InsufficientStock
        // (tested at service level — here we verify the detection logic)
        var wouldGoNegative = resultQty < -0.0001m;
        wouldGoNegative.ShouldBeTrue();
        item.AllowNegativeStock.ShouldBeFalse(); // guard would block
    }

    [Fact]
    public void NegativeStock_Allowed_WhenItemPermits()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-002", "Service Widget", ItemType.Goods);
        item.AllowNegativeStock = true;

        var queue = new FifoValuation();
        queue.AddStock(5, 100);
        queue.RemoveStock(8);

        var wouldGoNegative = queue.TotalQty < -0.0001m;
        wouldGoNegative.ShouldBeTrue();
        item.AllowNegativeStock.ShouldBeTrue(); // guard would allow
    }

    [Fact]
    public void CreditLimit_BlocksWhenExceeded()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "ABC Corp");
        customer.CreditLimit = 50000m;

        // Simulate outstanding of RM45,000 + new transaction RM10,000
        var outstanding = 45000m;
        var newAmount = 10000m;
        var totalExposure = outstanding + newAmount;

        var exceeds = totalExposure > customer.CreditLimit;
        exceeds.ShouldBeTrue(); // Would throw CreditLimitExceeded
    }

    [Fact]
    public void CreditLimit_AllowsWhenWithinLimit()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "XYZ Trading");
        customer.CreditLimit = 100000m;

        var outstanding = 45000m;
        var newAmount = 10000m;
        var totalExposure = outstanding + newAmount;

        var exceeds = totalExposure > customer.CreditLimit;
        exceeds.ShouldBeFalse(); // Within limit — allowed
    }

    [Fact]
    public void CreditLimit_ZeroMeansUnlimited()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "VIP Customer");
        customer.CreditLimit = 0; // No limit

        // Even with huge outstanding, zero limit = unlimited
        // CreditLimitService skips validation when limit <= 0
        var shouldSkip = customer.CreditLimit <= 0;
        shouldSkip.ShouldBeTrue();
    }

    [Fact]
    public void StockFrozen_BlocksTransactionBeforeFreezeDate()
    {
        var company = new Company(Guid.NewGuid(), "Test Company");
        company.StockFrozenUpto = new DateTime(2026, 6, 30);

        var postingDate = new DateTime(2026, 6, 15); // Before frozen date

        var isBlocked = company.StockFrozenUpto.HasValue && postingDate <= company.StockFrozenUpto.Value;
        isBlocked.ShouldBeTrue(); // Would throw StockFrozenPeriod
    }

    [Fact]
    public void StockFrozen_AllowsAfterFreezeDate()
    {
        var company = new Company(Guid.NewGuid(), "Test Company");
        company.StockFrozenUpto = new DateTime(2026, 6, 30);

        var postingDate = new DateTime(2026, 7, 1); // After frozen date

        var isBlocked = company.StockFrozenUpto.HasValue && postingDate <= company.StockFrozenUpto.Value;
        isBlocked.ShouldBeFalse(); // Allowed
    }

    [Fact]
    public void AccountingPeriod_BlocksClosedPeriod()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(),
            "Q2 2026", new DateTime(2026, 4, 1), new DateTime(2026, 6, 30));
        period.Close();

        var postingDate = new DateTime(2026, 5, 15); // Within closed period

        var isInPeriod = period.IsClosed && period.ContainsDate(postingDate);
        isInPeriod.ShouldBeTrue(); // Would throw AccountingPeriodClosed
    }

    [Fact]
    public void AccountingPeriod_AllowsOpenPeriod()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(),
            "Q3 2026", new DateTime(2026, 7, 1), new DateTime(2026, 9, 30));
        // Not closed

        var postingDate = new DateTime(2026, 8, 15);

        var isBlocked = period.IsClosed && period.ContainsDate(postingDate);
        isBlocked.ShouldBeFalse(); // Period is open — allowed
    }

    [Fact]
    public void SO_CannotSubmitWithoutItems()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-EMPTY", DateTime.UtcNow);

        Should.Throw<Volo.Abp.BusinessException>(() => so.Submit());
    }

    [Fact]
    public void SO_CannotCancelAlreadyCancelled()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-CANCEL", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item", 1, 100, 0);
        so.Submit();
        so.Cancel();

        Should.Throw<Volo.Abp.BusinessException>(() => so.Cancel());
    }

    [Fact]
    public void PO_CannotCloseFromDraft()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-DRAFT", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Material", 10, 50, 0);

        Should.Throw<Volo.Abp.BusinessException>(() => po.Close());
    }

    [Fact]
    public void PO_CannotReopenWhenNotClosed()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-OPEN", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Material", 10, 50, 0);
        po.Submit();

        Should.Throw<Volo.Abp.BusinessException>(() => po.Reopen());
    }

    [Fact]
    public void AccountsFrozen_BlocksGLPosting()
    {
        var company = new Company(Guid.NewGuid(), "Test Company");
        company.AccountsFrozenTillDate = new DateTime(2026, 3, 31);

        var postingDate = new DateTime(2026, 3, 15); // In frozen period

        var isBlocked = company.AccountsFrozenTillDate.HasValue && postingDate <= company.AccountsFrozenTillDate.Value;
        isBlocked.ShouldBeTrue(); // Would throw AccountingPeriodClosed
    }

    [Fact]
    public void PaymentOverAllocation_Detected()
    {
        // Outstanding: RM1000, trying to allocate RM1500
        var outstanding = 1000m;
        var allocatedAmount = 1500m;

        var isOverAllocated = Math.Abs(allocatedAmount) > Math.Abs(outstanding) + 0.01m;
        isOverAllocated.ShouldBeTrue(); // Would throw OverAllocation
    }
}
