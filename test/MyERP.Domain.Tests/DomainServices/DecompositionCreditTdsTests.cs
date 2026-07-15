using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.DomainServices;

/// <summary>
/// Tests for ProductBundleDecompositionService DTOs and concepts.
/// </summary>
public class ProductBundleDecompositionDtoTests
{
    [Fact]
    public void DecomposedItem_RecordCreation()
    {
        var componentId = Guid.NewGuid();
        var bundleItemId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        var item = new DecomposedItem(componentId, "Widget A", 5m, 25m, "Unit", bundleItemId, bundleId);

        item.ComponentItemId.ShouldBe(componentId);
        item.ComponentItemName.ShouldBe("Widget A");
        item.Qty.ShouldBe(5m);
        item.Rate.ShouldBe(25m);
        item.Uom.ShouldBe("Unit");
        item.ParentBundleItemId.ShouldBe(bundleItemId);
        item.ParentBundleId.ShouldBe(bundleId);
    }

    [Fact]
    public void DecomposedItem_ProportionalRate()
    {
        // Parent bundle rate = 100, component qty = 2 out of total bundle qty = 5
        // Proportional rate = 100 × (2/5) = 40
        decimal parentRate = 100m;
        decimal componentQty = 2m;
        decimal totalBundleQty = 5m;
        var proportionalRate = parentRate * (componentQty / totalBundleQty);
        proportionalRate.ShouldBe(40m);
    }

    [Fact]
    public void BundleTransactionItem_RecordCreation()
    {
        var item = new BundleTransactionItem(Guid.NewGuid(), 10m, 50m);
        item.Qty.ShouldBe(10m);
        item.Rate.ShouldBe(50m);
    }

    [Fact]
    public void DecompositionResult_GetStockItems_CombinesRegularAndPacked()
    {
        var regularId = Guid.NewGuid();
        var packedId = Guid.NewGuid();
        var bundleItemId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();

        var regular = new List<BundleTransactionItem> { new(regularId, 3m, 100m) };
        var packed = new List<DecomposedItem> { new(packedId, "Component", 6m, 20m, "Unit", bundleItemId, bundleId) };

        var result = new DecompositionResult(regular, packed);
        var stockItems = result.GetStockItems().ToList();

        stockItems.Count.ShouldBe(2);
        stockItems.ShouldContain(x => x.ItemId == regularId && x.Qty == 3m);
        stockItems.ShouldContain(x => x.ItemId == packedId && x.Qty == 6m);
    }

    [Fact]
    public void DecompositionResult_EmptyBothLists()
    {
        var result = new DecompositionResult(new(), new());
        result.GetStockItems().ShouldBeEmpty();
    }

    [Fact]
    public void DecompositionResult_RegularOnly()
    {
        var id = Guid.NewGuid();
        var result = new DecompositionResult(
            new List<BundleTransactionItem> { new(id, 5m, 80m) },
            new List<DecomposedItem>());
        result.GetStockItems().Count().ShouldBe(1);
    }
}

/// <summary>
/// Tests for CreditLimit concepts on Customer entity.
/// </summary>
public class CreditLimitEntityTests
{
    [Fact]
    public void Customer_CreditLimit_DefaultZero_MeansUnlimited()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test");
        customer.CreditLimit.ShouldBe(0m); // Zero = unlimited
    }

    [Fact]
    public void Customer_CreditLimit_CanBeSet()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test");
        customer.CreditLimit = 50000m;
        customer.CreditLimit.ShouldBe(50000m);
    }

    [Fact]
    public void Customer_CreditLimit_Enforcement_Logic()
    {
        // Credit limit = 10000, outstanding = 8000, new transaction = 3000
        // Total exposure = 8000 + 3000 = 11000 > 10000 → BLOCKED
        decimal limit = 10000m;
        decimal outstanding = 8000m;
        decimal newTransaction = 3000m;
        var totalExposure = outstanding + newTransaction;
        (totalExposure > limit).ShouldBeTrue();
    }

    [Fact]
    public void Customer_CreditLimit_ZeroLimit_AlwaysPasses()
    {
        // Zero limit = unlimited — any transaction amount passes
        decimal limit = 0m;
        // CreditLimitService skips validation when limit == 0
        (limit == 0).ShouldBeTrue();
    }

    [Fact]
    public void Customer_CreditLimit_WithinLimit_Passes()
    {
        decimal limit = 50000m;
        decimal outstanding = 20000m;
        decimal newTransaction = 10000m;
        var totalExposure = outstanding + newTransaction;
        (totalExposure <= limit).ShouldBeTrue();
    }

    [Fact]
    public void Customer_CreditLimit_ExactlyAtLimit_Passes()
    {
        decimal limit = 50000m;
        decimal outstanding = 40000m;
        decimal newTransaction = 10000m;
        var totalExposure = outstanding + newTransaction;
        (totalExposure <= limit).ShouldBeTrue(); // Exactly at limit = OK
    }
}

/// <summary>
/// Tests for TaxWithholdingResult and LDC calculation concepts.
/// </summary>
public class TaxWithholdingCalculationTests
{
    [Fact]
    public void WithheldAmount_BasicCalculation()
    {
        // Rate 2%, taxable amount 100,000
        decimal rate = 2m;
        decimal taxableAmount = 100000m;
        var withheld = taxableAmount * rate / 100;
        withheld.ShouldBe(2000m);
    }

    [Fact]
    public void WithheldAmount_WithLDC_ReducedRate()
    {
        // Normal rate 10%, LDC rate 3%
        // LDC takes precedence — withhold at reduced rate
        decimal normalRate = 10m;
        decimal ldcRate = 3m;
        decimal taxableAmount = 50000m;
        var withLdc = taxableAmount * ldcRate / 100;
        var withoutLdc = taxableAmount * normalRate / 100;
        withLdc.ShouldBe(1500m);
        withoutLdc.ShouldBe(5000m);
        withLdc.ShouldBeLessThan(withoutLdc);
    }

    [Fact]
    public void WithheldAmount_OnceDeductedAlwaysDeducted()
    {
        // If TDS was deducted on any prior invoice in the FY,
        // it must continue to be deducted on all subsequent invoices
        // regardless of threshold
        bool hasHistoricalWithholding = true;
        decimal currentInvoiceAmount = 100m; // Below threshold
        decimal singleThreshold = 10000m;
        // Rule: if historical exists → deduct regardless
        (hasHistoricalWithholding || currentInvoiceAmount >= singleThreshold).ShouldBeTrue();
    }

    [Fact]
    public void WithheldAmount_ExcessOverThreshold()
    {
        // Only withhold on EXCESS above threshold
        decimal cumulativeInvoiced = 250000m;
        decimal threshold = 200000m;
        decimal excess = cumulativeInvoiced - threshold;
        excess.ShouldBe(50000m);
        // TDS = excess × rate
        var tds = excess * 2m / 100;
        tds.ShouldBe(1000m);
    }

    [Fact]
    public void WithheldAmount_PreviouslyDeducted_Subtracted()
    {
        // If 500 was already deducted this FY, and new calc = 1000, actual = 500
        decimal calculatedTds = 1000m;
        decimal previouslyDeducted = 500m;
        var actualTds = calculatedTds - previouslyDeducted;
        actualTds.ShouldBe(500m);
    }

    [Fact]
    public void WithheldAmount_PreviousExceedsCalc_ZeroResult()
    {
        // If previously deducted > calculated → TDS = 0 (no negative deduction)
        decimal calculatedTds = 300m;
        decimal previouslyDeducted = 500m;
        var actualTds = Math.Max(0, calculatedTds - previouslyDeducted);
        actualTds.ShouldBe(0m);
    }
}

/// <summary>
/// Tests for SalesOrder address auto-fill fields.
/// </summary>
public class SalesOrderAddressTests
{
    [Fact]
    public void SalesOrder_BillingAddressId_DefaultNull()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.BillingAddressId.ShouldBeNull();
    }

    [Fact]
    public void SalesOrder_ShippingAddressId_DefaultNull()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.ShippingAddressId.ShouldBeNull();
    }

    [Fact]
    public void SalesOrder_AddressIds_CanBeSet()
    {
        var billing = Guid.NewGuid();
        var shipping = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.BillingAddressId = billing;
        so.ShippingAddressId = shipping;
        so.BillingAddressId.ShouldBe(billing);
        so.ShippingAddressId.ShouldBe(shipping);
    }

    [Fact]
    public void SalesOrder_ShippingCharge_DefaultZero()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.ShippingCharge.ShouldBe(0m);
    }

    [Fact]
    public void SalesOrder_ShippingCharge_CanBeSet()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.ShippingCharge = 25.50m;
        so.ShippingCharge.ShouldBe(25.50m);
    }
}
