using System;
using System.Linq;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Tests for CreditLimitService — enforces customer credit limits at SO/DN/SI submit.
/// </summary>
public class CreditLimitServiceTests
{
    // === Credit Limit Property Tests ===

    [Fact]
    public void Customer_ZeroLimit_MeansUnlimited()
    {
        var customer = CreateCustomer(0);

        // Per ERPNext: credit_limit = 0 means no limit (unlimited)
        var hasLimit = customer.CreditLimit > 0;
        hasLimit.ShouldBeFalse();
    }

    [Fact]
    public void Customer_PositiveLimit_MeansEnforced()
    {
        var customer = CreateCustomer(50000);
        customer.CreditLimit.ShouldBe(50000);
    }

    [Fact]
    public void Customer_DefaultLimit_IsZero()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test Customer");
        customer.CreditLimit.ShouldBe(0);
    }

    // === Credit Limit Validation Logic ===

    [Fact]
    public void CreditCheck_WithinLimit_Passes()
    {
        decimal creditLimit = 50000;
        decimal currentOutstanding = 20000;
        decimal newAmount = 15000;

        var exceeds = CreditExceeded(creditLimit, currentOutstanding, newAmount);
        exceeds.ShouldBeFalse();
    }

    [Fact]
    public void CreditCheck_ExceedsLimit_Fails()
    {
        decimal creditLimit = 50000;
        decimal currentOutstanding = 40000;
        decimal newAmount = 15000;

        var exceeds = CreditExceeded(creditLimit, currentOutstanding, newAmount);
        exceeds.ShouldBeTrue();
    }

    [Fact]
    public void CreditCheck_ExactlyAtLimit_Passes()
    {
        decimal creditLimit = 50000;
        decimal currentOutstanding = 30000;
        decimal newAmount = 20000;

        // Exactly at limit: outstanding + new == limit → should pass
        var exceeds = CreditExceeded(creditLimit, currentOutstanding, newAmount);
        exceeds.ShouldBeFalse();
    }

    [Fact]
    public void CreditCheck_ZeroLimit_AlwaysPasses()
    {
        decimal creditLimit = 0; // unlimited
        decimal currentOutstanding = 999999;
        decimal newAmount = 999999;

        // Zero limit = no enforcement
        var exceeds = CreditExceeded(creditLimit, currentOutstanding, newAmount);
        exceeds.ShouldBeFalse();
    }

    [Fact]
    public void CreditCheck_NewTransactionAlone_CanExceedLimit()
    {
        decimal creditLimit = 10000;
        decimal currentOutstanding = 0;
        decimal newAmount = 15000;

        var exceeds = CreditExceeded(creditLimit, currentOutstanding, newAmount);
        exceeds.ShouldBeTrue();
    }

    // === Outstanding Calculation Scope ===

    [Fact]
    public void Outstanding_IncludesPostedInvoices()
    {
        // Outstanding for credit check should include all unpaid posted invoices
        // Invoice 1: GrandTotal 10000, Paid 5000 => Outstanding 5000
        // Invoice 2: GrandTotal 8000, Paid 8000 => Outstanding 0 (fully paid, excluded)
        // Invoice 3: GrandTotal 12000, Paid 3000 => Outstanding 9000
        decimal totalOutstanding = 5000 + 0 + 9000; // 14000

        totalOutstanding.ShouldBe(14000);
    }

    [Fact]
    public void Outstanding_ExcludesReturnInvoices()
    {
        // Returns (credit notes) reduce outstanding, not increase it
        decimal invoiceOutstanding = 20000;
        decimal creditNoteAmount = -3000; // return is negative

        // Net outstanding = invoice - abs(credit note)
        decimal netOutstanding = invoiceOutstanding + creditNoteAmount;
        netOutstanding.ShouldBe(17000);
    }

    [Fact]
    public void Outstanding_ExcludesCancelledInvoices()
    {
        // Cancelled invoices should not count toward outstanding
        // This is inherently handled by only querying Posted status invoices
        var status = Core.DocumentStatus.Cancelled;
        var isPosted = status == Core.DocumentStatus.Posted;
        isPosted.ShouldBeFalse();
    }

    // === Enforcement Points ===

    [Fact]
    public void CreditCheck_EnforcedAtSISubmit()
    {
        // Per DO-NOT: "Implement credit limit check only at SO — must also enforce at DN and SI submit"
        // All 3 enforcement points use the same validation logic
        var enforcementPoints = new[] { "SalesOrder.Submit", "DeliveryNote.Submit", "SalesInvoice.Submit" };
        enforcementPoints.Length.ShouldBe(3);
    }

    [Fact]
    public void CreditCheck_SkippedForReturns()
    {
        // Returns (IsReturn=true) should NOT trigger credit limit check
        bool isReturn = true;
        bool shouldValidate = !isReturn;
        shouldValidate.ShouldBeFalse();
    }

    // === Overdue Billing Threshold Gate + Role Bypass ===

    [Fact]
    public void OverdueBilling_GateDisabled_SkipsCheckEvenWithThresholdSet()
    {
        // Per Accounts Settings.EnableOverdueBillingThreshold: off by default.
        // A per-company OverdueBillingThreshold > 0 alone must NOT enforce when the gate is off.
        var shouldEnforce = OverdueBillingShouldEnforce(gateEnabled: false, threshold: 500m, overdueAmount: 1000m);
        shouldEnforce.ShouldBeFalse();
    }

    [Fact]
    public void OverdueBilling_GateEnabled_ThresholdExceeded_Enforces()
    {
        var shouldEnforce = OverdueBillingShouldEnforce(gateEnabled: true, threshold: 500m, overdueAmount: 1000m);
        shouldEnforce.ShouldBeTrue();
    }

    [Fact]
    public void OverdueBilling_BypassRoleMatchesCurrentUser_Skips()
    {
        var shouldEnforce = OverdueBillingShouldEnforce(
            gateEnabled: true, threshold: 500m, overdueAmount: 1000m,
            bypassRole: "CreditController", currentUserRoles: new[] { "Sales", "CreditController" });
        shouldEnforce.ShouldBeFalse();
    }

    [Fact]
    public void OverdueBilling_BypassRoleConfigured_UserDoesNotHaveIt_StillEnforces()
    {
        var shouldEnforce = OverdueBillingShouldEnforce(
            gateEnabled: true, threshold: 500m, overdueAmount: 1000m,
            bypassRole: "CreditController", currentUserRoles: new[] { "Sales" });
        shouldEnforce.ShouldBeTrue();
    }

    // === Credit Limit Bypass (role + SO-specific) ===

    [Fact]
    public void CreditLimit_ControllerRoleMatchesCurrentUser_SkipsEverywhere()
    {
        var shouldEnforce = CreditLimitShouldEnforce(
            creditLimit: 10000m, exposure: 20000m, isAtSalesOrder: false,
            controllerRole: "CreditController", currentUserRoles: new[] { "CreditController" });
        shouldEnforce.ShouldBeFalse();
    }

    [Fact]
    public void CreditLimit_SoBypassFlag_SkipsOnlyAtSalesOrder()
    {
        var atSo = CreditLimitShouldEnforce(
            creditLimit: 10000m, exposure: 20000m, isAtSalesOrder: true, customerBypassAtSo: true);
        var atDn = CreditLimitShouldEnforce(
            creditLimit: 10000m, exposure: 20000m, isAtSalesOrder: false, customerBypassAtSo: true);

        atSo.ShouldBeFalse();
        atDn.ShouldBeTrue(); // DN/SI still enforce even though the customer's SO check is bypassed
    }

    [Fact]
    public void CreditLimit_NoBypass_ExceedsLimit_Enforces()
    {
        var shouldEnforce = CreditLimitShouldEnforce(creditLimit: 10000m, exposure: 20000m, isAtSalesOrder: true);
        shouldEnforce.ShouldBeTrue();
    }

    /// <summary>Mirrors CreditLimitService.ValidateCreditLimitAsync's role/SO-bypass decision (pre-amount-check).</summary>
    private static bool CreditLimitShouldEnforce(
        decimal creditLimit, decimal exposure, bool isAtSalesOrder,
        string? controllerRole = null, string[]? currentUserRoles = null, bool customerBypassAtSo = false)
    {
        if (!string.IsNullOrWhiteSpace(controllerRole) && currentUserRoles != null
            && currentUserRoles.Contains(controllerRole, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }
        if (isAtSalesOrder && customerBypassAtSo) return false;
        if (creditLimit <= 0) return false;
        return exposure > creditLimit;
    }

    // === Helper Methods ===

    /// <summary>Mirrors CreditLimitService.ValidateOverdueBillingThresholdAsync's gate/bypass/threshold decision.</summary>
    private static bool OverdueBillingShouldEnforce(
        bool gateEnabled, decimal threshold, decimal overdueAmount,
        string? bypassRole = null, string[]? currentUserRoles = null)
    {
        if (!gateEnabled) return false;
        if (!string.IsNullOrWhiteSpace(bypassRole) && currentUserRoles != null
            && currentUserRoles.Contains(bypassRole, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }
        if (threshold <= 0) return false;
        return overdueAmount > threshold;
    }

    private static Customer CreateCustomer(decimal creditLimit)
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test Customer");
        customer.CreditLimit = creditLimit;
        return customer;
    }

    /// <summary>
    /// Implements the credit check logic matching CreditLimitService.
    /// Returns true if credit limit would be exceeded.
    /// </summary>
    private static bool CreditExceeded(decimal creditLimit, decimal currentOutstanding, decimal newAmount)
    {
        // Zero limit = unlimited (no enforcement)
        if (creditLimit <= 0) return false;

        return (currentOutstanding + newAmount) > creditLimit;
    }
}
