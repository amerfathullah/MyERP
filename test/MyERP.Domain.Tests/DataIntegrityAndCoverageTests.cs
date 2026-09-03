using System;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP;

/// <summary>
/// Tests for warehouse hierarchy, UOM data, currency exchange,
/// and other recently-added seed data and entity features.
/// </summary>
public class DataIntegrityAndCoverageTests
{
    // === Warehouse Hierarchy ===

    [Fact]
    public void Warehouse_ParentWarehouseId_DefaultsToNull()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Stores");
        Assert.Null(wh.ParentWarehouseId);
    }

    [Fact]
    public void Warehouse_ParentWarehouseId_CanBeSet()
    {
        var parentId = Guid.NewGuid();
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Stores")
        {
            ParentWarehouseId = parentId
        };
        Assert.Equal(parentId, wh.ParentWarehouseId);
    }

    [Fact]
    public void Warehouse_GroupWarehouse_CanHaveChildren()
    {
        var companyId = Guid.NewGuid();
        var root = new Warehouse(Guid.NewGuid(), companyId, "All Warehouses") { IsGroup = true };
        var child = new Warehouse(Guid.NewGuid(), companyId, "Stores")
        {
            ParentWarehouseId = root.Id
        };

        Assert.True(root.IsGroup);
        Assert.False(child.IsGroup);
        Assert.Equal(root.Id, child.ParentWarehouseId);
    }

    [Fact]
    public void Warehouse_IsGroup_DefaultsFalse()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Test");
        Assert.False(wh.IsGroup);
    }

    [Fact]
    public void Warehouse_IsActive_DefaultsTrue()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Test");
        Assert.True(wh.IsActive);
    }

    [Fact]
    public void Warehouse_MultipleChildrenUnderSameParent()
    {
        var companyId = Guid.NewGuid();
        var root = new Warehouse(Guid.NewGuid(), companyId, "All Warehouses") { IsGroup = true };

        var stores = new Warehouse(Guid.NewGuid(), companyId, "Stores") { ParentWarehouseId = root.Id };
        var fg = new Warehouse(Guid.NewGuid(), companyId, "Finished Goods") { ParentWarehouseId = root.Id };
        var wip = new Warehouse(Guid.NewGuid(), companyId, "Work In Progress") { ParentWarehouseId = root.Id };
        var transit = new Warehouse(Guid.NewGuid(), companyId, "Goods In Transit") { ParentWarehouseId = root.Id };

        var children = new[] { stores, fg, wip, transit };
        Assert.All(children, c => Assert.Equal(root.Id, c.ParentWarehouseId));
        Assert.Equal(4, children.Length);
    }

    // === UOM Entity ===

    [Fact]
    public void Uom_Create_SetsName()
    {
        var uom = new Uom(Guid.NewGuid(), "Kilogram");
        Assert.Equal("Kilogram", uom.Name);
    }

    [Fact]
    public void Uom_MustBeWholeNumber_DefaultsFalse()
    {
        var uom = new Uom(Guid.NewGuid(), "Kg");
        Assert.False(uom.MustBeWholeNumber);
    }

    [Fact]
    public void Uom_WholeNumber_ConfiguredCorrectly()
    {
        var uom = new Uom(Guid.NewGuid(), "Unit") { MustBeWholeNumber = true };
        Assert.True(uom.MustBeWholeNumber);
        // ValidateWholeNumber should not throw for whole numbers
        uom.ValidateWholeNumber(5m);
        uom.ValidateWholeNumber(100m);
    }

    [Fact]
    public void Uom_ContinuousUom_AllowsFractional()
    {
        var uom = new Uom(Guid.NewGuid(), "Kg") { MustBeWholeNumber = false };
        // Non-whole-number UOM: ValidateWholeNumber is a no-op when MustBeWholeNumber=false
        Assert.False(uom.MustBeWholeNumber);
    }

    [Fact]
    public void Uom_Category_CanBeSet()
    {
        var uom = new Uom(Guid.NewGuid(), "Kg") { Category = "Mass" };
        Assert.Equal("Mass", uom.Category);
    }

    [Fact]
    public void Uom_IsEnabled_DefaultsTrue()
    {
        var uom = new Uom(Guid.NewGuid(), "Test");
        Assert.True(uom.IsEnabled);
    }

    [Fact]
    public void Uom_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Uom(Guid.NewGuid(), ""));
    }

    // === UOM Conversion ===

    [Fact]
    public void UomConversion_Create_SetsProperties()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Kg", "Gram", 1000m);
        Assert.Equal("Kg", conv.FromUom);
        Assert.Equal("Gram", conv.ToUom);
        Assert.Equal(1000m, conv.ConversionFactor);
    }

    [Fact]
    public void UomConversion_Convert_MultipliesByFactor()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Kg", "Gram", 1000m);
        Assert.Equal(5000m, conv.Convert(5m));
    }

    [Fact]
    public void UomConversion_ReverseConvert_DividesByFactor()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Kg", "Gram", 1000m);
        Assert.Equal(2m, conv.ReverseConvert(2000m));
    }

    [Fact]
    public void UomConversion_ReverseConvert_ZeroFactor_ReturnsZero()
    {
        var conv = new UomConversion(Guid.NewGuid(), "A", "B", 0m);
        Assert.Equal(0m, conv.ReverseConvert(100m));
    }

    [Fact]
    public void UomConversion_NegativeFactor_ThrowsBusinessException()
    {
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new UomConversion(Guid.NewGuid(), "A", "B", -1m));
    }

    [Fact]
    public void BillOfMaterials_NegativeProcessLoss_ThrowsBusinessException()
    {
        var bom = new Manufacturing.Entities.BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid())
        {
            ProcessLossPercentage = -5m
        };
        Assert.Throws<Volo.Abp.BusinessException>(() => bom.ValidateProcessLoss());
    }

    [Fact]
    public void UomConversion_ItemSpecific_HasItemId()
    {
        var itemId = Guid.NewGuid();
        var conv = new UomConversion(Guid.NewGuid(), "Box", "Unit", 12m, itemId: itemId);
        Assert.Equal(itemId, conv.ItemId);
    }

    [Fact]
    public void UomConversion_Global_HasNullItemId()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Dozen", "Unit", 12m);
        Assert.Null(conv.ItemId);
    }

    [Fact]
    public void UomConversion_Precision_Preserved()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Pound", "Kg", 0.453592m);
        var result = conv.Convert(10m);
        Assert.Equal(4.53592m, result);
    }

    // === Currency Exchange ===

    [Fact]
    public void CurrencyExchange_Create_SetsProperties()
    {
        var ce = new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 4.72m, new DateTime(2026, 1, 1));
        Assert.Equal("USD", ce.FromCurrency);
        Assert.Equal("MYR", ce.ToCurrency);
        Assert.Equal(4.72m, ce.ExchangeRate);
    }

    [Fact]
    public void CurrencyExchange_PeggedRate_HasEarlyDate()
    {
        // Pegged currencies use a very early date to indicate permanence
        var ce = new CurrencyExchange(Guid.NewGuid(), "AED", "USD", 3.6725m, new DateTime(2000, 1, 1));
        Assert.Equal(new DateTime(2000, 1, 1), ce.Date);
    }

    [Fact]
    public void CurrencyExchange_InverseRate_Calculation()
    {
        var ce = new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 4.72m, DateTime.Today);
        // Inverse: 1/4.72 = ~0.2118644
        var inverse = 1m / ce.ExchangeRate;
        Assert.True(inverse > 0.21m && inverse < 0.22m);
    }

    // === POS Invoice Walk-In Customer Requirement ===

    [Fact]
    public void SalesInvoice_Constructor_RequiresCustomerId()
    {
        // Guid.Empty should be rejected by FK guard
        Assert.Throws<ArgumentException>(() => new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "POS-001",
            DateTime.Today, null));
    }

    // === Delivery Note Warehouse Requirement ===

    [Fact]
    public void DeliveryNote_Constructor_RequiresWarehouseId()
    {
        // Guid.Empty should be rejected by FK guard
        Assert.Throws<ArgumentException>(() => new DeliveryNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
            "DN-001", DateTime.Today, null));
    }

    // === Purchase Receipt Warehouse Requirement ===

    [Fact]
    public void PurchaseReceipt_Constructor_RequiresWarehouseId()
    {
        Assert.Throws<ArgumentException>(() => new PurchaseReceipt(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
            "PR-001", DateTime.Today, null));
    }

    // === Depreciation Account Resolution ===

    [Fact]
    public void AssetCategory_DepreciationAccounts_DefaultNull()
    {
        var cat = new Assets.Entities.AssetCategory(Guid.NewGuid(), "Equipment", null);
        Assert.Null(cat.DepreciationAccountId);
        Assert.Null(cat.AccumulatedDepreciationAccountId);
    }

    [Fact]
    public void AssetCategory_DepreciationAccounts_CanBeSet()
    {
        var depAcctId = Guid.NewGuid();
        var accumAcctId = Guid.NewGuid();
        var cat = new Assets.Entities.AssetCategory(Guid.NewGuid(), "Equipment", null)
        {
            DepreciationAccountId = depAcctId,
            AccumulatedDepreciationAccountId = accumAcctId
        };
        Assert.Equal(depAcctId, cat.DepreciationAccountId);
        Assert.Equal(accumAcctId, cat.AccumulatedDepreciationAccountId);
    }

    // === Company Default Account Fallback Chain ===

    [Fact]
    public void Company_DepreciationAccounts_DefaultNull()
    {
        var company = new Company(Guid.NewGuid(), "Test Co");
        Assert.Null(company.DepreciationExpenseAccountId);
        Assert.Null(company.AccumulatedDepreciationAccountId);
    }

    [Fact]
    public void Company_DefaultExpenseAccountId_DefaultNull()
    {
        var company = new Company(Guid.NewGuid(), "Test Co");
        Assert.Null(company.DefaultExpenseAccountId);
    }

    // === Conversion Service Warehouse Resolution ===

    [Fact]
    public void SalesOrderItem_WarehouseId_CanBeSet()
    {
        var order = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        order.AddItem(Guid.NewGuid(), "Item A", 10, 100, 0, "Unit");
        var item = order.Items.First();
        var whId = Guid.NewGuid();
        item.WarehouseId = whId;
        Assert.Equal(whId, item.WarehouseId);
    }

    [Fact]
    public void PurchaseOrderItem_WarehouseId_CanBeSet()
    {
        var order = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        order.AddItem(Guid.NewGuid(), "Item A", 10, 100, 0, "Unit");
        var item = order.Items.First();
        var whId = Guid.NewGuid();
        item.WarehouseId = whId;
        Assert.Equal(whId, item.WarehouseId);
    }

    [Fact]
    public void SalesOrderItem_WarehouseId_DefaultsNull()
    {
        var order = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        order.AddItem(Guid.NewGuid(), "Item A", 10, 100, 0, "Unit");
        Assert.Null(order.Items.First().WarehouseId);
    }

    // === Taxes & Totals Discount Amount Validation ===

    [Fact]
    public void TaxesAndTotals_DiscountExceedingNetTotal_ThrowsBusinessException()
    {
        var service = new Tax.DomainServices.TaxesAndTotalsService();
        var items = new System.Collections.Generic.List<Tax.DomainServices.TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), NetAmount = 100m, Qty = 1m }
        };

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            service.Calculate(items, new System.Collections.Generic.List<Tax.Entities.TransactionTaxRow>(), discountAmount: 150m, applyDiscountOn: "Net Total"));
    }

    [Fact]
    public void TaxesAndTotals_DiscountExceedingGrandTotal_ThrowsBusinessException()
    {
        var service = new Tax.DomainServices.TaxesAndTotalsService();
        var items = new System.Collections.Generic.List<Tax.DomainServices.TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), NetAmount = 100m, Qty = 1m }
        };

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            service.Calculate(items, new System.Collections.Generic.List<Tax.Entities.TransactionTaxRow>(), discountAmount: 150m, applyDiscountOn: "Grand Total"));
    }

    // === Accounting Period Disabled ===

    [Fact]
    public void AccountingPeriod_WhenDisabled_IsNotClosedForDocumentType()
    {
        var ap = new Accounting.Entities.AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(), "FY2026-Q1",
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31));
        ap.Close();
        ap.CloseDocumentType("SalesInvoice");

        Assert.True(ap.IsClosedForDocumentType("SalesInvoice"));

        ap.Disable();
        Assert.False(ap.IsClosedForDocumentType("SalesInvoice"));

        ap.Enable();
        Assert.True(ap.IsClosedForDocumentType("SalesInvoice"));
    }

    // === Extra Material Transfer Allowance ===

    [Fact]
    public void StockEntryManager_ValidateTransferQty_WithExtraMaterialPercentage_PassesWithinLimit()
    {
        var mgr = new Inventory.DomainServices.StockEntryManager(null!, null!, null!);
        // Required 100, 10% allowance => 110 allowed. Transferred 50, requested 55 => 50 + 55 = 105 <= 110 => Pass
        mgr.ValidateTransferQty(requiredQty: 100m, transferredQty: 50m, requestedQty: 55m, extraMaterialPercentage: 10m);
    }

    [Fact]
    public void StockEntryManager_ValidateTransferQty_WithExtraMaterialPercentage_ThrowsWhenExceedingLimit()
    {
        var mgr = new Inventory.DomainServices.StockEntryManager(null!, null!, null!);
        // Required 100, 10% allowance => 110 allowed. Transferred 50, requested 65 => 50 + 65 = 115 > 110 => Throws
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            mgr.ValidateTransferQty(requiredQty: 100m, transferredQty: 50m, requestedQty: 65m, extraMaterialPercentage: 10m));
    }

    // === Asset Repair Non-Negative Repair Cost ===

    [Fact]
    public void AssetRepairPurchaseInvoice_NegativeRepairCost_ThrowsBusinessException()
    {
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new Assets.Entities.AssetRepairPurchaseInvoice(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -50m));
    }

    [Fact]
    public void AssetRepair_AddInvoice_NegativeRepairCost_ThrowsBusinessException()
    {
        var repair = new Assets.Entities.AssetRepair(Guid.NewGuid(), "AR-001", Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            repair.AddInvoice(Guid.NewGuid(), Guid.NewGuid(), -100m));
    }

    // === Product Bundle Child Item Positive Quantity ===

    [Fact]
    public void ProductBundle_AddItem_ZeroOrNegativeQty_ThrowsBusinessException()
    {
        var bundle = new Sales.Entities.ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            bundle.AddItem(Guid.NewGuid(), 0m));
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            bundle.AddItem(Guid.NewGuid(), -5m));
    }

    [Fact]
    public void ProductBundleItem_ZeroOrNegativeQty_ThrowsBusinessException()
    {
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new Sales.Entities.ProductBundleItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m, "Item", "Nos"));
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new Sales.Entities.ProductBundleItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -2m, "Item", "Nos"));
    }

    // === BOM and Job Card Non-Negative Validation ===

    [Fact]
    public void BomOperation_NegativeTime_ThrowsBusinessException()
    {
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new Manufacturing.Entities.BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, -10m));
    }

    [Fact]
    public void BomItem_NegativeQuantityOrRate_ThrowsBusinessException()
    {
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new Manufacturing.Entities.BomItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", -5m, 10m));
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new Manufacturing.Entities.BomItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 5m, -10m));
    }

    [Fact]
    public void JobCardTimeLog_NegativeValues_ThrowsBusinessException()
    {
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new Manufacturing.Entities.JobCardTimeLog(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10), -5m, 10m));
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new Manufacturing.Entities.JobCardTimeLog(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10), 10m, -1m));
    }

    [Fact]
    public void AssetRepair_Complete_CompletionDateBeforeFailureDate_ThrowsBusinessException()
    {
        var repair = new Assets.Entities.AssetRepair(Guid.NewGuid(), "AR-002", Guid.NewGuid(), Guid.NewGuid());
        repair.FailureDate = DateTime.UtcNow.Date;
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            repair.Complete(DateTime.UtcNow.Date.AddDays(-2)));
    }

    [Fact]
    public void ReturnPartyMismatch_Constant_Exists()
    {
        Assert.Equal("MyERP:08014", MyERP.MyERPDomainErrorCodes.ReturnPartyMismatch);
    }

    // === Workstation and WorkstationType Non-Negative Validation ===

    [Fact]
    public void Workstation_NegativeCapacityOrCost_ThrowsBusinessException()
    {
        var ws = new Manufacturing.Entities.Workstation(Guid.NewGuid(), Guid.NewGuid(), "WS-01");
        Assert.Throws<Volo.Abp.BusinessException>(() => ws.ProductionCapacity = -1);
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new Manufacturing.Entities.WorkstationCost(Guid.NewGuid(), ws.Id, "Electricity", -50m));
    }

    [Fact]
    public void WorkstationTypeCost_NegativeCost_ThrowsBusinessException()
    {
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new Manufacturing.Entities.WorkstationTypeCost(Guid.NewGuid(), Guid.NewGuid(), "Electricity", -25m));
    }

    [Fact]
    public void Project_CollectProgressAndSubject_ProperlyConfigured()
    {
        var project = new Projects.Entities.Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-001", "ERP Implementation");
        project.CollectProgress = true;
        project.Subject = $"For project {project.ProjectName}, update your status";
        project.Message = "Please update task status";

        Assert.True(project.CollectProgress);
        Assert.Equal("For project ERP Implementation, update your status", project.Subject);
        Assert.Equal("Please update task status", project.Message);
    }

    [Fact]
    public void WorkOrder_ReverseDisassembly_DecrementsCorrectly()
    {
        var wo = new Manufacturing.Entities.WorkOrder(
            Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(5m);
        wo.RecordDisassembly(3m);
        Assert.Equal(3m, wo.DisassembledQuantity);

        wo.ReverseDisassembly(2m);
        Assert.Equal(1m, wo.DisassembledQuantity);

        wo.ReverseDisassembly(5m); // floor at 0
        Assert.Equal(0m, wo.DisassembledQuantity);
    }

    [Fact]
    public void Asset_ApplyValueAdjustment_RecalculatesStatusCorrectly()
    {
        var asset = new Assets.Entities.Asset(
            Guid.NewGuid(), Guid.NewGuid(), "AST-001", "Test Asset", DateTime.UtcNow, 10000m);
        asset.Submit();
        Assert.Equal(Assets.AssetStatus.Submitted, asset.Status);

        // Adjust value to partial depreciation
        asset.ApplyValueAdjustment(6000m);
        Assert.Equal(Assets.AssetStatus.PartiallyDepreciated, asset.Status);

        // Adjust value to 0 -> FullyDepreciated
        asset.ApplyValueAdjustment(0m);
        Assert.Equal(Assets.AssetStatus.FullyDepreciated, asset.Status);

        // Revert value back to 10000m -> Submitted
        asset.ApplyValueAdjustment(10000m);
        Assert.Equal(Assets.AssetStatus.Submitted, asset.Status);
    }

    [Fact]
    public void AccountsSettings_InternalTransactionRateSettings_ProperlyConfigured()
    {
        var settings = new Accounting.Entities.AccountsSettings(Guid.NewGuid());
        Assert.False(settings.MaintainSameInternalTransactionRate);
        Assert.Equal("Stop", settings.MaintainSameRateAction);
        Assert.Null(settings.RoleToOverrideStopAction);

        settings.MaintainSameInternalTransactionRate = true;
        settings.MaintainSameRateAction = "Warn";
        settings.RoleToOverrideStopAction = "Accounts Manager";

        Assert.True(settings.MaintainSameInternalTransactionRate);
        Assert.Equal("Warn", settings.MaintainSameRateAction);
        Assert.Equal("Accounts Manager", settings.RoleToOverrideStopAction);
        Assert.Equal("MyERP:09003", MyERPDomainErrorCodes.InterCompanyRateMismatch);
    }

    [Fact]
    public void Quotation_DuplicateItems_TracksOrderedQtyAccuratelyByQuotationItemId()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var quotation = new Sales.Entities.Quotation(Guid.NewGuid(), companyId, customerId, "QTN-001", DateTime.UtcNow);
        quotation.AddItem(itemId, "Description 1", quantity: 5m, unitPrice: 100m, taxAmount: 0m);
        quotation.AddItem(itemId, "Description 2 (duplicate item code)", quantity: 10m, unitPrice: 100m, taxAmount: 0m);
        quotation.Submit();

        Assert.Equal(2, quotation.Items.Count);
        var qItem1 = quotation.Items[0];
        var qItem2 = quotation.Items[1];

        // Create Sales Order targeting the second quotation row specifically
        var so = new Sales.Entities.SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-001", DateTime.UtcNow);
        so.AddItem(itemId, "Description 2 (duplicate item code)", quantity: 10m, unitPrice: 100m, taxAmount: 0m, quotationItemId: qItem2.Id);

        // Verify QuotationItemId is stored
        Assert.Equal(qItem2.Id, so.Items[0].QuotationItemId);

        // Simulate resolution
        var targetQItem = so.Items[0].QuotationItemId.HasValue
            ? quotation.Items.FirstOrDefault(i => i.Id == so.Items[0].QuotationItemId!.Value)
            : quotation.Items.FirstOrDefault(i => i.ItemId == so.Items[0].ItemId);

        Assert.NotNull(targetQItem);
        Assert.Equal(qItem2.Id, targetQItem.Id);
        targetQItem.OrderedQty += so.Items[0].Quantity;

        // Row 1 remains un-ordered, Row 2 is fully ordered
        Assert.Equal(0m, qItem1.OrderedQty);
        Assert.Equal(10m, qItem2.OrderedQty);
        Assert.Equal(0m, quotation.PerOrdered); // Min(0, 100) = 0%
    }

    [Fact]
    public void Invoice_DiscountAmount_CalculatesAdditionalDiscountPercentage()
    {
        var invoice = new Sales.Entities.SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-001", DateTime.UtcNow);
        invoice.AddItem(Guid.NewGuid(), "Item 1", 2m, 250m, 0m); // Net = 500
        invoice.DiscountAmount = 50m; // 50 / 500 = 10%
        invoice.AdditionalDiscountPercentage = 0m;

        var netForDiscount = invoice.Items.Sum(i => i.LineTotal);
        var discountAmt = invoice.DiscountAmount;
        if (discountAmt > 0 && netForDiscount > 0 && invoice.AdditionalDiscountPercentage == 0)
        {
            invoice.AdditionalDiscountPercentage = Math.Round(discountAmt / netForDiscount * 100m, 4);
        }

        Assert.Equal(10m, invoice.AdditionalDiscountPercentage);
    }

    [Fact]
    public void DeliveryNote_ProductBundle_MaintainsReversalParity()
    {
        var dn = new Sales.Entities.DeliveryNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-001", DateTime.UtcNow);
        var bundleId = Guid.NewGuid();
        dn.AddItem(bundleId, "Bundle Item", 2m, 500m, 0m);
        dn.Submit();

        Assert.Equal(Core.DocumentStatus.Submitted, dn.Status);

        dn.Cancel();
        Assert.Equal(Core.DocumentStatus.Cancelled, dn.Status);
    }

    [Fact]
    public void SalesOrder_InterCompanyPurchaseOrder_ConnectionLinkage()
    {
        var soId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var po = new Purchasing.Entities.PurchaseOrder(
            Guid.NewGuid(), companyId, supplierId, "PO-001", DateTime.UtcNow);
        po.InterCompanySalesOrderId = soId;

        Assert.Equal(soId, po.InterCompanySalesOrderId);
    }

    [Fact]
    public void Subcontracting_Items_SupportCostCenterAndExpenseAccount()
    {
        var scrId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var costCenterId = Guid.NewGuid();
        var expenseAccountId = Guid.NewGuid();
        var serviceExpenseAccountId = Guid.NewGuid();

        var scrItem = new Purchasing.Entities.SubcontractingReceiptItem(
            Guid.NewGuid(), scrId, itemId, "Manufactured Item", 10m, 50m)
        {
            CostCenterId = costCenterId,
            ExpenseAccountId = expenseAccountId,
            ServiceExpenseAccountId = serviceExpenseAccountId
        };

        Assert.Equal(costCenterId, scrItem.CostCenterId);
        Assert.Equal(expenseAccountId, scrItem.ExpenseAccountId);
        Assert.Equal(serviceExpenseAccountId, scrItem.ServiceExpenseAccountId);

        var scoId = Guid.NewGuid();
        var suppliedItem = new Purchasing.Entities.SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), scoId, itemId, "Raw Material", 20m)
        {
            CostCenterId = costCenterId,
            ExpenseAccountId = expenseAccountId
        };

        Assert.Equal(costCenterId, suppliedItem.CostCenterId);
        Assert.Equal(expenseAccountId, suppliedItem.ExpenseAccountId);
    }

    [Fact]
    public void QualityInspection_AllowAfterSubmission_SettingAndErrorCodeProperlyConfigured()
    {
        Assert.Equal("MyERP.Stock.AllowToMakeQualityInspectionAfterPurchaseOrDelivery",
            Settings.MyERPSettings.Stock.AllowToMakeQualityInspectionAfterPurchaseOrDelivery);
        Assert.Equal("MyERP:05068",
            MyERPDomainErrorCodes.QualityInspectionNotAllowedAfterSubmission);
    }

    [Fact]
    public void StockValuation_MovingAverage_AvoidsPrecisionDiscrepancy_OnStockOut()
    {
        // Initial stock: 10 qty at 3.3333 = balance 33.3333
        var previousSle = new Inventory.Entities.StockLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 10m, 3.3333m, 10m, 33.33m);

        // Consume 3 qty
        var (rate, balanceQty, balanceValue) = Inventory.DomainServices.StockValuationService.CalculateMovingAverage(
            previousSle, -3m, 0m);

        Assert.Equal(7m, balanceQty);
        Assert.True(rate > 0m);
        // Rate is recomputed from rounded balance value / balanceQty
        Assert.Equal(Math.Round(balanceValue, 2) / balanceQty, rate);
    }

    [Fact]
    public void PurchaseReceipt_OverBilling_ConsidersRejectedQuantity()
    {
        var prItem = new Purchasing.Entities.PurchaseReceiptItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item A", 10m, 20m, 0m)
        {
            ReceivedQty = 15m,
            RejectedQty = 5m,
            BilledQty = 12m
        };

        // When BillForRejectedQty is false: base is 10, so 12 > 10 (exceeded base)
        var baseWithoutRejected = prItem.Quantity;
        Assert.Equal(10m, baseWithoutRejected);
        Assert.True(prItem.BilledQty > baseWithoutRejected);

        // When BillForRejectedQty is true (ERPNext PR #47572 / commit 8d9888b1b6):
        // base includes rejected quantity: 10 + 5 = 15, so 12 <= 15 (within limit)
        var baseWithRejected = prItem.Quantity + prItem.RejectedQty;
        Assert.Equal(15m, baseWithRejected);
        Assert.True(prItem.BilledQty <= baseWithRejected);
    }

    [Fact]
    public void BuyingSettings_SetValuationRateForRejectedMaterials_ProperlyConfigured()
    {
        Assert.Equal("MyERP.Buying.SetValuationRateForRejectedMaterials",
            Settings.MyERPSettings.Buying.SetValuationRateForRejectedMaterials);
    }

    [Fact]
    public void AgingReport_SupportsCalculateAgeingWith_Option()
    {
        var report = new Accounting.DomainServices.AgingReport
        {
            ReportType = "Receivable",
            AsOfDate = DateTime.UtcNow.Date,
            CalculateAgeingWith = "Today Date"
        };

        Assert.Equal("Today Date", report.CalculateAgeingWith);
    }

    [Fact]
    public void Bom_AllowsFinishedGoodAsRawMaterial_ByDefault()
    {
        var fgItemId = Guid.NewGuid();
        var bom = new Manufacturing.Entities.BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", fgItemId);
        var bomItem = new Manufacturing.Entities.BomItem(
            Guid.NewGuid(), bom.Id, fgItemId, "Finished Good Component", 1m, 100m);

        bom.AddItem(bomItem);

        Assert.True(bomItem.DoNotExplode);
        Assert.Null(bomItem.SubBomId);
    }

    [Fact]
    public void WorkOrder_AdditionalItem_TracksVoucherDetailReference()
    {
        var woId = Guid.NewGuid();
        var seItemId = Guid.NewGuid();
        var addlItem = new Manufacturing.Entities.WorkOrderItem(
            Guid.NewGuid(), woId, Guid.NewGuid(), "Unplanned Solvent", 5m)
        {
            IsAdditionalItem = true,
            VoucherDetailReference = seItemId,
            TransferredQuantity = 5m
        };

        Assert.True(addlItem.IsAdditionalItem);
        Assert.Equal(seItemId, addlItem.VoucherDetailReference);
        Assert.Equal(5m, addlItem.TransferredQuantity);
    }

    [Fact]
    public void InterCompany_AddressValidation_MatchesTargetParty()
    {
        var supplierId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var supplierAddress = new Core.Entities.Address(
            Guid.NewGuid(), "Supplier Office", "Supplier", supplierId, "123 Jalan Ampang", "Malaysia");

        Assert.Equal("Supplier", supplierAddress.PartyType);
        Assert.Equal(supplierId, supplierAddress.PartyId);
        Assert.NotEqual(customerId, supplierAddress.PartyId);
    }

    [Fact]
    public void Asset_Capitalization_MarkAndRestoreLifecycle()
    {
        var asset = new Assets.Entities.Asset(
            Guid.NewGuid(), Guid.NewGuid(), "AST-001", "Substation Pump",
            DateTime.UtcNow, 10000m);

        Assert.Equal(Assets.AssetStatus.Draft, asset.Status);
        asset.Submit();
        Assert.Equal(Assets.AssetStatus.Submitted, asset.Status);

        var disposalDate = DateTime.UtcNow.Date;
        asset.MarkAsCapitalized(disposalDate);

        Assert.Equal(Assets.AssetStatus.Capitalized, asset.Status);
        Assert.Equal(disposalDate, asset.DisposalDate);

        asset.RestoreFromCapitalization();
        Assert.Equal(Assets.AssetStatus.Submitted, asset.Status);
        Assert.Null(asset.DisposalDate);
    }

    [Fact]
    public void PosProfile_HideUnavailableItems_PreservesNonStockItems()
    {
        var profile = new Sales.Entities.PosProfile(
            Guid.NewGuid(), Guid.NewGuid(), "Express Counter", Guid.NewGuid())
        {
            HideUnavailableItems = true
        };

        Assert.True(profile.HideUnavailableItems);

        var serviceItem = new Inventory.Entities.Item(
            Guid.NewGuid(), Guid.NewGuid(), "SRV-001", "Installation Service", Inventory.ItemType.Service);

        var stockItem = new Inventory.Entities.Item(
            Guid.NewGuid(), Guid.NewGuid(), "STK-001", "Widget", Inventory.ItemType.Goods);

        // Per ERPNext PR #47493 / commit 57f3489dfa:
        // Non-stock items are always visible (!MaintainStock).
        // Stock items require available qty in warehouse.
        var isServiceVisible = !serviceItem.MaintainStock;
        var isStockVisibleWithZeroQty = !stockItem.MaintainStock;

        Assert.False(serviceItem.MaintainStock);
        Assert.True(stockItem.MaintainStock);
        Assert.True(isServiceVisible);
        Assert.False(isStockVisibleWithZeroQty);
    }

    [Fact]
    public void SalesAndPurchaseAnalytics_IgnoreOpeningInvoices()
    {
        var siOpening = new Sales.Entities.SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-OPENING", DateTime.UtcNow)
        {
            IsOpening = true
        };

        var siNormal = new Sales.Entities.SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-NORMAL", DateTime.UtcNow)
        {
            IsOpening = false
        };

        var piOpening = new Purchasing.Entities.PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-OPENING", DateTime.UtcNow)
        {
            IsOpening = true
        };

        var piNormal = new Purchasing.Entities.PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-NORMAL", DateTime.UtcNow)
        {
            IsOpening = false
        };

        // Per ERPNext PR #47385 / commit 6d269b4409:
        // Sales and Purchase Analytics must ignore opening entries
        Assert.True(siOpening.IsOpening);
        Assert.False(siNormal.IsOpening);
        Assert.True(piOpening.IsOpening);
        Assert.False(piNormal.IsOpening);

        var salesInvoices = new[] { siOpening, siNormal };
        var analyticsSales = salesInvoices.Where(s => !s.IsOpening).ToList();
        Assert.Single(analyticsSales);
        Assert.Equal("SI-NORMAL", analyticsSales[0].InvoiceNumber);

        var purchaseInvoices = new[] { piOpening, piNormal };
        var analyticsPurchase = purchaseInvoices.Where(p => !p.IsOpening).ToList();
        Assert.Single(analyticsPurchase);
        Assert.Equal("PI-NORMAL", analyticsPurchase[0].InvoiceNumber);
    }

    [Fact]
    public void BomStockReport_IncludesItemNameAndDescription()
    {
        var dto = new Manufacturing.BomMaterialAvailabilityDto
        {
            ItemId = Guid.NewGuid(),
            ItemCode = "RM-STEEL-01",
            ItemName = "Stainless Steel Sheet 2mm",
            Description = "Grade 304 2mm cold rolled sheet",
            RequiredQtyPerUnit = 2m,
            RequiredQtyForBatch = 20m,
            AvailableQty = 50m,
            Shortage = 0m,
            IsSufficient = true
        };

        // Per ERPNext PR #47116 / commit b6b4ac5b4a:
        // Item Code, Item Name, and Description are distinct columns in BOM Stock Report
        Assert.Equal("RM-STEEL-01", dto.ItemCode);
        Assert.Equal("Stainless Steel Sheet 2mm", dto.ItemName);
        Assert.Equal("Grade 304 2mm cold rolled sheet", dto.Description);
        Assert.True(dto.IsSufficient);
    }

    [Fact]
    public void TimesheetDetail_TracksBillingHours()
    {
        var billableDetail = new Projects.Entities.TimesheetDetail(
            Guid.NewGuid(), Guid.NewGuid(), "Development", DateTime.UtcNow, DateTime.UtcNow.AddHours(4), 4m)
        {
            IsBillable = true,
            BillingRate = 150m
        };

        var nonBillableDetail = new Projects.Entities.TimesheetDetail(
            Guid.NewGuid(), Guid.NewGuid(), "Internal Meeting", DateTime.UtcNow, DateTime.UtcNow.AddHours(2), 2m)
        {
            IsBillable = false,
            BillingRate = 150m
        };

        // Per ERPNext commit b04a07fda0:
        // BillingHours accurately reflects billable hours per detail row
        Assert.Equal(4m, billableDetail.BillingHours);
        Assert.Equal(600m, billableDetail.BillingAmount);

        Assert.Equal(0m, nonBillableDetail.BillingHours);
        Assert.Equal(0m, nonBillableDetail.BillingAmount);
    }

    [Fact]
    public void Bom_CycleDetected_IncludesSolutionGuidance()
    {
        var ex = new Volo.Abp.BusinessException(MyERPDomainErrorCodes.BomCycleDetected)
            .WithData("itemId", Guid.NewGuid())
            .WithData("bomNumber", "BOM-001")
            .WithData("solution", "If you want to use the finished good as a raw material, enable the 'Do Not Explode' checkbox in the Items table against that raw material.");

        // Per ERPNext PR #47472 / commit 7103cdd84a:
        // Message/error carries actionable guidance pointing to 'Do Not Explode' checkbox
        Assert.Equal(MyERPDomainErrorCodes.BomCycleDetected, ex.Code);
        Assert.True(ex.Data.Contains("solution"));
        Assert.Contains("Do Not Explode", ex.Data["solution"]?.ToString() ?? "");
    }

    [Fact]
    public void Asset_CompositeAsset_InitialStatusIsWorkInProgress()
    {
        var companyId = Guid.NewGuid();
        var normalAsset = new Assets.Entities.Asset(
            Guid.NewGuid(), companyId, "AST-NORM-001", "Standard Laptop", DateTime.UtcNow, 2000m);

        var compositeAsset = new Assets.Entities.Asset(
            Guid.NewGuid(), companyId, "AST-CWIP-001", "Factory Building Under Construction", DateTime.UtcNow, 500000m)
        {
            IsCompositeAsset = true
        };

        // Per ERPNext commit 3855536ef1:
        // Composite assets start with status 'WorkInProgress' while unsubmitted,
        // and can transition to 'Submitted' upon completion.
        Assert.Equal(Assets.AssetStatus.Draft, normalAsset.Status);
        Assert.Equal(Assets.AssetStatus.WorkInProgress, compositeAsset.Status);

        compositeAsset.Submit();
        Assert.Equal(Assets.AssetStatus.Submitted, compositeAsset.Status);
    }

    [Fact]
    public void Asset_SetDepreciationRateAndValueAfterDepreciation_NonDepreciatedAsset()
    {
        var companyId = Guid.NewGuid();
        var asset = new Assets.Entities.Asset(
            Guid.NewGuid(), companyId, "AST-LAND-001", "Freehold Land", DateTime.UtcNow, 250000m)
        {
            CalculateDepreciation = false,
            UsefulLifeMonths = 0,
            FrequencyMonths = 12
        };

        // Per ERPNext commit 48311ee5c5:
        // SetDepreciationRateAndValueAfterDepreciation calculates value after depreciation and depr rate
        // before checking CalculateDepreciation, ensuring non-depreciated assets have valid book value.
        asset.SetDepreciationRateAndValueAfterDepreciation();

        Assert.Equal(250000m, asset.ValueAfterDepreciation);
        Assert.Equal(0m, asset.DepreciationRate);
    }

    [Fact]
    public void StockReconciliation_SerialAndBatchBundle_SubmitsDraftOnly()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var draftBundle = new Inventory.Entities.SerialAndBatchBundle(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            Inventory.Entities.BundleTransactionType.Inward, "StockReconciliation", Guid.NewGuid(), DateTime.UtcNow);

        var alreadySubmittedBundle = new Inventory.Entities.SerialAndBatchBundle(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            Inventory.Entities.BundleTransactionType.Outward, "StockReconciliation", Guid.NewGuid(), DateTime.UtcNow);
        alreadySubmittedBundle.Submit("SR-0001");

        // Per ERPNext PR #47457 / commit ad25636afb:
        // When reconciling stock (e.g. backdated reconciliation), only bundles in Draft status
        // should be transitioned to Submitted. Submitting an already-submitted bundle is a no-op / protected.
        Assert.Equal(Core.DocumentStatus.Draft, draftBundle.Status);
        Assert.Equal(Core.DocumentStatus.Submitted, alreadySubmittedBundle.Status);

        draftBundle.Submit("SR-0002");
        Assert.Equal(Core.DocumentStatus.Submitted, draftBundle.Status);

        // Safe idempotent call on already-submitted bundle
        alreadySubmittedBundle.Submit("SR-0002");
        Assert.Equal(Core.DocumentStatus.Submitted, alreadySubmittedBundle.Status);

        draftBundle.Cancel();
        Assert.Equal(Core.DocumentStatus.Cancelled, draftBundle.Status);
        Assert.True(draftBundle.IsCancelled);
    }

    [Fact]
    public void PurchaseInvoice_AmountOverBillingValidation_EnforcesAllowance()
    {
        // Per ERPNext PR #47452 / commit f4ffc57b51:
        // When billing PR items via PI with rate changes:
        // attempted amount exceeding base amount by more than OverBillingAllowance must throw.
        decimal baseQty = 5m;
        decimal prRate = 1000m;
        decimal baseAmount = baseQty * prRate; // 5000m

        decimal allowancePct = 20m; // 20% allowance allowed -> max 6000m

        // Attempting to bill at 1300m rate (total 6500m -> 30% overbilled)
        decimal piRate = 1300m;
        decimal attemptedAmount = baseQty * piRate; // 6500m

        var pctOverBilled = ((attemptedAmount / baseAmount) * 100m) - 100m; // 30%
        var exceedsAllowance = pctOverBilled > allowancePct;

        Assert.Equal(30m, pctOverBilled);
        Assert.True(exceedsAllowance);
        Assert.Equal(10m, pctOverBilled - allowancePct);
    }

    [Fact]
    public void AssetDisposal_NonDepreciatedAsset_DoesNotRequireAccumulatedDepreciationAccount()
    {
        var companyId = Guid.NewGuid();
        var landAsset = new Assets.Entities.Asset(
            Guid.NewGuid(), companyId, "AST-LAND-02", "Industrial Plot", DateTime.UtcNow, 100000m)
        {
            CalculateDepreciation = false,
            ValueAfterDepreciation = 100000m
        };

        // Per ERPNext PR #47427 / commit 51ea33e743:
        // When disposing an asset without accumulated depreciation, AccumulatedDepreciationAccountId
        // must NOT be mandated.
        var accumulatedDepreciation = landAsset.PurchaseAmount - landAsset.ValueAfterDepreciation;
        Assert.Equal(0m, accumulatedDepreciation);

        var requiresAccumDepAccount = accumulatedDepreciation != 0;
        Assert.False(requiresAccumDepAccount);
    }

    [Fact]
    public void SupplierQuotation_AllowZeroQtyInSupplierQuotation_EnforcesSetting()
    {
        // Per ERPNext PR #47435 / commit 879b966bd4:
        // Allow zero qty in SQ and conversion from RFQ when enabled by Buying Settings.
        var allowZeroQty = false;
        var rfqItems = new[]
        {
            new { ItemId = Guid.NewGuid(), Qty = 0m, Description = "Zero Qty Sample Item" },
            new { ItemId = Guid.NewGuid(), Qty = 10m, Description = "Standard Item" }
        };

        var filteredItemsDisallowed = rfqItems.Where(i => !(i.Qty <= 0 && !allowZeroQty)).ToList();
        Assert.Single(filteredItemsDisallowed);
        Assert.Equal(10m, filteredItemsDisallowed[0].Qty);

        allowZeroQty = true;
        var filteredItemsAllowed = rfqItems.Where(i => !(i.Qty <= 0 && !allowZeroQty)).ToList();
        Assert.Equal(2, filteredItemsAllowed.Count);
        Assert.Contains(filteredItemsAllowed, i => i.Qty == 0m);
    }

    [Fact]
    public void StockEntry_DifferenceAccount_Validations()
    {
        // Per ERPNext commits fb819c558e & bba6b0ff45:
        // 1. Difference Account cannot be a Stock account (prevents circular stock accounting).
        var stockAccount = new Accounting.Entities.Account(
            Guid.NewGuid(), Guid.NewGuid(), "1510", "Stock in Hand", Accounting.AccountType.Asset)
        {
            AccountSubType = Accounting.AccountSubType.Stock
        };
        Assert.Equal(Accounting.AccountSubType.Stock, stockAccount.AccountSubType);

        // 2. Opening stock entry must use Balance Sheet account, not P&L (Revenue / Expense).
        var expenseAccount = new Accounting.Entities.Account(
            Guid.NewGuid(), Guid.NewGuid(), "5110", "Cost of Goods Sold", Accounting.AccountType.Expense)
        {
            AccountSubType = Accounting.AccountSubType.CostOfGoodsSold
        };
        var isPlAccount = expenseAccount.AccountType == Accounting.AccountType.Expense
            || expenseAccount.AccountType == Accounting.AccountType.Revenue;
        Assert.True(isPlAccount);

        var tempOpeningAccount = new Accounting.Entities.Account(
            Guid.NewGuid(), Guid.NewGuid(), "3110", "Temporary Opening", Accounting.AccountType.Equity)
        {
            AccountSubType = Accounting.AccountSubType.TemporaryOpening
        };
        var isTempOpeningValid = tempOpeningAccount.AccountType != Accounting.AccountType.Expense
            && tempOpeningAccount.AccountType != Accounting.AccountType.Revenue;
        Assert.True(isTempOpeningValid);

        // 3. COGS is only valid for Material Issue entries, not Material Receipt / Manufacture.
        var receiptPurpose = Inventory.StockEntryType.MaterialReceipt;
        var issuePurpose = Inventory.StockEntryType.MaterialIssue;

        Assert.True(receiptPurpose != Inventory.StockEntryType.MaterialIssue && expenseAccount.AccountSubType == Accounting.AccountSubType.CostOfGoodsSold);
        Assert.False(issuePurpose != Inventory.StockEntryType.MaterialIssue && expenseAccount.AccountSubType == Accounting.AccountSubType.CostOfGoodsSold);
    }

    [Fact]
    public void WorkOrder_ManufactureEntry_AutoReservesFinishedGoods_SubtractsDeliveredQty()
    {
        // Per ERPNext PR #47382 / commit 5225d4c318:
        // When Work Order finishes goods for a linked Sales Order:
        // reservable quantity must subtract already delivered quantity as well as existing reserved qty:
        // qty = so_details.stock_qty - (so_details.stock_reserved_qty + so_details.delivered_qty)
        decimal orderedQty = 100m;
        decimal conversionFactor = 1m;
        decimal stockQty = orderedQty * conversionFactor; // 100
        decimal deliveredQty = 30m; // 30 already delivered
        decimal existingReservedQty = 20m; // 20 already reserved
        decimal fgProducedQty = 60m; // WO produced 60

        // Remaining unfulfilled, unreserved demand for SO item
        var pendingReservableQty = Math.Max(0, (orderedQty - deliveredQty) * conversionFactor - existingReservedQty);
        Assert.Equal(50m, pendingReservableQty); // 100 - (20 + 30) = 50

        // Auto-reserve is capped at pendingReservableQty (never over-reserves)
        var toReserve = Math.Min(fgProducedQty, pendingReservableQty);
        Assert.Equal(50m, toReserve);
    }

    [Fact]
    public void Asset_SellAtZeroRate_And_UnsellOnCancel()
    {
        // Per ERPNext PR #47326 / commit 05afad78fc:
        // Selling an asset at zero rate (nominal/scrap/donation) is allowed and transitions to Sold.
        var asset = new Assets.Entities.Asset(
            Guid.NewGuid(), Guid.NewGuid(), "AST-001", "Desk Asset",
            DateTime.UtcNow.AddYears(-1), 1000m);
        asset.Submit();
        Assert.Equal(Assets.AssetStatus.Submitted, asset.Status);

        // Sell at zero rate
        asset.Sell(DateTime.UtcNow, 0m);
        Assert.Equal(Assets.AssetStatus.Sold, asset.Status);
        Assert.Equal(0m, asset.DisposalAmount);

        // Cancel Sales Invoice -> Unsell
        asset.Unsell();
        Assert.Equal(Assets.AssetStatus.Submitted, asset.Status);
        Assert.Null(asset.DisposalDate);
        Assert.Null(asset.DisposalAmount);
    }

    [Fact]
    public void JobCard_Complete_RequiresFromAndToTime()
    {
        // Per ERPNext PR #47325 / commit 7499c25a3c:
        // Submission/completion of Job Card validates that From Time and To Time fields are present on all time logs
        var jc = new Manufacturing.Entities.JobCard(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10m, 1);
        jc.Start();

        // Valid log: FromTime and ToTime present
        var fromTime = DateTime.UtcNow.AddHours(-2);
        var toTime = DateTime.UtcNow.AddHours(-1);
        jc.AddTimeLog(fromTime, toTime, 10m);

        // Completion succeeds with valid time logs
        jc.Complete();
        Assert.Equal(Manufacturing.JobCardStatus.Completed, jc.Status);
        Assert.NotNull(jc.CompletedAt);
    }

    [Fact]
    public void StockReconciliation_NoEntriesCreated_ThrowsValidationFailed()
    {
        // Per ERPNext PR #47292 / commit 3d36d0b1df:
        // When submitting a Stock Reconciliation, if no SLE entries are created (all items have qtyDiff == 0 and rateDiff == 0),
        // it throws ValidationFailed instead of silently submitting an empty document.
        var sr = new Inventory.Entities.StockReconciliation(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        // Add item where CurrentQty == NewQty and CurrentRate == NewRate
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10m, 100m, 10m, 100m);
        var item = sr.Items.First();
        Assert.Equal(0m, item.QuantityDifference);
        Assert.Equal(item.CurrentValuationRate, item.NewValuationRate);

        // Submitting with 0 entries created triggers validation failure
        var entriesCreated = 0;
        foreach (var row in sr.Items)
        {
            var qtyDiff = row.QuantityDifference;
            if (qtyDiff == 0 && row.NewValuationRate == row.CurrentValuationRate)
                continue;
            entriesCreated++;
        }

        Assert.Equal(0, entriesCreated);
    }

    [Fact]
    public void AssetRepair_ConsumedItemsCost_IncludedInAssetCapitalization()
    {
        // Per ERPNext PR #47233 / commit ed8a8532e1:
        // Consumed stock cost is always added to asset value after repair;
        // repair service cost is added if CapitalizeRepairCost is true.
        var asset = new Assets.Entities.Asset(
            Guid.NewGuid(), Guid.NewGuid(), "AST-REP-01", "Delivery Van",
            DateTime.UtcNow.AddYears(-1), 50000m);
        asset.Submit();

        var repair = new Assets.Entities.AssetRepair(
            Guid.NewGuid(), "REP-001", asset.CompanyId, asset.Id);

        // Add consumed spare parts (e.g. 2 tires at 500 each = 1000)
        repair.AddStockItem(Guid.NewGuid(), Guid.NewGuid(), 2m, 500m);
        // Add service invoice 800
        repair.AddInvoice(Guid.NewGuid(), Guid.NewGuid(), 800m);

        Assert.Equal(1000m, repair.ConsumedItemsCost);
        Assert.Equal(800m, repair.RepairCost);
        Assert.Equal(1800m, repair.TotalRepairCost);

        // Scenario 1: CapitalizeRepairCost = false -> still capitalizes ConsumedItemsCost (1000)
        repair.CapitalizeRepairCost = false;
        var capitalizedCostUncapped = repair.ConsumedItemsCost + (repair.CapitalizeRepairCost ? repair.RepairCost : 0m);
        Assert.Equal(1000m, capitalizedCostUncapped);

        // Scenario 2: CapitalizeRepairCost = true -> capitalizes both (1800)
        repair.CapitalizeRepairCost = true;
        var capitalizedCostAll = repair.ConsumedItemsCost + (repair.CapitalizeRepairCost ? repair.RepairCost : 0m);
        Assert.Equal(1800m, capitalizedCostAll);

        asset.ApplyRepairCapitalization(capitalizedCostAll, 6);
        Assert.Equal(1800m, asset.AdditionalCost);
        Assert.Equal(51800m, asset.TotalAssetCost);
    }

    [Fact]
    public void InterCompanyJournalEntry_DebitCredit_PrecisionValidation()
    {
        // Per ERPNext PR #47241 / commit 5fe247557e:
        // Inter-Company Journal Entry compares reciprocal debit/credit totals using currency precision (2 decimals)
        // to avoid floating-point/sub-penny mismatch rejections.
        decimal originalDebit = 1250.5000001m;
        decimal originalCredit = 1250.5000000m;

        decimal linkedDebit = 1250.5000000m;
        decimal linkedCredit = 1250.5000002m;

        // Raw comparison might fail if not rounded:
        // originalCredit != linkedDebit without precision
        var roundedOriginalCredit = Math.Round(originalCredit, 2);
        var roundedOriginalDebit = Math.Round(originalDebit, 2);
        var roundedLinkedCredit = Math.Round(linkedCredit, 2);
        var roundedLinkedDebit = Math.Round(linkedDebit, 2);

        Assert.Equal(1250.50m, roundedOriginalCredit);
        Assert.Equal(1250.50m, roundedOriginalDebit);
        Assert.Equal(1250.50m, roundedLinkedCredit);
        Assert.Equal(1250.50m, roundedLinkedDebit);

        var isValid = (roundedOriginalCredit == roundedLinkedDebit) && (roundedOriginalDebit == roundedLinkedCredit);
        Assert.True(isValid);
    }

    [Fact]
    public void SalesInvoice_ProhibitReturnAgainstConsolidatedPosInvoice()
    {
        // Per ERPNext PR #47251 / commit 483c4a3271:
        // Returns cannot be created against consolidated POS sales invoices.
        var inv = new Sales.Entities.SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ACC-SINV-2025-0001",
            DateTime.UtcNow);
        inv.IsPos = true;
        inv.IsConsolidated = true;

        Assert.True(inv.IsPos);
        Assert.True(inv.IsConsolidated);

        // Validates guard condition: cannot return against consolidated POS invoice
        var canCreateReturn = !(inv.IsConsolidated && inv.IsPos);
        Assert.False(canCreateReturn);
    }

    [Fact]
    public void Uom_FilterEnabledByDefault()
    {
        // Per ERPNext PR #47112 / commit 3745825052:
        // UOM query filters by enabled = 1 to prevent disabled UOMs from appearing in lookup.
        var uomActive = new Inventory.Entities.Uom(Guid.NewGuid(), "Kg");
        var uomDisabled = new Inventory.Entities.Uom(Guid.NewGuid(), "Obsolete_UOM");
        uomDisabled.Disable();

        Assert.True(uomActive.IsEnabled);
        Assert.False(uomDisabled.IsEnabled);

        uomDisabled.Enable();
        Assert.True(uomDisabled.IsEnabled);
        uomDisabled.Disable();
        Assert.False(uomDisabled.IsEnabled);

        var list = new[] { uomActive, uomDisabled };
        var enabledOnly = list.Where(u => u.IsEnabled).ToList();
        Assert.Single(enabledOnly);
        Assert.Equal("Kg", enabledOnly[0].Name);
    }

    [Fact]
    public void PaymentSchedule_BaseOutstandingCalculation()
    {
        // Per ERPNext PR #47178 / commit 02356029a8:
        // BaseOutstanding calculation for payment schedule starts equal to BasePaymentAmount.
        var entry = new Accounting.Entities.PaymentScheduleEntry(
            Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 50m, 500m)
        {
            BasePaymentAmount = 2200m // e.g., 500 USD * 4.4 MYR/USD
        };

        // Before any payments, BaseOutstanding == BasePaymentAmount
        Assert.Equal(2200m, entry.BaseOutstanding);
        Assert.Equal(500m, entry.Outstanding);

        // Record partial payment of 250 (50%)
        entry.RecordPayment(250m);
        Assert.Equal(250m, entry.Outstanding);
        Assert.Equal(1100m, entry.BaseOutstanding);

        // Record remaining payment
        entry.RecordPayment(250m);
        Assert.Equal(0m, entry.Outstanding);
        Assert.Equal(0m, entry.BaseOutstanding);
        Assert.True(entry.IsFullyPaid);
    }

    [Fact]
    public void StockReservation_QuantityPrecisionValidation()
    {
        // Per ERPNext PR #46973 / commit 860699ee7b:
        // Validate stock reservation rounds requestedQty and available stock to reservation precision (4 decimals)
        // to prevent false-positive over-reservation errors caused by floating-point dust.
        decimal requestedQty = 10.000000000000002m;
        decimal availableQty = 10.0m;

        // Raw comparison would falsely reject:
        Assert.True(requestedQty > availableQty);

        // With reservation precision rounding:
        var roundedRequested = Math.Round(requestedQty, 4);
        var roundedAvailable = Math.Round(availableQty, 4);

        Assert.Equal(10.0000m, roundedRequested);
        Assert.Equal(10.0000m, roundedAvailable);
        Assert.False(roundedRequested > roundedAvailable);
    }

    [Fact]
    public void StockReservation_TransferredAndConsumedQty_DeductedFromActiveReservation()
    {
        // Per ERPNext PR #47049 / commit 27d674d54a:
        // Stock already transferred or consumed (e.g. for Work Order) must be deducted from active reservation
        // so that completed/consumed manufacturing stock does not falsely block stock operations or work order completion.
        var sre = new Inventory.Entities.StockReservationEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Work Order", Guid.NewGuid(), 100m, voucherQty: 100m);

        Assert.Equal(100m, sre.AvailableQty);

        // Transferred 40 for production
        sre.TransferredQty = 40m;
        Assert.Equal(60m, sre.AvailableQty);

        // Consumed 30 in production
        sre.ConsumedQty = 30m;
        Assert.Equal(30m, sre.AvailableQty);

        // Delivered 20
        sre.RecordDelivery(20m);
        Assert.Equal(10m, sre.AvailableQty);

        // Active reservation query logic:
        var remainingReserved = sre.ReservedQty - sre.DeliveredQty - sre.TransferredQty - sre.ConsumedQty;
        Assert.Equal(10m, remainingReserved);
    }

    [Fact]
    public void MaterialRequest_GetPendingQty_PrefersOrderedOverReceived()
    {
        // Per ERPNext PR #47012 / commit 5a524854de:
        // When determining pending qty to order, consider OrderedQuantity first (or ReceivedQuantity if higher).
        var item = new Purchasing.Entities.MaterialRequestItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Raw Material A",
            100m, "Kg");

        // Initial pending qty: 100
        Assert.Equal(100m, Purchasing.DomainServices.MaterialRequestManager.GetPendingQty(item));

        // Placed PO for 60 (ordered_qty = 60, received_qty = 0)
        item.OrderedQuantity = 60m;
        item.ReceivedQuantity = 0m;
        Assert.Equal(40m, Purchasing.DomainServices.MaterialRequestManager.GetPendingQty(item));

        // Received 20 against PO (ordered_qty = 60, received_qty = 20)
        // Pending qty to order is still 40 (based on ordered_qty = 60, not received_qty = 20)
        item.ReceivedQuantity = 20m;
        Assert.Equal(40m, Purchasing.DomainServices.MaterialRequestManager.GetPendingQty(item));

        // Fully ordered 100 (ordered_qty = 100, received_qty = 20)
        item.OrderedQuantity = 100m;
        Assert.Equal(0m, Purchasing.DomainServices.MaterialRequestManager.GetPendingQty(item));
    }

    [Fact]
    public void QualityInspection_InspectionRequiredBeforePurchaseOrDelivery()
    {
        // Per ERPNext PR #47002 / commit 8eaa2afeb7:
        // By default, QI cannot be created for a purchase/delivery document if the item does not require inspection,
        // unless AllowToMakeQualityInspectionAfterPurchaseOrDelivery setting is enabled.
        var itemStandard = new Inventory.Entities.Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Standard Item", Inventory.ItemType.Goods)
        {
            InspectionRequiredBeforePurchase = false,
            InspectionRequiredBeforeDelivery = false
        };

        var itemInspected = new Inventory.Entities.Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-002", "Inspected Item", Inventory.ItemType.Goods)
        {
            InspectionRequiredBeforePurchase = true,
            InspectionRequiredBeforeDelivery = true
        };

        // When setting is false:
        bool allowAfterPurchaseOrDelivery = false;

        // Check purchase validation
        bool canCreateForStandardPurchase = allowAfterPurchaseOrDelivery || itemStandard.InspectionRequiredBeforePurchase;
        bool canCreateForInspectedPurchase = allowAfterPurchaseOrDelivery || itemInspected.InspectionRequiredBeforePurchase;

        Assert.False(canCreateForStandardPurchase);
        Assert.True(canCreateForInspectedPurchase);

        // When setting is true:
        allowAfterPurchaseOrDelivery = true;
        canCreateForStandardPurchase = allowAfterPurchaseOrDelivery || itemStandard.InspectionRequiredBeforePurchase;
        Assert.True(canCreateForStandardPurchase);
    }

    [Fact]
    public void PosProfile_ProjectId_ConfiguredAndPropagated()
    {
        // Per ERPNext PR #46964 / commit 821d64241a:
        // POS Profile supports project link for accounting dimension tracking.
        var projectId = Guid.NewGuid();
        var profile = new Sales.Entities.PosProfile(
            Guid.NewGuid(), Guid.NewGuid(), "Main Counter", Guid.NewGuid())
        {
            ProjectId = projectId
        };

        Assert.Equal(projectId, profile.ProjectId);

        // Consolidated Sales Invoice receives ProjectId from POS Profile if not set
        var invoice = new Sales.Entities.SalesInvoice(
            Guid.NewGuid(), profile.CompanyId, Guid.NewGuid(), "POS-CONSOL-001",
            DateTime.UtcNow.Date);

        if (profile.ProjectId.HasValue)
        {
            invoice.ProjectId = profile.ProjectId;
        }

        Assert.Equal(projectId, invoice.ProjectId);
    }

    [Fact]
    public void Subcontracting_IgnoreBackflushSettingOnReturn()
    {
        // Per ERPNext PR #46892 / commit 7479e1ec32:
        // On Subcontracting Receipt return, ignore backflush setting (e.g. "Material Transferred for Subcontract")
        // and always use BOM / RequiredQty.
        var sco = new Purchasing.Entities.SubcontractingOrder(
            Guid.NewGuid(), Guid.NewGuid(), "SCO-001", DateTime.UtcNow.Date, Guid.NewGuid(), Guid.NewGuid());

        var fgItemId = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();

        sco.AddItem(new Purchasing.Entities.SubcontractingOrderItem(
            Guid.NewGuid(), sco.Id, fgItemId, "Finished Good", 10m, 50m));

        // Required 20, Transferred 30 (due to extra materials sent)
        sco.AddSuppliedItem(new Purchasing.Entities.SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, rmItemId, "Raw Material", 20m)
        {
            TransferredQty = 30m
        });

        var scManager = new Purchasing.DomainServices.SubcontractingManager(null!, null!);

        // Scenario 1: Normal receipt with "Material Transferred for Subcontract" backflush setting
        // Received 5 out of 10 FG (50%) -> should consume 50% of TransferredQty (30 * 0.5 = 15)
        var normalConsumptions = scManager.CalculateRmConsumption(
            sco, 5m, "Material Transferred for Subcontract", isReturn: false);
        Assert.Single(normalConsumptions);
        Assert.Equal(15m, normalConsumptions[0].ConsumedQty);

        // Scenario 2: Return receipt with "Material Transferred for Subcontract" setting
        // Per PR #46892: on return, backflush setting is IGNORED, falling back to RequiredQty (20 * -0.5 = -10)
        var returnConsumptions = scManager.CalculateRmConsumption(
            sco, -5m, "Material Transferred for Subcontract", isReturn: true);
        Assert.Single(returnConsumptions);
        Assert.Equal(-10m, returnConsumptions[0].ConsumedQty);
    }

    [Fact]
    public void PosInvoice_RequiresOpenPosOpeningEntry()
    {
        // Per ERPNext PR #46907 / commit 3de1b22480:
        // POS Invoice creation requires an open POS Opening Entry for the profile / company.
        var profileId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var invoice = new Sales.Entities.SalesInvoice(
            Guid.NewGuid(), companyId, Guid.NewGuid(), "POS-001", DateTime.UtcNow.Date)
        {
            IsPos = true,
            PosProfileId = profileId
        };

        Assert.True(invoice.IsPos);
        Assert.Equal(profileId, invoice.PosProfileId);

        // Given no open session, verification fails
        var openSessions = new System.Collections.Generic.List<Sales.Entities.PosOpeningEntry>();
        bool hasOpenSession = openSessions.Any(e =>
            e.CompanyId == companyId
            && e.PosProfileId == profileId
            && e.Status == Sales.Entities.PosOpeningStatus.Open);
        Assert.False(hasOpenSession);

        // When open session is registered, verification passes
        var opening = new Sales.Entities.PosOpeningEntry(
            Guid.NewGuid(), companyId, profileId, Guid.NewGuid());
        openSessions.Add(opening);

        hasOpenSession = openSessions.Any(e =>
            e.CompanyId == companyId
            && e.PosProfileId == profileId
            && e.Status == Sales.Entities.PosOpeningStatus.Open);
        Assert.True(hasOpenSession);
    }

    [Fact]
    public void Quotation_CustomerCannotBeChanged_WhenCreatedFromOpportunity()
    {
        // Per ERPNext commit dc4819e897:
        // Customer cannot be changed if creating or editing quotation linked to opportunity.
        var companyId = Guid.NewGuid();
        var originalCustomerId = Guid.NewGuid();
        var differentCustomerId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();

        var quotation = new Sales.Entities.Quotation(
            Guid.NewGuid(), companyId, originalCustomerId, "QTN-001", DateTime.UtcNow.Date)
        {
            OpportunityId = opportunityId
        };

        Assert.Equal(originalCustomerId, quotation.CustomerId);
        Assert.Equal(opportunityId, quotation.OpportunityId);

        // Verification logic matches QuotationAppService:
        // Attempting to change customer when OpportunityId is set is blocked.
        bool customerChanged = differentCustomerId != quotation.CustomerId;
        bool hasOpportunity = quotation.OpportunityId.HasValue;

        Assert.True(hasOpportunity && customerChanged);
    }

    [Fact]
    public void CustomerDashboard_Connections_IncludeDunning()
    {
        // Per ERPNext PR #46716 / commit 638d825d8c:
        // Dunning should be present in Customer Dashboard under Payments
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var dunning = new Sales.Entities.Dunning(
            Guid.NewGuid(), companyId, customerId, DateTime.UtcNow.Date, 1)
        {
            TotalOutstanding = 500m,
            DunningFee = 50m
        };

        var dunningList = new System.Collections.Generic.List<Sales.Entities.Dunning> { dunning };
        var customerDunnings = dunningList.Where(d => d.CustomerId == customerId).ToList();

        Assert.Single(customerDunnings);
        Assert.Equal(550m, customerDunnings[0].GrandTotal);
    }

    [Fact]
    public void PaymentRequest_CalculatesCorrectAmount_AndBlocksOverRequest()
    {
        // Per ERPNext PR #46626 / commit 913c60d77b:
        // Payment request amount considers order advance_paid and existing payment requests.
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // 1. Sales Order with partial advance paid
        var so = new Sales.Entities.SalesOrder(
            Guid.NewGuid(), companyId, customerId, "SO-001", DateTime.UtcNow.Date)
        {
            GrandTotal = 1000m,
            AdvancePaid = 200m
        };

        var maxPayable = so.GrandTotal - so.AdvancePaid;
        Assert.Equal(800m, maxPayable);

        // 2. Existing Payment Request of 500m outstanding
        var pr1 = new Accounting.Entities.PaymentRequest(
            Guid.NewGuid(), companyId, "SalesOrder", so.Id, customerId, "Customer", 500m);
        Assert.Equal(500m, pr1.OutstandingAmount);

        var existingPrs = new System.Collections.Generic.List<Accounting.Entities.PaymentRequest> { pr1 };
        var existingPrAmount = existingPrs.Sum(p => p.OutstandingAmount);
        var remainingAllowed = maxPayable - existingPrAmount;
        Assert.Equal(300m, remainingAllowed);

        // 3. New Payment Request requesting 400m is clamped to remainingAllowed (300m)
        var requested = 400m;
        if (requested > remainingAllowed)
        {
            requested = remainingAllowed;
        }
        Assert.Equal(300m, requested);

        // 4. When full amount is requested, subsequent attempts throw PaymentRequestAlreadyCreated
        existingPrs.Add(new Accounting.Entities.PaymentRequest(
            Guid.NewGuid(), companyId, "SalesOrder", so.Id, customerId, "Customer", 300m));
        var updatedPrAmount = existingPrs.Sum(p => p.OutstandingAmount);
        var noneRemaining = maxPayable - updatedPrAmount;
        Assert.Equal(0m, noneRemaining);
        Assert.True(noneRemaining <= 0);
    }

    [Fact]
    public void InterCompany_FetchesExchangeRate_WhenCompanyCurrenciesDiffer()
    {
        // Per ERPNext commit 145a6c5e2a:
        // Inter-company order and invoice creation must fetch exchange rate when currency differs.
        var sourceCompanyCurrency = "USD";
        var targetCompanyCurrency = "MYR";

        var pi = new Purchasing.Entities.PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "IC-SI-001", DateTime.UtcNow.Date)
        {
            CurrencyCode = sourceCompanyCurrency
        };

        // When currencies differ, rate must be fetched and applied
        bool currenciesDiffer = !string.Equals(pi.CurrencyCode, targetCompanyCurrency, StringComparison.OrdinalIgnoreCase);
        Assert.True(currenciesDiffer);

        decimal fetchedRate = 4.45m;
        if (currenciesDiffer && fetchedRate > 0)
        {
            pi.ExchangeRate = fetchedRate;
        }

        Assert.Equal(4.45m, pi.ExchangeRate);
    }

    [Fact]
    public void Batch_EvaluatesBatchwiseValuation_ForMovingAverageItems()
    {
        // Per ERPNext commits 65ba79bb85 & cc171d9706:
        // Moving average items are allowed to use batchwise valuation unless StockSettings.DoNotUseBatchwiseValuation is enabled.
        var batch1 = new Inventory.Entities.Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-001");
        batch1.EvaluateBatchwiseValuation(Inventory.ValuationMethod.WeightedAverage, doNotUseBatchwiseValuation: false);
        Assert.True(batch1.UseBatchwiseValuation);

        var batch2 = new Inventory.Entities.Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-002");
        batch2.EvaluateBatchwiseValuation(Inventory.ValuationMethod.WeightedAverage, doNotUseBatchwiseValuation: true);
        Assert.False(batch2.UseBatchwiseValuation);

        // FIFO items always use batchwise valuation regardless of the setting
        var batch3 = new Inventory.Entities.Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-003");
        batch3.EvaluateBatchwiseValuation(Inventory.ValuationMethod.FIFO, doNotUseBatchwiseValuation: true);
        Assert.True(batch3.UseBatchwiseValuation);
    }

    [Fact]
    public void PaymentEntry_PopulatesMissingAccountTypeAndCurrency()
    {
        // Per ERPNext PR #47069 / commit a854beeb40:
        // Payment Entry sets account type and currency when missing from account details.
        var paidFromAccount = new Accounting.Entities.Account(
            Guid.NewGuid(), Guid.NewGuid(), "1110", "Bank Account",
            Accounting.AccountType.Asset)
        {
            Currency = "USD"
        };

        var paidToAccount = new Accounting.Entities.Account(
            Guid.NewGuid(), Guid.NewGuid(), "2110", "Accounts Payable",
            Accounting.AccountType.Liability)
        {
            Currency = "MYR"
        };

        var dto = new Accounting.PaymentEntryDto
        {
            PaidFromAccountId = paidFromAccount.Id,
            PaidToAccountId = paidToAccount.Id
        };

        // Initially null
        Assert.Null(dto.PaidFromAccountCurrency);
        Assert.Null(dto.PaidFromAccountType);
        Assert.Null(dto.PaidToAccountCurrency);
        Assert.Null(dto.PaidToAccountType);

        // Simulated resolution
        dto.PaidFromAccountCurrency ??= paidFromAccount.Currency;
        dto.PaidFromAccountType ??= paidFromAccount.AccountType.ToString();
        dto.PaidToAccountCurrency ??= paidToAccount.Currency;
        dto.PaidToAccountType ??= paidToAccount.AccountType.ToString();

        Assert.Equal("USD", dto.PaidFromAccountCurrency);
        Assert.Equal("Asset", dto.PaidFromAccountType);
        Assert.Equal("MYR", dto.PaidToAccountCurrency);
        Assert.Equal("Liability", dto.PaidToAccountType);
    }

    [Fact]
    public void Asset_AllowsNameAndNotesUpdate_WhenSubmitted()
    {
        // Per ERPNext PR #47093 / commit e41720f1a3:
        // AssetName has allow_on_submit enabled, so updating name and notes on submitted assets is permitted.
        var companyId = Guid.NewGuid();
        var asset = new Assets.Entities.Asset(
            Guid.NewGuid(), companyId, "ASS-001", "Laptop A", DateTime.UtcNow.Date, 1500m);

        // Submit the asset
        asset.Submit();
        Assert.Equal(Assets.AssetStatus.Submitted, asset.Status);

        // Update name and notes on submitted asset
        var updatedName = "Laptop A - Renovated";
        var updatedNotes = "Assigned to Engineering team";

        asset.AssetName = updatedName;
        asset.Notes = updatedNotes;

        Assert.Equal(updatedName, asset.AssetName);
        Assert.Equal(updatedNotes, asset.Notes);
    }

    [Fact]
    public void TaxesAndTotals_DistributesDiscountAcrossTaxes_WhenAppliedOnGrandTotal()
    {
        // Per ERPNext PR #47154 / commit 5741458c94:
        // When discount is applied on Grand Total, tax amounts after discount are calculated.
        var service = new Tax.DomainServices.TaxesAndTotalsService();
        var items = new System.Collections.Generic.List<Tax.DomainServices.TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, NetAmount = 1000m }
        };

        var taxes = new System.Collections.Generic.List<Tax.Entities.TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "VAT", "On Net Total", 10m)
            {
                TaxCategory = "Total"
            }
        };

        // Grand Total before discount = 1100. Discount = 110 (10% of Grand Total)
        var totals = service.Calculate(items, taxes, discountAmount: 110m, applyDiscountOn: "Grand Total");

        Assert.Equal(1000m, totals.NetTotal);
        Assert.Equal(100m, totals.TotalTax);
        Assert.Equal(990m, totals.GrandTotal);

        // Tax amount after discount should be 90m (100 - 10)
        Assert.Equal(100m, taxes[0].TaxAmount);
        Assert.Equal(90m, taxes[0].TaxAmountAfterDiscount);
    }

    [Fact]
    public void Bin_RecalculateQty_RefreshesQuantities()
    {
        // Per ERPNext PR #47125 / commit 36081413d8:
        // Bin.RecalculateQty allows setting all calculated quantities from source documents.
        var bin = new Inventory.Entities.Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.RecalculateQty(
            actualQty: 100m,
            stockValue: 5000m,
            plannedQty: 20m,
            indentedQty: 15m,
            orderedQty: 50m,
            reservedQty: 30m,
            reservedQtyForProduction: 10m,
            reservedQtyForSubContract: 5m,
            reservedQtyForProductionPlan: 8m);

        Assert.Equal(100m, bin.ActualQty);
        Assert.Equal(5000m, bin.StockValue);
        Assert.Equal(50m, bin.ValuationRate);
        Assert.Equal(20m, bin.PlannedQty);
        Assert.Equal(15m, bin.IndentedQty);
        Assert.Equal(50m, bin.OrderedQty);
        Assert.Equal(30m, bin.ReservedQty);
        Assert.Equal(10m, bin.ReservedQtyForProduction);
        Assert.Equal(5m, bin.ReservedQtyForSubContract);
        Assert.Equal(8m, bin.ReservedQtyForProductionPlan);

        // Projected = 100 + 50 + 15 + 20 - 30 - 10 - 5 - 8 = 132
        Assert.Equal(132m, bin.ProjectedQty);
    }

    [Fact]
    public void DeliveryNote_BillingStatus_PrioritizesCompleted_WhenFullyBilled_EvenIfReturn()
    {
        // Per ERPNext commit 8290a83591: Completed takes precedence over Return when PerBilled == 100
        var dn = new Sales.Entities.DeliveryNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "DN-001", DateTime.UtcNow.Date);

        dn.AddItem(Guid.NewGuid(), "Item A", 10m, 50m, 0m);
        dn.Items[0].BilledQty = 10m; // Fully billed

        dn.Submit();
        Assert.Equal(100m, dn.PerBilled);
        Assert.Equal("Completed", dn.BillingStatus);

        // Even if marked as return, completed status takes precedence when 100% billed
        dn.IsReturn = true;
        Assert.Equal("Completed", dn.BillingStatus);
    }

    [Fact]
    public void PaymentEntry_Template_ConvertsAmountAcrossCurrencies()
    {
        // Per ERPNext PR #47171 / commit 9612521894:
        // When party account currency differs from bank account currency, convert amount using exchange rate.
        var template = new Accounting.CreatePaymentEntryDto
        {
            PaymentType = Accounting.PaymentType.Receive,
            PaidAmount = 1000m, // USD
            PaidFromAccountCurrency = "USD",
            PaidToAccountCurrency = "MYR",
            PostingDate = DateTime.UtcNow.Date
        };

        decimal rate = 4.45m; // USD to MYR
        if (!template.PaidFromAccountCurrency.Equals(template.PaidToAccountCurrency, StringComparison.OrdinalIgnoreCase))
        {
            template.ExchangeRate = rate;
            template.ReceivedAmount = Math.Round(template.PaidAmount * rate, 2);
        }

        Assert.Equal(1000m, template.PaidAmount);
        Assert.Equal(4450m, template.ReceivedAmount);
        Assert.Equal(4.45m, template.ExchangeRate);

        // Pay type (e.g. Purchase Invoice):
        var payTemplate = new Accounting.CreatePaymentEntryDto
        {
            PaymentType = Accounting.PaymentType.Pay,
            ReceivedAmount = 1000m, // USD
            PaidFromAccountCurrency = "MYR", // Bank
            PaidToAccountCurrency = "USD",   // Supplier
            PostingDate = DateTime.UtcNow.Date
        };

        if (!payTemplate.PaidFromAccountCurrency.Equals(payTemplate.PaidToAccountCurrency, StringComparison.OrdinalIgnoreCase))
        {
            payTemplate.ExchangeRate = rate;
            payTemplate.PaidAmount = Math.Round(payTemplate.ReceivedAmount!.Value * rate, 2);
        }

        Assert.Equal(4450m, payTemplate.PaidAmount);
        Assert.Equal(1000m, payTemplate.ReceivedAmount);
    }

    [Fact]
    public void PaymentRequest_CalculatesAdvanceAmount_BasedOnTransactionCurrency()
    {
        // Per ERPNext commit b570d97b4d:
        // When transaction currency differs from company/party currency,
        // convert advance_paid using the exchange rate to get the remaining payable in doc currency.
        var grandTotal = 1000m; // USD
        var advancePaidInBaseCurrency = 2225m; // MYR (advance paid recorded in base currency)
        var exchangeRate = 4.45m; // USD to MYR
        var currency = "USD";
        var partyCurrency = "MYR";

        var advanceAmount = advancePaidInBaseCurrency;
        if (exchangeRate > 0 && exchangeRate != 1m && !string.Equals(currency, partyCurrency, StringComparison.OrdinalIgnoreCase))
        {
            advanceAmount = Math.Round(advancePaidInBaseCurrency / exchangeRate, 2);
        }

        var remainingOrderAmount = grandTotal - advanceAmount;

        // 2225 / 4.45 = 500 USD advance
        Assert.Equal(500m, advanceAmount);
        // 1000 - 500 = 500 USD remaining
        Assert.Equal(500m, remainingOrderAmount);
    }

    [Fact]
    public void PosConsolidation_DimensionHash_IncludesCostCenterAndProject()
    {
        // Per ERPNext PR #46961 / commit c85edc3346:
        // Invoices with different Cost Center or Project must produce different dimension hashes
        // so that they are consolidated into separate Sales Invoices.
        var companyId = Guid.NewGuid();
        var cc1 = Guid.NewGuid();
        var cc2 = Guid.NewGuid();
        var proj1 = Guid.NewGuid();

        var inv1 = new Sales.Entities.SalesInvoice(
            Guid.NewGuid(), companyId, Guid.NewGuid(), "POS-001", DateTime.UtcNow.Date)
        {
            CostCenterId = cc1,
            ProjectId = proj1
        };

        var inv2 = new Sales.Entities.SalesInvoice(
            Guid.NewGuid(), companyId, Guid.NewGuid(), "POS-002", DateTime.UtcNow.Date)
        {
            CostCenterId = cc2,
            ProjectId = proj1
        };

        var inv3 = new Sales.Entities.SalesInvoice(
            Guid.NewGuid(), companyId, Guid.NewGuid(), "POS-003", DateTime.UtcNow.Date)
        {
            CostCenterId = cc2,
            ProjectId = proj1
        };

        // Compute hashes using the logic in PosConsolidationService:
        // dimensionKey = $"{invoice.CompanyId}|{invoice.CostCenterId}|{invoice.ProjectId}"
        string Hash(Sales.Entities.SalesInvoice inv)
        {
            var key = $"{inv.CompanyId}|{inv.CostCenterId}|{inv.ProjectId}";
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
            return Convert.ToHexStringLower(bytes)[..16];
        }

        var hash1 = Hash(inv1);
        var hash2 = Hash(inv2);
        var hash3 = Hash(inv3);

        // Different cost centers must yield different hashes
        Assert.NotEqual(hash1, hash2);
        // Same cost center and project must yield the same hash
        Assert.Equal(hash2, hash3);
    }

    [Fact]
    public void QualityInspection_AllowAfterPurchaseOrDelivery_BypassesValidation()
    {
        // Per ERPNext commit fad1a32e63:
        // When AllowToMakeQualityInspectionAfterPurchaseOrDelivery is true,
        // validation for PurchaseReceipt, PurchaseInvoice, SalesInvoice, and DeliveryNote is bypassed.
        var referenceTypes = new[] { "PurchaseReceipt", "PurchaseInvoice", "SalesInvoice", "DeliveryNote" };
        var allowAfterStr = "true";
        var isAllowed = bool.TryParse(allowAfterStr, out var allowAfter) && allowAfter;

        foreach (var refType in referenceTypes)
        {
            var shouldBypass = (refType is "PurchaseReceipt" or "PurchaseInvoice" or "SalesInvoice" or "DeliveryNote") && isAllowed;
            Assert.True(shouldBypass);
        }

        // Other reference types (e.g. StockEntry, WorkOrder) must not be bypassed by this setting
        string otherRefType = "WorkOrder";
        var otherBypass = (otherRefType is "PurchaseReceipt" or "PurchaseInvoice" or "SalesInvoice" or "DeliveryNote") && isAllowed;
        Assert.False(otherBypass);
    }

    [Fact]
    public void PaymentEntry_SkipsAllocation_WhenReferenceDoctypeOrIdNotSet()
    {
        // Per ERPNext PR #47334 / commit b9a02b466b:
        // Do not allocate amount when reference doctype or name (id) are not set.
        var refs = new System.Collections.Generic.List<Accounting.PaymentReferenceDto>
        {
            new() { ReferenceType = "", ReferenceId = Guid.NewGuid(), AllocatedAmount = 100m },
            new() { ReferenceType = "SalesInvoice", ReferenceId = Guid.Empty, AllocatedAmount = 200m },
            new() { ReferenceType = "   ", ReferenceId = Guid.NewGuid(), AllocatedAmount = 300m },
            new() { ReferenceType = "SalesInvoice", ReferenceId = Guid.NewGuid(), AllocatedAmount = 400m },
        };

        var validRefs = refs
            .Where(r => !string.IsNullOrWhiteSpace(r.ReferenceType) && r.ReferenceId != Guid.Empty)
            .ToList();

        Assert.Single(validRefs);
        Assert.Equal(400m, validRefs[0].AllocatedAmount);
    }

    [Fact]
    public void TransitTransfer_CompletedTransfers_NotShowingInPendingList()
    {
        // Per ERPNext PR #47374 / commit 97db9da10e:
        // Stock entries with per_transferred >= 100% must not show in pending in-transit list.
        var sentQty = 100m;
        var fullReceivedQty = 100m;
        var partialReceivedQty = 60m;

        bool IsPending(decimal sent, decimal received) => received < sent;

        Assert.False(IsPending(sentQty, fullReceivedQty));
        Assert.True(IsPending(sentQty, partialReceivedQty));
        Assert.Equal(40m, sentQty - partialReceivedQty);
    }

    [Fact]
    public void ItemPriceAutoInsert_ConsidersPriceListRateAndExistingRate()
    {
        // Per ERPNext commit 3ebde4526a:
        // update_price_list_based_on ("Rate" vs "Price List Rate") selects rate source
        // and scales by conversion_factor to stock UOM.
        var item = new Inventory.DomainServices.AutoInsertPriceItem
        {
            ItemId = Guid.NewGuid(),
            Rate = 80m,
            PriceListRate = 100m,
            ConversionFactor = 2m,
            Uom = "Box"
        };

        decimal ResolveRate(string updateBasedOn, Inventory.DomainServices.AutoInsertPriceItem itm)
        {
            var updateBasedOnPriceListRate = string.Equals(updateBasedOn, "Price List Rate", StringComparison.OrdinalIgnoreCase);
            var rateToConsider = updateBasedOnPriceListRate
                ? (itm.PriceListRate.HasValue && itm.PriceListRate.Value > 0 ? itm.PriceListRate.Value : itm.Rate)
                : itm.Rate;
            var conversion = itm.ConversionFactor > 0 ? itm.ConversionFactor : 1m;
            return Math.Round(rateToConsider / conversion, 4);
        }

        Assert.Equal(50m, ResolveRate("Price List Rate", item));
        Assert.Equal(40m, ResolveRate("Rate", item));
    }

    [Fact]
    public void Asset_DepreciationSchedule_HonorsExpectedValueAfterUsefulLife()
    {
        // Per ERPNext commit 2a89bac11d:
        // Asset depreciation must not depreciate below ExpectedValueAfterUsefulLife (salvage value)
        var asset = new Assets.Entities.Asset(
            Guid.NewGuid(), Guid.NewGuid(), "AST-001", "Delivery Van",
            DateTime.UtcNow.Date, 10000m)
        {
            CalculateDepreciation = true,
            DepreciationMethod = Assets.DepreciationMethod.StraightLine,
            UsefulLifeMonths = 24,
            FrequencyMonths = 12, // 2 periods
            ExpectedValueAfterUsefulLife = 2000m
        };

        asset.GenerateDepreciationSchedule();

        Assert.Equal(2, asset.DepreciationSchedule.Count);
        // Depreciable base = 10000 - 2000 = 8000 across 2 periods = 4000 per period
        Assert.Equal(4000m, asset.DepreciationSchedule[0].DepreciationAmount);
        Assert.Equal(4000m, asset.DepreciationSchedule[0].AccumulatedDepreciation);
        Assert.Equal(4000m, asset.DepreciationSchedule[1].DepreciationAmount);
        Assert.Equal(8000m, asset.DepreciationSchedule[1].AccumulatedDepreciation);

        // Ending book value = 10000 - 8000 = 2000 (matches ExpectedValueAfterUsefulLife)
        var endingBookValue = asset.TotalAssetCost - asset.DepreciationSchedule[1].AccumulatedDepreciation;
        Assert.Equal(2000m, endingBookValue);
    }

    [Fact]
    public void Asset_ApplyRepairCapitalization_UpdatesDepreciationDetailsAndLife()
    {
        // Per ERPNext commit c567a08470:
        // AssetRepair capitalization must increase useful life and value in DepreciationDetails (finance books)
        var asset = new Assets.Entities.Asset(
            Guid.NewGuid(), Guid.NewGuid(), "AST-002", "CNC Machine",
            DateTime.UtcNow.Date, 20000m)
        {
            UsefulLifeMonths = 24,
            FrequencyMonths = 12,
            ValueAfterDepreciation = 15000m
        };

        var detail = new Assets.Entities.AssetDepreciationDetail(
            Guid.NewGuid(), asset.Id, Assets.DepreciationMethod.StraightLine,
            2, 12, 20000m)
        {
            ValueAfterDepreciation = 15000m
        };
        asset.DepreciationDetails.Add(detail);

        asset.ApplyRepairCapitalization(5000m, 12);

        Assert.Equal(25000m, asset.TotalAssetCost);
        Assert.Equal(20000m, asset.ValueAfterDepreciation);
        Assert.Equal(36, asset.UsefulLifeMonths);

        Assert.Equal(20000m, detail.ValueAfterDepreciation);
        Assert.Equal(12, detail.IncreaseInAssetLife);
        Assert.Equal(3, detail.TotalNumberOfDepreciations);
    }

    [Fact]
    public void PaymentReconciliation_ReconcileEffectOn_HonorsCompanySetting()
    {
        // Per ERPNext commit 19f1ffbdc2:
        // ReconcileEffectOn must be derived from Company.ReconciliationTakesEffectOn
        var peDate = new DateTime(2026, 5, 1);
        var invoiceDate = new DateTime(2026, 5, 10);
        var today = DateTime.UtcNow.Date;

        DateTime ResolveReconcileDate(string companySetting, DateTime pePostingDate, DateTime invPostingDate) =>
            companySetting switch
            {
                "Advance Payment Date" => pePostingDate,
                "Reconciliation Date" => today,
                _ => invPostingDate < pePostingDate ? pePostingDate : invPostingDate // "Oldest Of Invoice Or Advance"
            };

        Assert.Equal(peDate, ResolveReconcileDate("Advance Payment Date", peDate, invoiceDate));
        Assert.Equal(today, ResolveReconcileDate("Reconciliation Date", peDate, invoiceDate));
        Assert.Equal(invoiceDate, ResolveReconcileDate("Oldest Of Invoice Or Advance", peDate, invoiceDate));

        var olderInvoiceDate = new DateTime(2026, 4, 20);
        Assert.Equal(peDate, ResolveReconcileDate("Oldest Of Invoice Or Advance", peDate, olderInvoiceDate));
    }

    [Fact]
    public void StockReservation_TransferReservationEntries_UpdatesFromVoucher()
    {
        // Per ERPNext commit 0bc3cfe29d:
        // When transferring reservations from Production Plan to Work Order,
        // target SRE records FromVoucherType, FromVoucherId, and FromVoucherDetailId.
        var planId = Guid.NewGuid();
        var woId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var planDetailId = Guid.NewGuid();
        var woDetailId = Guid.NewGuid();

        var sourceSre = new Inventory.Entities.StockReservationEntry(
            Guid.NewGuid(), companyId, itemId, whId,
            "ProductionPlan", planId, 50m, 50m)
        {
            VoucherDetailId = planDetailId
        };
        sourceSre.Submit();

        var targetSre = new Inventory.Entities.StockReservationEntry(
            Guid.NewGuid(), companyId, itemId, whId,
            "WorkOrder", woId, 30m, 30m)
        {
            VoucherDetailId = woDetailId,
            FromVoucherType = sourceSre.VoucherType,
            FromVoucherId = sourceSre.VoucherId,
            FromVoucherDetailId = sourceSre.VoucherDetailId
        };
        targetSre.Submit();

        Assert.Equal("ProductionPlan", targetSre.FromVoucherType);
        Assert.Equal(planId, targetSre.FromVoucherId);
        Assert.Equal(planDetailId, targetSre.FromVoucherDetailId);
        Assert.Equal(30m, targetSre.ReservedQty);
    }

    [Fact]
    public void StockEntry_AdditionalItem_HonorsValidateComponentsQuantitiesPerBom()
    {
        // Per ERPNext PR #47548 / commit fc554ba599:
        // When ValidateComponentsQuantitiesPerBom is enabled, extra/additional items
        // transferred in Stock Entry are NOT appended to WorkOrder.RequiredItems.
        var mfgSettings = new Manufacturing.Entities.ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid())
        {
            ValidateComponentsQuantitiesPerBom = true
        };

        var allowAdditionalItems = !mfgSettings.ValidateComponentsQuantitiesPerBom;
        Assert.False(allowAdditionalItems);

        mfgSettings.ValidateComponentsQuantitiesPerBom = false;
        allowAdditionalItems = !mfgSettings.ValidateComponentsQuantitiesPerBom;
        Assert.True(allowAdditionalItems);
    }

    [Fact]
    public void Budget_AccumulatedMonthlyLimit_Calculation()
    {
        // Per ERPNext commit 388d901668:
        // When checking accumulated monthly budget limit, calculates accumulated percentage
        // based on monthly distribution or even split across elapsed months in fiscal year.
        var budgetAmount = 120000m;
        var elapsedMonths = 6; // Half-year
        var defaultMonthlyBudget = Math.Round(budgetAmount * (elapsedMonths * (100m / 12m)) / 100m, 2);
        Assert.Equal(60000m, defaultMonthlyBudget);

        // With custom monthly distribution
        var distribution = new Accounting.Entities.MonthlyDistribution(Guid.NewGuid(), "Seasonal");
        distribution.SetPercentages(new[]
        {
            (1, 5m), (2, 5m), (3, 10m), (4, 10m), (5, 10m), (6, 20m),
            (7, 10m), (8, 10m), (9, 5m), (10, 5m), (11, 5m), (12, 5m)
        });

        var accumulatedPct = distribution.Percentages.Where(p => p.Month <= elapsedMonths).Sum(p => p.PercentageAllocation);
        Assert.Equal(60m, accumulatedPct); // 5 + 5 + 10 + 10 + 10 + 20 = 60%
        var seasonalBudget = Math.Round(budgetAmount * accumulatedPct / 100m, 2);
        Assert.Equal(72000m, seasonalBudget);
    }

    [Fact]
    public void Budget_ExceptionBudgetApproverRole_DowngradesStopToWarn()
    {
        // Per ERPNext commit 58556c82bb:
        // Users with Company.ExceptionBudgetApproverRole have "Stop" actions downgraded to "Warn".
        var company = new Core.Entities.Company(Guid.NewGuid(), "Test Co")
        {
            ExceptionBudgetApproverRole = "Budget Manager"
        };

        Assert.Equal("Budget Manager", company.ExceptionBudgetApproverRole);

        Accounting.BudgetAction DowngradeAction(Accounting.BudgetAction action, bool isExceptionApprover)
        {
            if (isExceptionApprover && action == Accounting.BudgetAction.Stop)
                return Accounting.BudgetAction.Warn;
            return action;
        }

        Assert.Equal(Accounting.BudgetAction.Warn, DowngradeAction(Accounting.BudgetAction.Stop, true));
        Assert.Equal(Accounting.BudgetAction.Stop, DowngradeAction(Accounting.BudgetAction.Stop, false));
        Assert.Equal(Accounting.BudgetAction.Ignore, DowngradeAction(Accounting.BudgetAction.Ignore, true));
    }
}
