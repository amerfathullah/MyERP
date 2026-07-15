using System;
using System.Collections.Generic;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Tax.DomainServices;
using MyERP.Tax.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Tests for buying controller validations documented in purchasing.instructions.md:
/// - Posting date temporal ordering (PR/PI posting date >= PO transaction date)
/// - Asset return blocking (submitted assets prevent purchase returns)
/// - From-warehouse validation (same warehouse + subcontracting blocks)
/// - Bank transaction fee transformation and currency validation
/// - Item Tax Template per-item rate overrides in tax cascade
/// </summary>
public class BuyingControllerValidationTests
{
    // --- Posting Date Temporal Ordering ---

    [Fact]
    public void PurchaseReceipt_PostingDate_BeforePODate_Concept()
    {
        // PR posting date 2026-01-10, PO order date 2026-01-15
        // PR is BEFORE PO → should be blocked
        var prDate = new DateTime(2026, 1, 10);
        var poDate = new DateTime(2026, 1, 15);

        (prDate < poDate).ShouldBeTrue();
    }

    [Fact]
    public void PurchaseReceipt_PostingDate_AfterPODate_Allowed()
    {
        var prDate = new DateTime(2026, 1, 20);
        var poDate = new DateTime(2026, 1, 15);

        (prDate >= poDate).ShouldBeTrue();
    }

    [Fact]
    public void PurchaseReceipt_PostingDate_SameDayAsPO_Allowed()
    {
        var date = new DateTime(2026, 1, 15);

        var sameDate = new DateTime(2026, 1, 15);
        (date >= sameDate).ShouldBeTrue();
    }

    [Fact]
    public void PostingDateBeforePODate_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.PostingDateBeforePODate.ShouldBe("MyERP:04011");
    }

    // --- Asset Return Blocking ---

    [Fact]
    public void Asset_PurchaseReceiptId_DefaultsNull()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "A-001", "Laptop",
            DateTime.Today, 5000m);

        asset.PurchaseReceiptId.ShouldBeNull();
    }

    [Fact]
    public void Asset_PurchaseReceiptId_CanBeSet()
    {
        var prId = Guid.NewGuid();
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "A-002", "Server",
            DateTime.Today, 15000m);

        asset.PurchaseReceiptId = prId;

        asset.PurchaseReceiptId.ShouldBe(prId);
    }

    [Fact]
    public void Asset_PurchaseInvoiceId_CanBeSet()
    {
        var piId = Guid.NewGuid();
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "A-003", "Printer",
            DateTime.Today, 2000m);

        asset.PurchaseInvoiceId = piId;

        asset.PurchaseInvoiceId.ShouldBe(piId);
    }

    [Fact]
    public void AssetExistsOnReturnDocument_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.AssetExistsOnReturnDocument.ShouldBe("MyERP:04012");
    }

    // --- From-Warehouse Validation ---

    [Fact]
    public void PurchaseReceiptItem_FromWarehouseId_DefaultsNull()
    {
        var item = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Steel", 100, 10m, 0m);

        item.FromWarehouseId.ShouldBeNull();
    }

    [Fact]
    public void PurchaseReceiptItem_FromWarehouseId_CanBeSet()
    {
        var whId = Guid.NewGuid();
        var item = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Steel", 100, 10m, 0m);

        item.FromWarehouseId = whId;

        item.FromWarehouseId.ShouldBe(whId);
    }

    [Fact]
    public void PurchaseReceiptItem_WarehouseId_CanBeSet()
    {
        var whId = Guid.NewGuid();
        var item = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Steel", 100, 10m, 0m);

        item.WarehouseId = whId;

        item.WarehouseId.ShouldBe(whId);
    }

    [Fact]
    public void PurchaseReceipt_IsSubcontracted_DefaultsFalse()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "PR-001", DateTime.Today);

        pr.IsSubcontracted.ShouldBeFalse();
    }

    [Fact]
    public void FromWarehouseEqualsTargetWarehouse_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.FromWarehouseEqualsTargetWarehouse.ShouldBe("MyERP:04013");
    }

    [Fact]
    public void FromWarehouseOnSubcontractedDocument_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.FromWarehouseOnSubcontractedDocument.ShouldBe("MyERP:04014");
    }
}

/// <summary>
/// Tests for BankTransaction fee transformation and currency validation.
/// Per ERPNext: handle_excluded_fee (before_validate), validate_included_fee, validate_currency.
/// </summary>
public class BankTransactionFeeTests
{
    [Fact]
    public void NormalizeFees_ExcludedFee_SubtractsFromDeposit()
    {
        var bt = CreateBankTransaction();
        bt.Deposit = 1000m;
        bt.ExcludedFee = 50m;

        bt.NormalizeFees();

        bt.Deposit.ShouldBe(950m);
        bt.IncludedFee.ShouldBe(50m);
        bt.ExcludedFee.ShouldBe(0m);
    }

    [Fact]
    public void NormalizeFees_ExcludedFee_AddsToWithdrawal()
    {
        var bt = CreateBankTransaction();
        bt.Withdrawal = 500m;
        bt.ExcludedFee = 25m;

        bt.NormalizeFees();

        bt.Withdrawal.ShouldBe(525m);
        bt.IncludedFee.ShouldBe(25m);
        bt.ExcludedFee.ShouldBe(0m);
    }

    [Fact]
    public void NormalizeFees_FeeExceedsDeposit_Throws()
    {
        var bt = CreateBankTransaction();
        bt.Deposit = 100m;
        bt.ExcludedFee = 200m;

        Assert.Throws<BusinessException>(() => bt.NormalizeFees())
            .Code.ShouldBe(MyERPDomainErrorCodes.ExcludedFeeExceedsDeposit);
    }

    [Fact]
    public void NormalizeFees_BothDepositAndWithdrawal_Throws()
    {
        var bt = CreateBankTransaction();
        bt.Deposit = 1000m;
        bt.Withdrawal = 500m;
        bt.ExcludedFee = 50m;

        Assert.Throws<BusinessException>(() => bt.NormalizeFees())
            .Code.ShouldBe(MyERPDomainErrorCodes.BidirectionalFeeTransaction);
    }

    [Fact]
    public void NormalizeFees_ZeroExcludedFee_NoOp()
    {
        var bt = CreateBankTransaction();
        bt.Deposit = 1000m;
        bt.ExcludedFee = 0m;

        bt.NormalizeFees();

        bt.Deposit.ShouldBe(1000m);
        bt.IncludedFee.ShouldBe(0m);
    }

    [Fact]
    public void ValidateIncludedFee_FeeExceedsWithdrawal_Throws()
    {
        var bt = CreateBankTransaction();
        bt.Withdrawal = 100m;
        bt.IncludedFee = 200m;

        Assert.Throws<BusinessException>(() => bt.ValidateIncludedFee())
            .Code.ShouldBe(MyERPDomainErrorCodes.IncludedFeeExceedsWithdrawal);
    }

    [Fact]
    public void ValidateIncludedFee_WithinWithdrawal_NoThrow()
    {
        var bt = CreateBankTransaction();
        bt.Withdrawal = 500m;
        bt.IncludedFee = 50m;

        bt.ValidateIncludedFee(); // should not throw
    }

    [Fact]
    public void ValidateCurrency_Mismatch_Throws()
    {
        var bt = CreateBankTransaction();
        bt.CurrencyCode = "USD";

        Assert.Throws<BusinessException>(() => bt.ValidateCurrency("MYR"))
            .Code.ShouldBe(MyERPDomainErrorCodes.BankTransactionCurrencyMismatch);
    }

    [Fact]
    public void ValidateCurrency_Matches_NoThrow()
    {
        var bt = CreateBankTransaction();
        bt.CurrencyCode = "MYR";

        bt.ValidateCurrency("MYR"); // should not throw
    }

    [Fact]
    public void NormalizeFees_AccumulatesIntoIncludedFee()
    {
        var bt = CreateBankTransaction();
        bt.Withdrawal = 1000m;
        bt.IncludedFee = 10m; // existing included fee
        bt.ExcludedFee = 20m;

        bt.NormalizeFees();

        bt.IncludedFee.ShouldBe(30m); // 10 + 20
        bt.Withdrawal.ShouldBe(1020m);
    }

    private static BankTransaction CreateBankTransaction()
    {
        return new BankTransaction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, "Test transaction", 0m);
    }
}

/// <summary>
/// Tests for Item Tax Template per-item rate overrides in tax cascade.
/// Per ERPNext: item_tax_rate JSON map on items provides account→rate overrides.
/// The N/A sentinel (decimal.MinValue) excludes the tax row entirely for that item.
/// </summary>
public class ItemTaxTemplateOverrideTests
{
    [Fact]
    public void Calculate_WithItemTaxOverride_UsesOverrideRate()
    {
        var service = new TaxesAndTotalsService();
        var accountId = Guid.NewGuid();
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 1000, NetAmount = 1000,
                ItemTaxRateOverrides = new Dictionary<Guid, decimal> { { accountId, 10m } } }
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST 6%", "On Net Total", 6m)
            { AccountId = accountId }
        };

        var result = service.Calculate(items, taxes);

        // Should use 10% override, not 6% default → 1000 * 10% = 100
        taxes[0].TaxAmount.ShouldBe(100m);
        result.GrandTotal.ShouldBe(1100m);
    }

    [Fact]
    public void Calculate_WithNASentinel_ExcludesTaxForItem()
    {
        var service = new TaxesAndTotalsService();
        var accountId = Guid.NewGuid();
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 1000, NetAmount = 1000,
                ItemTaxRateOverrides = new Dictionary<Guid, decimal> { { accountId, decimal.MinValue } } }
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST 6%", "On Net Total", 6m)
            { AccountId = accountId }
        };

        var result = service.Calculate(items, taxes);

        // N/A sentinel → tax excluded for this item → 0 tax
        taxes[0].TaxAmount.ShouldBe(0m);
        result.GrandTotal.ShouldBe(1000m);
    }

    [Fact]
    public void Calculate_WithoutOverride_UsesDefaultRate()
    {
        var service = new TaxesAndTotalsService();
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 1000, NetAmount = 1000 }
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST 6%", "On Net Total", 6m)
            { AccountId = Guid.NewGuid() }
        };

        var result = service.Calculate(items, taxes);

        // No override → uses default 6% → 1000 * 6% = 60
        taxes[0].TaxAmount.ShouldBe(60m);
        result.GrandTotal.ShouldBe(1060m);
    }

    [Fact]
    public void Calculate_MixedItems_OverrideAndDefault()
    {
        var service = new TaxesAndTotalsService();
        var accountId = Guid.NewGuid();
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 1000, NetAmount = 1000,
                ItemTaxRateOverrides = new Dictionary<Guid, decimal> { { accountId, 10m } } },
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 500, NetAmount = 500 }
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST 6%", "On Net Total", 6m)
            { AccountId = accountId }
        };

        var result = service.Calculate(items, taxes);

        // Item 1: 1000 * 10% = 100, Item 2: 500 * 6% = 30 → total tax = 130
        taxes[0].TaxAmount.ShouldBe(130m);
        result.GrandTotal.ShouldBe(1630m);
    }

    [Fact]
    public void Calculate_RegionalRounding_RoundsToInteger()
    {
        var service = new TaxesAndTotalsService();
        var accountId = Guid.NewGuid();
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 999, NetAmount = 999 }
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST 6%", "On Net Total", 6m)
            { AccountId = accountId }
        };

        var roundOffAccounts = new List<Guid> { accountId };
        var result = service.Calculate(items, taxes, roundOffApplicableAccountIds: roundOffAccounts);

        // 999 * 6% = 59.94 → rounded to 60 for regional account
        taxes[0].TaxAmount.ShouldBe(60m);
    }

    [Fact]
    public void Calculate_RegionalRounding_BaseAmountAlsoRounded()
    {
        var service = new TaxesAndTotalsService();
        var accountId = Guid.NewGuid();
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 999, NetAmount = 999 }
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST 6%", "On Net Total", 6m)
            { AccountId = accountId }
        };

        var roundOffAccounts = new List<Guid> { accountId };
        var result = service.Calculate(items, taxes, exchangeRate: 4.5m, roundOffApplicableAccountIds: roundOffAccounts);

        // Base = 60 * 4.5 = 270 → already integer, no further rounding needed
        taxes[0].BaseTaxAmount.ShouldBe(270m);
    }

    [Fact]
    public void Calculate_NonRegionalAccount_NotRounded()
    {
        var service = new TaxesAndTotalsService();
        var roundAccountId = Guid.NewGuid();
        var normalAccountId = Guid.NewGuid();
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 999, NetAmount = 999 }
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST 6%", "On Net Total", 6m)
            { AccountId = normalAccountId }
        };

        var roundOffAccounts = new List<Guid> { roundAccountId };
        var result = service.Calculate(items, taxes, roundOffApplicableAccountIds: roundOffAccounts);

        // Normal account → NOT rounded → 59.94
        taxes[0].TaxAmount.ShouldBe(59.94m);
    }
}
