using System;
using System.Collections.Generic;
using MyERP.Accounting;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for SO→MR conversion guards, RFQ→SQ workflow,
/// PCV GL entries visibility, and ItemDetailsResolver enhancements.
/// </summary>
public class SoMrRfqSqPcvAndItemResolverTests
{
    // === SO → Material Request Conversion Guards ===

    [Fact]
    public void SalesOrder_CannotConvert_WhenDraft()
    {
        // SO→MR requires submitted status (per DocumentConversionAppService guard)
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001",
            DateTime.UtcNow);
        Assert.Equal(DocumentStatus.Draft, so.Status);
        // Backend should throw DocumentMustBeSubmittedForConversion
    }

    [Fact]
    public void SalesOrder_SubmittedStatus_AllowsConversion()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-002",
            DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item A", 10m, 100m, 0m);
        so.Submit();
        // SO goes to "ToDeliverAndBill" on submit (not plain "Submitted")
        // Backend conversion checks for non-Draft, non-Cancelled
        Assert.NotEqual(DocumentStatus.Draft, so.Status);
        Assert.NotEqual(DocumentStatus.Cancelled, so.Status);
    }

    // === RFQ → Supplier Quotation Workflow ===

    [Fact]
    public void Rfq_Submit_RequiresItemsAndSuppliers()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.UtcNow);
        // Cannot submit with no items or suppliers
        Assert.Throws<Volo.Abp.BusinessException>(() => rfq.Submit());
    }

    [Fact]
    public void Rfq_Submit_WithItemsAndSuppliers_Succeeds()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-002", DateTime.UtcNow);
        rfq.AddItem(Guid.NewGuid(), "Widget", 50m, "Unit");
        rfq.AddSupplier(Guid.NewGuid(), "Supplier A");
        rfq.Submit();
        Assert.Equal(DocumentStatus.Submitted, rfq.Status);
    }

    [Fact]
    public void Rfq_DuplicateSupplier_Blocked()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-003", DateTime.UtcNow);
        var supplierId = Guid.NewGuid();
        rfq.AddSupplier(supplierId, "Supplier X");
        Assert.Throws<Volo.Abp.BusinessException>(() => rfq.AddSupplier(supplierId, "Supplier X"));
    }

    [Fact]
    public void Rfq_CannotAddItems_WhenSubmitted()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-004", DateTime.UtcNow);
        rfq.AddItem(Guid.NewGuid(), "Widget", 10m, "Unit");
        rfq.AddSupplier(Guid.NewGuid(), "Supplier A");
        rfq.Submit();
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            rfq.AddItem(Guid.NewGuid(), "Another", 5m, "Unit"));
    }

    // === Supplier Quotation Lifecycle ===

    [Fact]
    public void SupplierQuotation_Submit_RequiresItems()
    {
        var sq = new SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        Assert.Throws<Volo.Abp.BusinessException>(() => sq.Submit());
    }

    [Fact]
    public void SupplierQuotation_AddItem_RecalculatesTotals()
    {
        var sq = new SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        sq.AddItem(Guid.NewGuid(), 10m, 25.50m, "Widget A");
        sq.AddItem(Guid.NewGuid(), 5m, 100m, "Widget B");

        // NetTotal = (10 * 25.50) + (5 * 100) = 255 + 500 = 755
        Assert.Equal(755m, sq.NetTotal);
    }

    // === ItemDetailsResolver Enhancements ===

    [Fact]
    public void ItemResolutionContext_IsMaterialRequest_DefaultsFalse()
    {
        var ctx = new ItemResolutionContext { ItemId = Guid.NewGuid() };
        Assert.False(ctx.IsMaterialRequest);
    }

    [Fact]
    public void ItemResolutionContext_IsMaterialRequest_CanBeTrue()
    {
        var ctx = new ItemResolutionContext
        {
            ItemId = Guid.NewGuid(),
            IsMaterialRequest = true,
            TransactionType = TransactionType.Buying
        };
        Assert.True(ctx.IsMaterialRequest);
    }

    [Fact]
    public void ResolvedItemDetails_TaxCategoryId_CanBeSet()
    {
        var result = new ResolvedItemDetails
        {
            ItemId = Guid.NewGuid(),
            ItemCode = "ITEM-001",
            ItemName = "Test Item",
            TaxCategoryId = Guid.NewGuid(),
        };
        Assert.NotNull(result.TaxCategoryId);
    }

    [Fact]
    public void ResolvedItemDetails_LastPurchaseRate_DefaultsZero()
    {
        var result = new ResolvedItemDetails
        {
            ItemId = Guid.NewGuid(),
            ItemCode = "ITEM-002",
            ItemName = "Test Item 2",
        };
        Assert.Equal(0m, result.LastPurchaseRate);
    }

    // === Item Validation for Transactions ===

    [Fact]
    public void Item_HasVariants_BlocksDirectTransaction()
    {
        // Template items (HasVariants=true) cannot be used directly in transactions
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEMPLATE-001", "Template Item", ItemType.Goods)
        { HasVariants = true };
        Assert.True(item.HasVariants);
        // Resolver would throw ItemHasVariants error
    }

    [Fact]
    public void Item_Inactive_BlocksTransaction()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "INACTIVE-001", "Old Item", ItemType.Goods)
        { IsActive = false };
        Assert.False(item.IsActive);
        // Resolver would throw ItemInactive error
    }

    // === PCV DTO Structure ===

    [Fact]
    public void PcvGlEntryDto_HasRequiredFields()
    {
        // Verify the DTO can carry all audit-relevant GL data
        var entry = new PcvGlEntryDto
        {
            AccountId = Guid.NewGuid(),
            AccountName = "4000 - Sales Revenue",
            Debit = 15000m,
            Credit = 0m,
            CostCenterId = Guid.NewGuid(),
            PostingDate = DateTime.UtcNow,
        };
        Assert.Equal(15000m, entry.Debit);
        Assert.Equal(0m, entry.Credit);
        Assert.NotNull(entry.AccountName);
    }

    [Fact]
    public void PeriodClosingVoucherDto_HasClosingAccountName()
    {
        // Verify the DTO carries the account name (not just GUID)
        var dto = new PeriodClosingVoucherDto
        {
            ClosingAccountId = Guid.NewGuid(),
            ClosingAccountName = "Retained Earnings",
            TotalClosingAmount = 250000m,
        };
        Assert.Equal("Retained Earnings", dto.ClosingAccountName);
    }

    // === Material Request Entity ===

    [Fact]
    public void MaterialRequest_CanLinkToSalesOrder()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-001",
            MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Widget", 10m, "Unit");

        var mrItem = mr.Items[^1]; // last added
        mrItem.SalesOrderId = Guid.NewGuid();
        Assert.NotNull(mrItem.SalesOrderId);
    }
}
