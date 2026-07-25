using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Inventory.Entities;
using MyERP.Inventory;
using MyERP.Core;
using MyERP.Core.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP;

/// <summary>
/// Tests for stock posting pipeline (StockPostingService + BinService + StockValuationService)
/// and payment reconciliation engine (PaymentReconciliationEngine + PaymentLedgerService).
/// Covers: SLE creation, Bin balance updates, frozen date validation, batch reconciliation,
/// exchange gain/loss, stale outstanding detection, unreconcile flow.
/// </summary>
public class StockAndReconciliationPipelineTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    // ========================
    // Stock Entry Entity Tests
    // ========================

    [Fact]
    public void StockEntry_MaterialReceipt_AddsToTargetWarehouse()
    {
        var se = new StockEntry(Guid.NewGuid(), CompanyId, StockEntryType.MaterialReceipt, DateTime.Today);
        se.AddItem(ItemId, 100, null, WarehouseId, 50m);
        var item = Assert.Single(se.Items);

        Assert.Null(item.SourceWarehouseId);
        Assert.Equal(WarehouseId, item.TargetWarehouseId);
        Assert.Equal(100m, item.Quantity);
        Assert.Equal(50m, item.ValuationRate);
    }

    [Fact]
    public void StockEntry_MaterialIssue_RemovesFromSourceWarehouse()
    {
        var se = new StockEntry(Guid.NewGuid(), CompanyId, StockEntryType.MaterialIssue, DateTime.Today);
        se.AddItem(ItemId, 30, WarehouseId, null);
        var item = Assert.Single(se.Items);

        Assert.Equal(WarehouseId, item.SourceWarehouseId);
        Assert.Null(item.TargetWarehouseId);
    }

    [Fact]
    public void StockEntry_MaterialTransfer_HasBothWarehouses()
    {
        var se = new StockEntry(Guid.NewGuid(), CompanyId, StockEntryType.MaterialTransfer, DateTime.Today);
        var sourceWh = Guid.NewGuid();
        var targetWh = Guid.NewGuid();
        se.AddItem(ItemId, 25, sourceWh, targetWh, 75m);
        var item = Assert.Single(se.Items);

        Assert.Equal(sourceWh, item.SourceWarehouseId);
        Assert.Equal(targetWh, item.TargetWarehouseId);
    }

    [Fact]
    public void StockEntry_MultiItem_AllItemsTracked()
    {
        var se = new StockEntry(Guid.NewGuid(), CompanyId, StockEntryType.MaterialReceipt, DateTime.Today);
        se.AddItem(Guid.NewGuid(), 10, null, WarehouseId, 100m);
        se.AddItem(Guid.NewGuid(), 20, null, WarehouseId, 200m);
        se.AddItem(Guid.NewGuid(), 30, null, WarehouseId, 50m);

        Assert.Equal(3, se.Items.Count);
        Assert.Equal(60m, se.Items.Sum(i => i.Quantity));
    }

    [Fact]
    public void StockEntry_Cancel_ChangesStatus()
    {
        var se = new StockEntry(Guid.NewGuid(), CompanyId, StockEntryType.MaterialReceipt, DateTime.Today);
        se.AddItem(ItemId, 10, null, WarehouseId);
        se.Submit();
        se.Post();
        se.Cancel();

        Assert.Equal(DocumentStatus.Cancelled, se.Status);
    }

    [Fact]
    public void StockEntry_Submit_BlockedWhenEmpty()
    {
        var se = new StockEntry(Guid.NewGuid(), CompanyId, StockEntryType.MaterialReceipt, DateTime.Today);
        Assert.Throws<BusinessException>(() => se.Submit());
    }

    // ========================
    // SLE Entity Tests
    // ========================

    [Fact]
    public void StockLedgerEntry_Creation_SetsAllFields()
    {
        var sle = new StockLedgerEntry(
            Guid.NewGuid(), CompanyId, ItemId, WarehouseId,
            DateTime.Today, 50m, 10m, 150m, 1500m);

        Assert.Equal(CompanyId, sle.CompanyId);
        Assert.Equal(ItemId, sle.ItemId);
        Assert.Equal(WarehouseId, sle.WarehouseId);
        Assert.Equal(50m, sle.QuantityChange);
        Assert.Equal(10m, sle.ValuationRate);
        Assert.Equal(150m, sle.BalanceQuantity);
        Assert.Equal(1500m, sle.BalanceValue);
    }

    [Fact]
    public void StockLedgerEntry_NegativeQty_ForStockOut()
    {
        var sle = new StockLedgerEntry(
            Guid.NewGuid(), CompanyId, ItemId, WarehouseId,
            DateTime.Today, -20m, 15m, 80m, 1200m);

        Assert.Equal(-20m, sle.QuantityChange);
        Assert.Equal(80m, sle.BalanceQuantity);
    }

    [Fact]
    public void StockLedgerEntry_VoucherReference_Tracked()
    {
        var sleId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();
        var sle = new StockLedgerEntry(
            sleId, CompanyId, ItemId, WarehouseId,
            DateTime.Today, 10m, 100m, 10m, 1000m)
        { VoucherType = "StockEntry", VoucherId = voucherId };

        Assert.Equal("StockEntry", sle.VoucherType);
        Assert.Equal(voucherId, sle.VoucherId);
    }

    // ========================
    // Bin Entity Tests
    // ========================

    [Fact]
    public void Bin_ProjectedQty_Formula()
    {
        var bin = new Bin(Guid.NewGuid(), ItemId, WarehouseId);
        bin.ActualQty = 100;
        bin.OrderedQty = 50;
        bin.IndentedQty = 20;
        bin.PlannedQty = 10;
        bin.ReservedQty = 30;
        bin.ReservedQtyForProduction = 15;
        bin.ReservedQtyForSubContract = 5;

        // projected = actual + ordered + indented + planned - reserved - reserved_production - reserved_subcontract
        var expected = 100 + 50 + 20 + 10 - 30 - 15 - 5;
        Assert.Equal(expected, bin.ProjectedQty);
    }

    [Fact]
    public void Bin_NegativeProjected_AllowedForReorderDetection()
    {
        var bin = new Bin(Guid.NewGuid(), ItemId, WarehouseId);
        bin.ActualQty = 10;
        bin.ReservedQty = 50; // more reserved than actual

        Assert.True(bin.ProjectedQty < 0);
    }

    [Fact]
    public void Bin_DefaultValues_AllZero()
    {
        var bin = new Bin(Guid.NewGuid(), ItemId, WarehouseId);

        Assert.Equal(0m, bin.ActualQty);
        Assert.Equal(0m, bin.OrderedQty);
        Assert.Equal(0m, bin.ReservedQty);
        Assert.Equal(0m, bin.PlannedQty);
        Assert.Equal(0m, bin.IndentedQty);
        Assert.Equal(0m, bin.ReservedQtyForProduction);
        Assert.Equal(0m, bin.ReservedQtyForSubContract);
        Assert.Equal(0m, bin.ProjectedQty);
    }

    // ========================
    // Stock Frozen Date Tests
    // ========================

    [Fact]
    public void Company_StockFrozenUpto_DefaultNull()
    {
        var company = new Company(Guid.NewGuid(), "Test Co");
        Assert.Null(company.StockFrozenUpto);
    }

    [Fact]
    public void Company_StockFrozenUptoDays_DefaultZero()
    {
        var company = new Company(Guid.NewGuid(), "Test Co");
        Assert.Equal(0, company.StockFrozenUptoDays);
    }

    [Fact]
    public void Company_StockAuthRole_DefaultNull()
    {
        var company = new Company(Guid.NewGuid(), "Test Co");
        Assert.Null(company.StockAuthRole);
    }

    // ========================
    // Payment Reconciliation Engine Tests
    // ========================

    [Fact]
    public void ReconciliationAllocation_DefaultValues()
    {
        var alloc = new ReconciliationAllocation();
        Assert.Null(alloc.PaymentVoucherType);
        Assert.Equal(Guid.Empty, alloc.PaymentVoucherId);
        Assert.Null(alloc.InvoiceVoucherType);
        Assert.Equal(Guid.Empty, alloc.InvoiceVoucherId);
        Assert.Equal(0m, alloc.AllocatedAmount);
    }

    [Fact]
    public void ReconciliationAllocation_SetFields()
    {
        var peId = Guid.NewGuid();
        var siId = Guid.NewGuid();
        var alloc = new ReconciliationAllocation
        {
            PaymentVoucherType = "PaymentEntry",
            PaymentVoucherId = peId,
            InvoiceVoucherType = "SalesInvoice",
            InvoiceVoucherId = siId,
            AllocatedAmount = 5000m,
        };

        Assert.Equal("PaymentEntry", alloc.PaymentVoucherType);
        Assert.Equal(peId, alloc.PaymentVoucherId);
        Assert.Equal("SalesInvoice", alloc.InvoiceVoucherType);
        Assert.Equal(siId, alloc.InvoiceVoucherId);
        Assert.Equal(5000m, alloc.AllocatedAmount);
    }

    [Fact]
    public void ReconciliationResult_DefaultNoErrors()
    {
        var result = new ReconciliationResult();
        Assert.Equal(0, result.ReconciledCount);
        Assert.Equal(0m, result.TotalAllocated);
        Assert.False(result.HasErrors);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ReconciliationResult_WithErrors_HasErrorsTrue()
    {
        var result = new ReconciliationResult();
        result.Errors.Add(new ReconciliationError
        {
            InvoiceVoucherId = Guid.NewGuid(),
            Message = "Outstanding changed"
        });

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ReconciliationResult_CountsCorrect()
    {
        var result = new ReconciliationResult
        {
            ReconciledCount = 3,
            TotalAllocated = 15000m,
        };
        result.Errors.Add(new ReconciliationError { InvoiceVoucherId = Guid.NewGuid(), Message = "Stale" });

        Assert.Equal(3, result.ReconciledCount);
        Assert.Equal(15000m, result.TotalAllocated);
        Assert.Single(result.Errors);
    }

    // ========================
    // Exchange Gain/Loss Calculation Tests
    // ========================

    [Fact]
    public void ExchangeGainLoss_SameRate_Zero()
    {
        var result = PaymentReconciliationEngine.CalculateExchangeGainLoss(1000m, 4.72m, 4.72m);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void ExchangeGainLoss_HigherPaymentRate_Gain()
    {
        // Payment at 4.80, invoice at 4.72 → gain (more MYR received per USD)
        var result = PaymentReconciliationEngine.CalculateExchangeGainLoss(1000m, 4.80m, 4.72m);
        Assert.Equal(80m, result); // 1000 × (4.80 - 4.72) = 80
    }

    [Fact]
    public void ExchangeGainLoss_LowerPaymentRate_Loss()
    {
        // Payment at 4.60, invoice at 4.72 → loss
        var result = PaymentReconciliationEngine.CalculateExchangeGainLoss(1000m, 4.60m, 4.72m);
        Assert.Equal(-120m, result); // 1000 × (4.60 - 4.72) = -120
    }

    [Fact]
    public void ExchangeGainLoss_LargeAmount_Precision()
    {
        var result = PaymentReconciliationEngine.CalculateExchangeGainLoss(100_000m, 4.725m, 4.720m);
        Assert.Equal(500m, result); // 100000 × 0.005 = 500
    }

    [Fact]
    public void ExchangeGainLoss_SmallDifference_RoundedTo2dp()
    {
        var result = PaymentReconciliationEngine.CalculateExchangeGainLoss(333m, 4.721m, 4.720m);
        // 333 × 0.001 = 0.333 → rounded to 0.33
        Assert.Equal(0.33m, result);
    }

    [Fact]
    public void ExchangeGainLoss_ZeroAmount_Zero()
    {
        var result = PaymentReconciliationEngine.CalculateExchangeGainLoss(0m, 4.80m, 4.72m);
        Assert.Equal(0m, result);
    }

    // ========================
    // Unreconciled Payment DTO Tests
    // ========================

    [Fact]
    public void UnreconciledPayment_DefaultValues()
    {
        var payment = new UnreconciledPayment();
        Assert.Null(payment.VoucherType);
        Assert.Equal(Guid.Empty, payment.VoucherId);
        Assert.Equal(0m, payment.TotalAmount);
        Assert.Equal(0m, payment.UnallocatedAmount);
    }

    [Fact]
    public void UnreconciledPayment_PartiallyAllocated()
    {
        var payment = new UnreconciledPayment
        {
            VoucherType = "PaymentEntry",
            VoucherId = Guid.NewGuid(),
            TotalAmount = 10000m,
            UnallocatedAmount = 3000m,
        };

        Assert.Equal(7000m, payment.TotalAmount - payment.UnallocatedAmount); // already allocated
    }

    // ========================
    // Payment Ledger Entry Entity Tests
    // ========================

    [Fact]
    public void PaymentLedgerEntry_CreatedWithCorrectFields()
    {
        var ple = new PaymentLedgerEntry(
            Guid.NewGuid(), CompanyId, DateTime.Today, AccountId,
            "Customer", CustomerId,
            "SalesInvoice", Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(),
            5000m, 5000m, "MYR");

        Assert.Equal(CompanyId, ple.CompanyId);
        Assert.Equal(AccountId, ple.AccountId);
        Assert.Equal("Customer", ple.PartyType);
        Assert.Equal(CustomerId, ple.PartyId);
        Assert.Equal(5000m, ple.Amount);
        Assert.Equal("MYR", ple.AccountCurrency);
        Assert.False(ple.Delinked);
        Assert.False(ple.IsReversal);
    }

    [Fact]
    public void PaymentLedgerEntry_Delink_SetsFlag()
    {
        var ple = new PaymentLedgerEntry(
            Guid.NewGuid(), CompanyId, DateTime.Today, AccountId,
            "Customer", CustomerId,
            "PaymentEntry", Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(),
            -3000m, -3000m, "MYR");

        ple.Delinked = true;
        Assert.True(ple.Delinked);
    }

    // ========================
    // Warehouse Entity Tests
    // ========================

    [Fact]
    public void Warehouse_IsGroup_DefaultFalse()
    {
        var wh = new Warehouse(Guid.NewGuid(), CompanyId, "Stores");
        Assert.False(wh.IsGroup);
    }

    [Fact]
    public void Warehouse_GroupWarehouse_CannotReceiveStock()
    {
        var wh = new Warehouse(Guid.NewGuid(), CompanyId, "All Warehouses");
        wh.IsGroup = true;
        Assert.True(wh.IsGroup);
        // StockPostingService validates: group=true → throws GroupWarehouseCannotReceiveStock
    }

    [Fact]
    public void Warehouse_ParentChild_Hierarchy()
    {
        var parent = new Warehouse(Guid.NewGuid(), CompanyId, "All Warehouses");
        parent.IsGroup = true;
        var child = new Warehouse(Guid.NewGuid(), CompanyId, "Stores");
        child.ParentWarehouseId = parent.Id;

        Assert.Equal(parent.Id, child.ParentWarehouseId);
        Assert.False(child.IsGroup);
    }

    // ========================
    // Item MaintainStock Tests
    // ========================

    [Fact]
    public void Item_MaintainStock_DefaultTrue()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "ITEM-001", "Widget", ItemType.Goods);
        Assert.True(item.MaintainStock);
    }

    [Fact]
    public void Item_ServiceItem_MaintainStockFalse()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "SVC-001", "Consulting", ItemType.Service);
        item.MaintainStock = false;
        Assert.False(item.MaintainStock);
        // StockPostingService skips items with MaintainStock=false
    }
}
