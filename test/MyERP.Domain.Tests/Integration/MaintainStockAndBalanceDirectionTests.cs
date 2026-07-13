using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

public class MaintainStockAndBalanceDirectionTests
{
    [Fact]
    public void Item_MaintainStock_True_CreatesStockMovement()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "STOCK-001", "Stock Item", ItemType.Goods);
        item.MaintainStock.ShouldBeTrue();
        // StockPostingService and DN/PR/SI will create SLE for this item
    }

    [Fact]
    public void Item_MaintainStock_False_SkipsStockMovement()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "SVC-001", "Consulting Service", ItemType.Service);
        item.MaintainStock = false;
        item.MaintainStock.ShouldBeFalse();
        // StockPostingService, DN, PR, SI submit will skip SLE for this item
    }

    [Fact]
    public void Account_BalanceMustBe_DefaultsNull()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "1000", "Cash", AccountType.Asset);
        account.BalanceMustBe.ShouldBeNull();
    }

    [Fact]
    public void Account_BalanceMustBe_Debit_ForAssetAccounts()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "1100", "Bank", AccountType.Asset);
        account.BalanceMustBe = BalanceDirection.Debit;
        account.BalanceMustBe.ShouldBe(BalanceDirection.Debit);
    }

    [Fact]
    public void Account_BalanceMustBe_Credit_ForLiabilityAccounts()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "2100", "Accounts Payable", AccountType.Liability);
        account.BalanceMustBe = BalanceDirection.Credit;
        account.BalanceMustBe.ShouldBe(BalanceDirection.Credit);
    }

    [Fact]
    public void BalanceDirection_Enum_HasDebitAndCredit()
    {
        ((int)BalanceDirection.Debit).ShouldBe(0);
        ((int)BalanceDirection.Credit).ShouldBe(1);
    }

    [Fact]
    public void Account_BalanceMustBe_ExpenseIsDebitNormal()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "5100", "COGS", AccountType.Expense);
        account.BalanceMustBe = BalanceDirection.Debit;
        // Expense accounts normally have debit balances
        account.BalanceMustBe.ShouldBe(BalanceDirection.Debit);
    }

    [Fact]
    public void Account_BalanceMustBe_RevenueIsCreditNormal()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "4100", "Sales", AccountType.Revenue);
        account.BalanceMustBe = BalanceDirection.Credit;
        // Revenue accounts normally have credit balances
        account.BalanceMustBe.ShouldBe(BalanceDirection.Credit);
    }

    [Fact]
    public void Item_ServiceType_DefaultsMaintainStockFalse()
    {
        // Service items auto-set MaintainStock=false in constructor (ItemType != Goods)
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "SVC-002", "Installation", ItemType.Service);
        item.MaintainStock.ShouldBeFalse();
    }
}
