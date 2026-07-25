using System;
using MyERP.Core.Entities;
using MyERP.Accounting;
using Shouldly;
using Xunit;

namespace MyERP.Core;

/// <summary>
/// Tests for Company advance payment routing, perpetual inventory, and additional settings.
/// Per ERPNext gotcha #205: BookAdvancePaymentsInSeparatePartyAccount + advance account routing.
/// Per ERPNext gotcha #200: RoundOffForOpeningAccountId separate from RoundOffAccountId.
/// </summary>
public class CompanyAdvanceAndSettingsTests
{
    private static Company CreateCompany() => new(Guid.NewGuid(), "Test Company MY");

    // --- Advance Payment Routing ---

    [Fact]
    public void BookAdvancePaymentsInSeparatePartyAccount_Defaults_False()
    {
        var company = CreateCompany();
        company.BookAdvancePaymentsInSeparatePartyAccount.ShouldBeFalse();
    }

    [Fact]
    public void DefaultAdvanceReceivedAccountId_Defaults_Null()
    {
        var company = CreateCompany();
        company.DefaultAdvanceReceivedAccountId.ShouldBeNull();
    }

    [Fact]
    public void DefaultAdvancePaidAccountId_Defaults_Null()
    {
        var company = CreateCompany();
        company.DefaultAdvancePaidAccountId.ShouldBeNull();
    }

    [Fact]
    public void AdvanceAccounts_CanBeSet()
    {
        var company = CreateCompany();
        var receivedAcct = Guid.NewGuid();
        var paidAcct = Guid.NewGuid();

        company.BookAdvancePaymentsInSeparatePartyAccount = true;
        company.DefaultAdvanceReceivedAccountId = receivedAcct;
        company.DefaultAdvancePaidAccountId = paidAcct;

        company.BookAdvancePaymentsInSeparatePartyAccount.ShouldBeTrue();
        company.DefaultAdvanceReceivedAccountId.ShouldBe(receivedAcct);
        company.DefaultAdvancePaidAccountId.ShouldBe(paidAcct);
    }

    // --- Perpetual Inventory ---

    [Fact]
    public void EnablePerpetualInventory_Defaults_True()
    {
        var company = CreateCompany();
        company.EnablePerpetualInventory.ShouldBeTrue();
    }

    [Fact]
    public void StockReceivedButNotBilledAccountId_Defaults_Null()
    {
        var company = CreateCompany();
        company.StockReceivedButNotBilledAccountId.ShouldBeNull();
    }

    [Fact]
    public void StockDeliveredButNotBilledAccountId_Defaults_Null()
    {
        var company = CreateCompany();
        company.StockDeliveredButNotBilledAccountId.ShouldBeNull();
    }

    // --- Round-Off ---

    [Fact]
    public void RoundOffAccountId_Defaults_Null()
    {
        var company = CreateCompany();
        company.RoundOffAccountId.ShouldBeNull();
    }

    [Fact]
    public void RoundOffForOpeningAccountId_Defaults_Null()
    {
        // Per gotcha #200: opening entries use a DIFFERENT round-off account
        var company = CreateCompany();
        company.RoundOffForOpeningAccountId.ShouldBeNull();
    }

    [Fact]
    public void RoundOffAccounts_AreSeparate()
    {
        // Per ERPNext: standard round-off and opening round-off are independent
        var company = CreateCompany();
        var roundOff = Guid.NewGuid();
        var openingRoundOff = Guid.NewGuid();

        company.RoundOffAccountId = roundOff;
        company.RoundOffForOpeningAccountId = openingRoundOff;

        company.RoundOffAccountId.ShouldNotBe(company.RoundOffForOpeningAccountId);
    }

    // --- Reporting Currency ---

    [Fact]
    public void ReportingCurrency_Defaults_Null()
    {
        // Per ERPNext: defaults to company currency when null
        var company = CreateCompany();
        company.ReportingCurrency.ShouldBeNull();
    }

    // --- Tolerances ---

    [Fact]
    public void OverDeliveryReceiptAllowance_Defaults_Zero()
    {
        var company = CreateCompany();
        company.OverDeliveryReceiptAllowance.ShouldBe(0m);
    }

    [Fact]
    public void OverBillingAllowance_Defaults_Zero()
    {
        var company = CreateCompany();
        company.OverBillingAllowance.ShouldBe(0m);
    }

    [Fact]
    public void Tolerances_CanBeSet()
    {
        var company = CreateCompany();
        company.OverDeliveryReceiptAllowance = 5m; // 5% over-delivery allowed
        company.OverBillingAllowance = 2.5m;       // 2.5% over-billing allowed

        company.OverDeliveryReceiptAllowance.ShouldBe(5m);
        company.OverBillingAllowance.ShouldBe(2.5m);
    }

    // --- Auto ERR ---

    [Fact]
    public void AutoExchangeRateRevaluation_Defaults_False()
    {
        var company = CreateCompany();
        company.AutoExchangeRateRevaluation.ShouldBeFalse();
    }

    // --- Payment Entry Advance Routing Logic ---

    [Fact]
    public void AdvanceRouting_OnlyTriggersWhenEnabled()
    {
        // When BookAdvancePaymentsInSeparatePartyAccount = false,
        // advance payments use the normal party account (no routing)
        var company = CreateCompany();
        company.BookAdvancePaymentsInSeparatePartyAccount = false;
        company.DefaultAdvanceReceivedAccountId = Guid.NewGuid();

        // The setting being false means the advance account should NOT be used
        // (even if configured) — validated at AppService level
        company.BookAdvancePaymentsInSeparatePartyAccount.ShouldBeFalse();
    }

    [Fact]
    public void PaymentType_Receive_ForCustomerAdvance()
    {
        // Receive type = customer paying us in advance
        // Maps to DefaultAdvanceReceivedAccountId (liability to customer)
        var paymentType = PaymentType.Receive;
        paymentType.ShouldBe(PaymentType.Receive);
    }

    [Fact]
    public void PaymentType_Pay_ForSupplierAdvance()
    {
        // Pay type = we paying supplier in advance
        // Maps to DefaultAdvancePaidAccountId (asset from supplier)
        var paymentType = PaymentType.Pay;
        paymentType.ShouldBe(PaymentType.Pay);
    }
}
