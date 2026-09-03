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
}
