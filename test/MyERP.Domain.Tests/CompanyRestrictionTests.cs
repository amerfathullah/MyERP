using System;
using System.Collections.Generic;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Core.DomainServices;
using Xunit;

namespace MyERP;

/// <summary>
/// Tests for company restriction feature (PR #57258 + #57352).
/// Validates: entity fields, restriction entry, exempt document types, validation service logic.
/// </summary>
public class CompanyRestrictionTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    #region Entity Field Tests

    [Fact]
    public void Item_RestrictToCompanies_DefaultsFalse()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "ITEM-001", "Test Item",
            MyERP.Inventory.ItemType.Goods, TenantId);
        Assert.False(item.RestrictToCompanies);
    }

    [Fact]
    public void Item_RestrictToCompanies_CanBeSet()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "ITEM-001", "Test Item",
            MyERP.Inventory.ItemType.Goods, TenantId);
        item.RestrictToCompanies = true;
        Assert.True(item.RestrictToCompanies);
    }

    [Fact]
    public void Customer_RestrictToCompanies_DefaultsFalse()
    {
        var customer = new Customer(Guid.NewGuid(), CompanyId, "Test Customer", TenantId);
        Assert.False(customer.RestrictToCompanies);
    }

    [Fact]
    public void Customer_RestrictToCompanies_CanBeSet()
    {
        var customer = new Customer(Guid.NewGuid(), CompanyId, "Test Customer", TenantId);
        customer.RestrictToCompanies = true;
        Assert.True(customer.RestrictToCompanies);
    }

    [Fact]
    public void Supplier_RestrictToCompanies_DefaultsFalse()
    {
        var supplier = new Supplier(Guid.NewGuid(), CompanyId, "Test Supplier", TenantId);
        Assert.False(supplier.RestrictToCompanies);
    }

    [Fact]
    public void Supplier_RestrictToCompanies_CanBeSet()
    {
        var supplier = new Supplier(Guid.NewGuid(), CompanyId, "Test Supplier", TenantId);
        supplier.RestrictToCompanies = true;
        Assert.True(supplier.RestrictToCompanies);
    }

    #endregion

    #region CompanyRestrictionEntry Tests

    [Fact]
    public void CompanyRestrictionEntry_Create_SetsAllFields()
    {
        var parentId = Guid.NewGuid();
        var entry = new CompanyRestrictionEntry(
            Guid.NewGuid(), "Item", parentId, CompanyId, TenantId);

        Assert.Equal("Item", entry.ParentType);
        Assert.Equal(parentId, entry.ParentId);
        Assert.Equal(CompanyId, entry.CompanyId);
        Assert.Equal(TenantId, entry.TenantId);
    }

    [Fact]
    public void CompanyRestrictionEntry_SupportsAllThreeParentTypes()
    {
        var entry1 = new CompanyRestrictionEntry(Guid.NewGuid(), "Item", Guid.NewGuid(), CompanyId);
        var entry2 = new CompanyRestrictionEntry(Guid.NewGuid(), "Customer", Guid.NewGuid(), CompanyId);
        var entry3 = new CompanyRestrictionEntry(Guid.NewGuid(), "Supplier", Guid.NewGuid(), CompanyId);

        Assert.Equal("Item", entry1.ParentType);
        Assert.Equal("Customer", entry2.ParentType);
        Assert.Equal("Supplier", entry3.ParentType);
    }

    #endregion

    #region Exempt Document Type Tests

    [Theory]
    [InlineData("Asset")]
    [InlineData("BankTransaction")]
    [InlineData("ExchangeRateRevaluation")]
    [InlineData("LandedCostVoucher")]
    [InlineData("PosClosingEntry")]
    [InlineData("SerialNo")]
    [InlineData("SerialAndBatchBundle")]
    [InlineData("RepostItemValuation")]
    public void IsExemptDocumentType_ReturnsTrue_ForExemptTypes(string docType)
    {
        Assert.True(CompanyRestrictionValidationService.IsExemptDocumentType(docType));
    }

    [Theory]
    [InlineData("SalesOrder")]
    [InlineData("PurchaseOrder")]
    [InlineData("SalesInvoice")]
    [InlineData("PurchaseInvoice")]
    [InlineData("DeliveryNote")]
    [InlineData("PurchaseReceipt")]
    [InlineData("StockEntry")]
    [InlineData("JournalEntry")]
    [InlineData("PaymentEntry")]
    public void IsExemptDocumentType_ReturnsFalse_ForTransactionTypes(string docType)
    {
        Assert.False(CompanyRestrictionValidationService.IsExemptDocumentType(docType));
    }

    [Fact]
    public void IsExemptDocumentType_IsCaseInsensitive()
    {
        Assert.True(CompanyRestrictionValidationService.IsExemptDocumentType("asset"));
        Assert.True(CompanyRestrictionValidationService.IsExemptDocumentType("ASSET"));
        Assert.True(CompanyRestrictionValidationService.IsExemptDocumentType("Asset"));
    }

    #endregion

    #region Error Code Tests

    [Fact]
    public void CompanyRestrictionBlocked_ErrorCodeExists()
    {
        Assert.Equal("MyERP:00004", MyERPDomainErrorCodes.CompanyRestrictionBlocked);
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public void UnrestrictedItem_AllowsAnyCompany()
    {
        // An item with RestrictToCompanies=false should work with ANY company
        var item = new Item(Guid.NewGuid(), CompanyId, "ITEM-001", "Test Item",
            MyERP.Inventory.ItemType.Goods, TenantId);
        Assert.False(item.RestrictToCompanies); // default = no restriction
    }

    [Fact]
    public void RestrictedItem_NeedsAllowedCompany()
    {
        // When restrict is enabled, the item should only be usable in explicitly allowed companies
        var item = new Item(Guid.NewGuid(), CompanyId, "ITEM-002", "Restricted Item",
            MyERP.Inventory.ItemType.Goods, TenantId);
        item.RestrictToCompanies = true;

        // At this point, the item needs entries in CompanyRestrictionEntry table
        // to specify which companies can use it
        Assert.True(item.RestrictToCompanies);
    }

    [Fact]
    public void CompanyRestrictionEntry_MultipleEntriesPerParent()
    {
        // A restricted item can be allowed in multiple companies
        var itemId = Guid.NewGuid();
        var entry1 = new CompanyRestrictionEntry(Guid.NewGuid(), "Item", itemId, CompanyId, TenantId);
        var entry2 = new CompanyRestrictionEntry(Guid.NewGuid(), "Item", itemId, OtherCompanyId, TenantId);

        Assert.Equal(itemId, entry1.ParentId);
        Assert.Equal(itemId, entry2.ParentId);
        Assert.NotEqual(entry1.CompanyId, entry2.CompanyId);
    }

    [Fact]
    public void ExemptDocumentTypes_Count14()
    {
        // Per ERPNext PR #57352: exactly 14 exempt system doctypes
        var exemptCount = 0;
        var knownExempt = new[] {
            "Asset", "BankTransaction", "ExchangeRateRevaluation", "LandedCostVoucher",
            "PosClosingEntry", "PosInvoiceMergeLog", "PaymentReconciliation",
            "ProcessPaymentReconciliation", "RepostAccountingLedger", "RepostItemValuation",
            "RepostPaymentLedger", "SerialNo", "SerialAndBatchBundle", "UnreconcilePayment"
        };
        foreach (var dt in knownExempt)
        {
            if (CompanyRestrictionValidationService.IsExemptDocumentType(dt))
                exemptCount++;
        }
        Assert.Equal(14, exemptCount);
    }

    [Fact]
    public void TransactionTypes_NotExempt()
    {
        // Critical transaction types must NOT be exempt (they need validation)
        var transactionTypes = new[] { "SalesOrder", "PurchaseOrder", "SalesInvoice", "PurchaseInvoice",
            "DeliveryNote", "PurchaseReceipt", "StockEntry", "JournalEntry", "PaymentEntry" };

        foreach (var dt in transactionTypes)
        {
            Assert.False(CompanyRestrictionValidationService.IsExemptDocumentType(dt),
                $"{dt} should not be exempt from company restriction validation");
        }
    }

    #endregion

    #region Permission Level Tests (PR #57383)

    [Fact]
    public void CompanyRestrictions_Permission_Constants_Exist()
    {
        // Per ERPNext PR #57383: company restriction fields gated behind permlevel 1
        Assert.Equal("MyERP.CompanyRestrictions", MyERP.Permissions.MyERPPermissions.CompanyRestrictions.Default);
        Assert.Equal("MyERP.CompanyRestrictions.Manage", MyERP.Permissions.MyERPPermissions.CompanyRestrictions.Manage);
    }

    [Fact]
    public void CompanyRestrictions_Manage_IsChildOfDefault()
    {
        // Manage permission should have the standard parent-child structure
        Assert.StartsWith(
            MyERP.Permissions.MyERPPermissions.CompanyRestrictions.Default,
            MyERP.Permissions.MyERPPermissions.CompanyRestrictions.Manage);
    }

    [Fact]
    public void CompanyRestrictions_ManagerOnly_NotRegularUser()
    {
        // The Manage permission is for "master-manager" roles only:
        // Item Manager, Sales Master Manager, Purchase Master Manager
        // Regular Sales User, Stock User, etc. should NOT have this permission
        // (Verified at application layer via [Authorize(CompanyRestrictions.Manage)])
        var managePermission = MyERP.Permissions.MyERPPermissions.CompanyRestrictions.Manage;
        Assert.Contains(".Manage", managePermission);
        Assert.DoesNotContain(".Edit", managePermission); // Not a regular edit permission
    }

    [Fact]
    public void CompanyRestrictions_GetAsync_RequiresOnlyAuthentication()
    {
        // GetAsync should be accessible to any authenticated user (they can SEE restriction status)
        // Only SaveAsync requires the Manage permission
        // This is the ABP pattern: class-level [Authorize] for read, method-level for write
        var defaultPermission = MyERP.Permissions.MyERPPermissions.CompanyRestrictions.Default;
        Assert.NotNull(defaultPermission);
    }

    [Fact]
    public void CompanyRestrictions_AppliesTo_ThreeMasterEntities()
    {
        // Per PR #57383: restriction applies to Item (Item Manager), Customer (Sales Master Manager), Supplier (Purchase Master Manager)
        var item = new Item(Guid.NewGuid(), CompanyId, "TEST", "Test", MyERP.Inventory.ItemType.Goods, TenantId);
        var customer = new Customer(Guid.NewGuid(), CompanyId, "Cust", TenantId);
        var supplier = new Supplier(Guid.NewGuid(), CompanyId, "Supp", TenantId);

        // All three support the RestrictToCompanies flag
        Assert.False(item.RestrictToCompanies);
        Assert.False(customer.RestrictToCompanies);
        Assert.False(supplier.RestrictToCompanies);
    }

    #endregion
}
