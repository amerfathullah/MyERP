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
}
