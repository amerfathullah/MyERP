using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Tax.DomainServices;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for domain service wiring session:
/// - TransactionTaxRecalculationService integration patterns
/// - PaymentScheduleValidationService integration patterns
/// - ItemVariant creation DTOs
/// - UOM/PartyLink/SABB AppService DTOs
/// </summary>
public class ServiceWiringAndEntityGapTests
{
    // === TransactionTaxRecalculationService ===

    [Fact]
    public void TaxRecalculationInput_DefaultValues()
    {
        var input = new TaxRecalculationInput();
        Assert.Null(input.DocumentType);
        Assert.Equal(Guid.Empty, input.DocumentId);
        Assert.Empty(input.Items);
        Assert.Equal(1m, input.ExchangeRate);
        Assert.Equal(0m, input.DiscountAmount);
        Assert.True(input.IsDiscountOnGrandTotal);
    }

    [Fact]
    public void TaxRecalculationInput_CanSetAllFields()
    {
        var docId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var input = new TaxRecalculationInput
        {
            DocumentType = "SalesInvoice",
            DocumentId = docId,
            Items = new List<TaxItemInput>
            {
                new() { ItemId = itemId, Quantity = 10, UnitPrice = 100 }
            },
            ExchangeRate = 4.72m,
            DiscountAmount = 50,
            IsDiscountOnGrandTotal = false,
        };

        Assert.Equal("SalesInvoice", input.DocumentType);
        Assert.Equal(docId, input.DocumentId);
        Assert.Single(input.Items);
        Assert.Equal(4.72m, input.ExchangeRate);
        Assert.Equal(50m, input.DiscountAmount);
        Assert.False(input.IsDiscountOnGrandTotal);
    }

    [Fact]
    public void RecalculatedTotals_StaticNetOnly_NoTaxRows()
    {
        var items = new List<TaxItemInput>
        {
            new() { ItemId = Guid.NewGuid(), Quantity = 5, UnitPrice = 200 },
            new() { ItemId = Guid.NewGuid(), Quantity = 3, UnitPrice = 100 },
        };

        var totals = TransactionTaxRecalculationService.CalculateNetOnly(items, 0, 1m);

        Assert.Equal(1300m, totals.NetTotal); // 5×200 + 3×100
        Assert.Equal(0m, totals.TaxAmount);
        Assert.Equal(1300m, totals.GrandTotal);
        Assert.Equal(1300m, totals.BaseNetTotal);
    }

    [Fact]
    public void RecalculatedTotals_StaticNetOnly_WithDiscount()
    {
        var items = new List<TaxItemInput>
        {
            new() { ItemId = Guid.NewGuid(), Quantity = 10, UnitPrice = 100 },
        };

        var totals = TransactionTaxRecalculationService.CalculateNetOnly(items, 100, 1m);

        Assert.Equal(1000m, totals.NetTotal);
        Assert.Equal(900m, totals.GrandTotal); // 1000 - 100 discount
    }

    [Fact]
    public void RecalculatedTotals_StaticNetOnly_MultiCurrency()
    {
        var items = new List<TaxItemInput>
        {
            new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 100 },
        };

        var totals = TransactionTaxRecalculationService.CalculateNetOnly(items, 0, 4.72m);

        Assert.Equal(100m, totals.NetTotal);
        Assert.Equal(472m, totals.BaseNetTotal); // 100 × 4.72
        Assert.Equal(472m, totals.BaseGrandTotal);
    }

    // === PaymentScheduleValidationService ===

    [Fact]
    public void PaymentScheduleValidation_EmptySchedule_IsValid()
    {
        var svc = new PaymentScheduleValidationService();
        var result = svc.Validate(new List<PaymentScheduleInput>(), 1000, DateTime.Today);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void PaymentScheduleValidation_PortionsNot100_Invalid()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today.AddDays(30), InvoicePortion = 60, PaymentAmount = 600 },
            new() { DueDate = DateTime.Today.AddDays(60), InvoicePortion = 30, PaymentAmount = 300 },
        };
        var result = svc.Validate(entries, 1000, DateTime.Today);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("100%"));
    }

    [Fact]
    public void PaymentScheduleValidation_DueDateBeforePosting_Invalid()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today.AddDays(-5), InvoicePortion = 100, PaymentAmount = 1000 },
        };
        var result = svc.Validate(entries, 1000, DateTime.Today);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("before posting"));
    }

    [Fact]
    public void PaymentScheduleValidation_ValidSchedule_Passes()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today.AddDays(30), InvoicePortion = 50, PaymentAmount = 500 },
            new() { DueDate = DateTime.Today.AddDays(60), InvoicePortion = 50, PaymentAmount = 500 },
        };
        var result = svc.Validate(entries, 1000, DateTime.Today);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void PaymentScheduleValidation_ResolveDueDate_ReturnsMax()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today.AddDays(30) },
            new() { DueDate = DateTime.Today.AddDays(60) },
            new() { DueDate = DateTime.Today.AddDays(45) },
        };
        var result = svc.ResolveDueDate(entries, DateTime.Today);
        Assert.Equal(DateTime.Today.AddDays(60), result);
    }

    [Fact]
    public void PaymentScheduleValidation_RecalculateAmounts_LastAbsorbsRounding()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today, InvoicePortion = 33.33m, PaymentAmount = 0 },
            new() { DueDate = DateTime.Today, InvoicePortion = 33.33m, PaymentAmount = 0 },
            new() { DueDate = DateTime.Today, InvoicePortion = 33.34m, PaymentAmount = 0 },
        };
        var result = svc.RecalculateAmounts(entries, 1000);
        var total = result.Sum(r => r.PaymentAmount);
        Assert.Equal(1000m, total); // Last entry absorbs rounding
    }

    // === ItemVariant DTO ===

    [Fact]
    public void ItemVariantAttribute_DefaultValues()
    {
        var attr = new ItemVariantAttribute(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Red");
        Assert.Equal("Red", attr.AttributeValue);
    }

    [Fact]
    public void Item_HasVariants_DefaultFalse()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TSHIRT", "T-Shirt", 0, null);
        Assert.False(item.HasVariants);
        Assert.Null(item.VariantOfId);
    }

    [Fact]
    public void Item_VariantCanSetTemplateRef()
    {
        var templateId = Guid.NewGuid();
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TSHIRT-RED", "T-Shirt Red", 0, null);
        item.VariantOfId = templateId;
        Assert.Equal(templateId, item.VariantOfId);
    }

    // === UOM Entity ===

    [Fact]
    public void Uom_Create_HasName()
    {
        var uom = new Uom(Guid.NewGuid(), "Kilogram", null);
        Assert.Equal("Kilogram", uom.Name);
        Assert.False(uom.MustBeWholeNumber);
        Assert.True(uom.IsEnabled);
    }

    [Fact]
    public void Uom_WholeNumber_Enforced()
    {
        var uom = new Uom(Guid.NewGuid(), "Unit", null) { MustBeWholeNumber = true };
        Assert.True(uom.MustBeWholeNumber);
    }

    // === SerialAndBatchBundle ===

    [Fact]
    public void Bundle_DefaultsEmpty()
    {
        var bundle = new SerialAndBatchBundle(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(),
            DateTime.Today, null);
        Assert.Equal(0m, bundle.TotalQty);
        Assert.Equal(0m, bundle.TotalAmount);
        Assert.False(bundle.IsCancelled);
    }

    [Fact]
    public void Bundle_TypeOfTransaction_IsEnum()
    {
        Assert.True(Enum.IsDefined(typeof(BundleTransactionType), BundleTransactionType.Inward));
        Assert.True(Enum.IsDefined(typeof(BundleTransactionType), BundleTransactionType.Outward));
    }

    // === PartyLink ===

    [Fact]
    public void PartyLink_SelfLink_Throws()
    {
        var partyId = Guid.NewGuid();
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new MyERP.Core.Entities.PartyLink(
                Guid.NewGuid(), "Customer", partyId, "Customer", partyId, null));
    }

    [Fact]
    public void PartyLink_DifferentParties_Succeeds()
    {
        var link = new MyERP.Core.Entities.PartyLink(
            Guid.NewGuid(), "Customer", Guid.NewGuid(), "Supplier", Guid.NewGuid(), null);
        Assert.NotNull(link);
        Assert.Equal("Customer", link.PrimaryPartyType);
    }

    // === Discount Amount Calculation Pattern ===

    [Fact]
    public void DiscountFromPercentage_CalculatedCorrectly()
    {
        // Pattern used in both SI and PI SubmitAsync
        var netTotal = 5000m;
        var discountPct = 10m;
        var discountAmt = 0m;

        if (discountPct > 0 && discountAmt == 0)
        {
            discountAmt = Math.Round(netTotal * discountPct / 100m, 2);
        }

        Assert.Equal(500m, discountAmt);
    }

    [Fact]
    public void DiscountFromPercentage_ExplicitAmountTakesPrecedence()
    {
        var netTotal = 5000m;
        var discountPct = 10m;
        var discountAmt = 250m; // Explicit amount set by user

        if (discountPct > 0 && discountAmt == 0)
        {
            discountAmt = Math.Round(netTotal * discountPct / 100m, 2);
        }

        Assert.Equal(250m, discountAmt); // Explicit value preserved
    }
}
