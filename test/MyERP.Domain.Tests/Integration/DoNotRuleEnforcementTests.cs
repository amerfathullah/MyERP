using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Integration;

public class DoNotRuleEnforcementTests
{
    // --- Valuation Method Change Guard ---

    [Fact]
    public void Item_SetValuationMethod_NoSLE_AllowsAnyChange()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Widget", ItemType.Goods);
        item.SetValuationMethod(ValuationMethod.WeightedAverage, hasStockLedgerEntries: false);
        item.ValuationMethod.ShouldBe(ValuationMethod.WeightedAverage);
    }

    [Fact]
    public void Item_SetValuationMethod_FIFO_To_MA_WithSLE_Allowed()
    {
        // Exception: FIFO → MA is the ONLY permitted change when SLE exists
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-002", "Widget", ItemType.Goods);
        item.ValuationMethod = ValuationMethod.FIFO;
        item.SetValuationMethod(ValuationMethod.WeightedAverage, hasStockLedgerEntries: true);
        item.ValuationMethod.ShouldBe(ValuationMethod.WeightedAverage);
    }

    [Fact]
    public void Item_SetValuationMethod_MA_To_FIFO_WithSLE_Blocked()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-003", "Widget", ItemType.Goods);
        item.ValuationMethod = ValuationMethod.WeightedAverage;

        var ex = Should.Throw<BusinessException>(() =>
            item.SetValuationMethod(ValuationMethod.FIFO, hasStockLedgerEntries: true));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValuationMethodChangeLocked);
    }

    [Fact]
    public void Item_SetValuationMethod_ToStandardCost_WithSLE_Blocked()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-004", "Widget", ItemType.Goods);
        item.ValuationMethod = ValuationMethod.FIFO;

        Should.Throw<BusinessException>(() =>
            item.SetValuationMethod(ValuationMethod.StandardCost, hasStockLedgerEntries: true))
            .Code.ShouldBe(MyERPDomainErrorCodes.ValuationMethodChangeLocked);
    }

    [Fact]
    public void Item_SetValuationMethod_FromStandardCost_WithSLE_Blocked()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-005", "Widget", ItemType.Goods);
        item.ValuationMethod = ValuationMethod.StandardCost;

        Should.Throw<BusinessException>(() =>
            item.SetValuationMethod(ValuationMethod.FIFO, hasStockLedgerEntries: true))
            .Code.ShouldBe(MyERPDomainErrorCodes.ValuationMethodChangeLocked);
    }

    [Fact]
    public void Item_SetValuationMethod_SameValue_NeverThrows()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-006", "Widget", ItemType.Goods);
        item.ValuationMethod = ValuationMethod.StandardCost;
        item.SetValuationMethod(ValuationMethod.StandardCost, hasStockLedgerEntries: true);
        // No exception — same value is a no-op
    }

    // --- JE Party Account Type Validation ---

    [Fact]
    public void JE_AddLineWithParty_ReceivableAccount_Allowed()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        je.AddLineWithParty(Guid.NewGuid(), 1000, true,
            Guid.NewGuid(), "Customer", AccountSubType.AccountsReceivable, "AR line");
        je.Lines.Count.ShouldBe(1);
    }

    [Fact]
    public void JE_AddLineWithParty_PayableAccount_Allowed()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        je.AddLineWithParty(Guid.NewGuid(), 500, false,
            Guid.NewGuid(), "Supplier", AccountSubType.AccountsPayable, "AP line");
        je.Lines.Count.ShouldBe(1);
    }

    [Fact]
    public void JE_AddLineWithParty_ExpenseAccount_Blocked()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        var ex = Should.Throw<BusinessException>(() =>
            je.AddLineWithParty(Guid.NewGuid(), 1000, true,
                Guid.NewGuid(), "Customer", AccountSubType.OperatingExpense, "Invalid"));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.PartyNotAllowedOnAccount);
    }

    [Fact]
    public void JE_AddLineWithParty_BankAccount_Blocked()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        Should.Throw<BusinessException>(() =>
            je.AddLineWithParty(Guid.NewGuid(), 500, true,
                Guid.NewGuid(), "Customer", AccountSubType.BankAccount, "Invalid"))
            .Code.ShouldBe(MyERPDomainErrorCodes.PartyNotAllowedOnAccount);
    }

    [Fact]
    public void JE_AddLineWithParty_NullSubType_Allowed()
    {
        // When account sub-type is unknown (null), allow party (backward compatibility)
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        je.AddLineWithParty(Guid.NewGuid(), 1000, true,
            Guid.NewGuid(), "Customer", null, "Legacy line");
        je.Lines.Count.ShouldBe(1);
    }

    // --- Error Codes ---

    [Fact]
    public void ValuationMethodChangeLocked_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.ValuationMethodChangeLocked.ShouldBe("MyERP:05015");
    }

    [Fact]
    public void PartyNotAllowedOnAccount_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.PartyNotAllowedOnAccount.ShouldBe("MyERP:02012");
    }
}
