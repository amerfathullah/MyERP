using System;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for service wiring: LoyaltyPoints, InterCompany, ShippingRule
/// verifying entities have the required fields for the wiring to work.
/// </summary>
public class ServiceWiringTests
{
    // === Customer.LoyaltyProgramId ===

    [Fact]
    public void Customer_LoyaltyProgramId_DefaultNull()
    {
        var c = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test Co");
        c.LoyaltyProgramId.ShouldBeNull();
    }

    [Fact]
    public void Customer_LoyaltyProgramId_CanBeSet()
    {
        var c = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test Co");
        var programId = Guid.NewGuid();
        c.LoyaltyProgramId = programId;
        c.LoyaltyProgramId.ShouldBe(programId);
    }

    [Fact]
    public void Customer_RepresentsCompanyId_ForInterCompany()
    {
        var c = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Subsidiary Co");
        var targetCompanyId = Guid.NewGuid();
        c.RepresentsCompanyId = targetCompanyId;
        c.RepresentsCompanyId.ShouldBe(targetCompanyId);
    }

    // === SalesOrder.ShippingCharge ===

    [Fact]
    public void SalesOrder_ShippingCharge_DefaultZero()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-001", DateTime.Today);
        so.ShippingCharge.ShouldBe(0);
    }

    [Fact]
    public void SalesOrder_ShippingCharge_CanBeSet()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-001", DateTime.Today);
        so.ShippingCharge = 25.50m;
        so.ShippingCharge.ShouldBe(25.50m);
    }

    // === ShippingRule Calculate ===

    [Fact]
    public void ShippingRule_Fixed_ReturnsFixedAmount()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Flat Rate", Sales.ShippingRuleType.Selling,
            Sales.ShippingCalculationMode.Fixed, Guid.NewGuid());
        rule.FixedAmount = 15.00m;
        var amount = rule.Calculate(1000m);
        amount.ShouldBe(15.00m);
    }

    [Fact]
    public void ShippingRule_AppliesToCountry_GlobalRule()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Global Shipping", Sales.ShippingRuleType.Selling,
            Sales.ShippingCalculationMode.Fixed, Guid.NewGuid());
        // No countries = applies globally
        rule.AppliesToCountry("MY").ShouldBeTrue();
        rule.AppliesToCountry("US").ShouldBeTrue();
    }

    [Fact]
    public void ShippingRule_IsEnabled_DefaultTrue()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Test", Sales.ShippingRuleType.Selling,
            Sales.ShippingCalculationMode.Fixed, Guid.NewGuid());
        rule.IsEnabled.ShouldBeTrue();
    }

    // === LoyaltyProgram tier+points (entity-level) ===

    [Fact]
    public void LoyaltyProgram_EarningFlow_Concept()
    {
        // Simulates: customer with loyalty program makes a 1000 MYR purchase
        var program = new LoyaltyProgram(Guid.NewGuid(), Guid.NewGuid(),
            "MyRewards", 100m, 365);
        program.AddTier("Standard", 0m, 1.0m, 0.5m);

        var tier = program.DetermineTier(0m, 1000m);
        tier.TierName.ShouldBe("Standard");

        var points = program.CalculatePointsEarned(1000m, tier);
        points.ShouldBe(10); // FLOOR(1000/100) × 1.0

        var value = program.CalculateRedemptionValue(10, tier);
        value.ShouldBe(5m); // 10 × 0.5
    }

    [Fact]
    public void LoyaltyPointEntry_CancelGuard_Concept()
    {
        // Simulates: earning entry that has been redeemed → should block cancel
        var earnEntry = new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), 10, DateTime.Today, DateTime.Today.AddYears(1));
        earnEntry.InvoiceType = "SalesInvoice";
        earnEntry.InvoiceId = Guid.NewGuid();

        // Create redemption against earning entry
        var redeemEntry = new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), -5, DateTime.Today);
        redeemEntry.RedeemAgainstId = earnEntry.Id;

        earnEntry.IsEarning.ShouldBeTrue();
        redeemEntry.IsRedemption.ShouldBeTrue();
        redeemEntry.RedeemAgainstId.ShouldBe(earnEntry.Id);
    }

    // === InterCompany transaction concept ===

    [Fact]
    public void InterCompany_BidirectionalLink_Concept()
    {
        // Customer in Company A represents Company B
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Company B Branch");
        var companyBId = Guid.NewGuid();
        customer.RepresentsCompanyId = companyBId;

        // Supplier in Company B represents Company A
        var supplier = new Purchasing.Entities.Supplier(Guid.NewGuid(), companyBId, "Company A Branch");
        var companyAId = customer.CompanyId;
        supplier.RepresentsCompanyId = companyAId;

        // Bidirectional: customer → company B, supplier → company A
        customer.RepresentsCompanyId.ShouldBe(companyBId);
        supplier.RepresentsCompanyId.ShouldBe(companyAId);
    }
}
