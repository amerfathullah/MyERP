using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests;

public class UpstreamParityBatch20261026Part4Tests
{
    // --- ERPNext PR #62149 / commit 05bc9ce0fe: Product Bundle in Pick List ---

    [Fact]
    public void PickListManager_ComputePickedQtyForBundle_UsesOrderRowBundleVersion()
    {
        // Scenario:
        // Bundle v1 requires 2 components per bundle.
        // Later bundle v2 requires 4 components per bundle.
        // Sales order item was placed when v1 was active (or specifically recorded v1 with 2 components).
        // 6 components are picked in Pick List.
        // Picked bundles must be 6 / 2 = 3 bundles, NOT 6 / 4 = 1 bundle.

        var soItemId = Guid.NewGuid();
        var componentItemId = Guid.NewGuid();
        var pickListId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var packedRowId = Guid.NewGuid();

        var pickItems = new List<PickListItem>
        {
            new PickListItem(
                id: Guid.NewGuid(),
                pickListId: pickListId,
                itemId: componentItemId,
                warehouseId: warehouseId,
                qty: 6m,
                stockQty: 6m,
                itemName: "Component",
                batchId: null,
                productBundleItemId: packedRowId)
            {
                SourceDocumentItemId = soItemId
            }
        };

        // Bundle version v1 requires 2 of componentItemId per bundle
        var bundleV1Map = new Dictionary<Guid, decimal>
        {
            [componentItemId] = 2m
        };

        var pickedBundles = PickListManager.ComputePickedQtyForBundle(pickItems, soItemId, bundleV1Map);

        pickedBundles.ShouldBe(3);
    }

    [Fact]
    public void PickListManager_ComputePickedQtyForBundle_AccountsForComponentUomConversion()
    {
        // Scenario:
        // Component UOM in bundle is "Box", with conversion factor 12 (1 Box = 12 Nos).
        // Pick List items are in stock UOM ("Nos").
        // Pick List picks 12 Nos.
        // Component required qty in stock UOM is 1 * 12 = 12 Nos.
        // 12 Nos picked / 12 = 1 bundle (not 12 bundles).

        var soItemId = Guid.NewGuid();
        var componentItemId = Guid.NewGuid();
        var pickListId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var packedRowId = Guid.NewGuid();

        var pickItems = new List<PickListItem>
        {
            new PickListItem(
                id: Guid.NewGuid(),
                pickListId: pickListId,
                itemId: componentItemId,
                warehouseId: warehouseId,
                qty: 12m,
                stockQty: 12m,
                itemName: "Component",
                batchId: null,
                productBundleItemId: packedRowId)
            {
                SourceDocumentItemId = soItemId
            }
        };

        // In stock UOM, 1 Box = 12 Nos
        var bundleMapInStockUom = new Dictionary<Guid, decimal>
        {
            [componentItemId] = 12m
        };

        var pickedBundles = PickListManager.ComputePickedQtyForBundle(pickItems, soItemId, bundleMapInStockUom);

        pickedBundles.ShouldBe(1);
    }

    [Fact]
    public async Task ProductBundleDecompositionService_DecomposeAsync_HonorsProductBundleIdOverride()
    {
        var parentItemId = Guid.NewGuid();
        var comp1 = Guid.NewGuid();
        var comp2 = Guid.NewGuid();

        // Bundle v1: 2 comp1
        var bundleV1 = new ProductBundle(Guid.NewGuid(), parentItemId) { IsActive = false }; // old version
        bundleV1.AddItem(comp1, 2m, "Comp 1");

        // Bundle v2: 4 comp2 (currently active)
        var bundleV2 = new ProductBundle(Guid.NewGuid(), parentItemId) { IsActive = true };
        bundleV2.AddItem(comp2, 4m, "Comp 2");

        var bundleRepo = Substitute.For<IRepository<ProductBundle, Guid>>();
        bundleRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { bundleV1, bundleV2 }.AsQueryable()));

        var service = new ProductBundleDecompositionService(bundleRepo);

        // Call DecomposeAsync specifying bundleV1 explicitly (order row version)
        var decomposed = await service.DecomposeAsync(
            itemId: parentItemId,
            transactionQty: 3m,
            parentRate: 100m,
            productBundleId: bundleV1.Id);

        decomposed.Count.ShouldBe(1);
        decomposed[0].ComponentItemId.ShouldBe(comp1);
        decomposed[0].Qty.ShouldBe(6m); // 2 * 3 = 6
        decomposed[0].ParentBundleId.ShouldBe(bundleV1.Id);
    }

    // --- ERPNext PR #62153 & #60274: Chart of Accounts Importer validations ---

    [Fact]
    public void ChartOfAccountsImportService_ValidateRootTypes_RejectsMissingRootType()
    {
        // Only Asset, Liability, Equity, Revenue (Expense missing)
        var rows = new List<CoaTemplateRow>
        {
            new CoaTemplateRow("1000", "Assets", AccountType.Asset, isGroup: true),
            new CoaTemplateRow("2000", "Liabilities", AccountType.Liability, isGroup: true),
            new CoaTemplateRow("3000", "Equity", AccountType.Equity, isGroup: true),
            new CoaTemplateRow("4000", "Revenue", AccountType.Revenue, isGroup: true),
        };

        var ex = Should.Throw<BusinessException>(() =>
            ChartOfAccountsImportService.ValidateRootTypes(rows));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Expense");
    }

    [Fact]
    public void ChartOfAccountsImportService_ValidateMandatoryAccountTypes_RejectsMissingBankOrCash()
    {
        // Has all roots, but missing Bank and Cash
        var rows = new List<CoaTemplateRow>
        {
            new CoaTemplateRow("1000", "Assets", AccountType.Asset, isGroup: true),
            new CoaTemplateRow("2000", "Liabilities", AccountType.Liability, isGroup: true),
            new CoaTemplateRow("3000", "Equity", AccountType.Equity, isGroup: true),
            new CoaTemplateRow("4000", "Revenue", AccountType.Revenue, isGroup: true),
            new CoaTemplateRow("5000", "Expenses", AccountType.Expense, isGroup: true),

            new CoaTemplateRow("1130", "Accounts Receivable", AccountType.Asset, parentCode: "1000", subType: AccountSubType.AccountsReceivable),
            new CoaTemplateRow("1140", "Inventory", AccountType.Asset, parentCode: "1000", subType: AccountSubType.Stock),
            new CoaTemplateRow("1210", "Equipment", AccountType.Asset, parentCode: "1000", subType: AccountSubType.FixedAsset),
            new CoaTemplateRow("2110", "Accounts Payable", AccountType.Liability, parentCode: "2000", subType: AccountSubType.AccountsPayable),
            new CoaTemplateRow("5100", "Cost of Goods Sold", AccountType.Expense, parentCode: "5000", subType: AccountSubType.CostOfGoodsSold),
            new CoaTemplateRow("5200", "Depreciation Expense", AccountType.Expense, parentCode: "5000", subType: AccountSubType.DepreciationExpense),
            new CoaTemplateRow("5300", "Stock Adjustment", AccountType.Expense, parentCode: "5000"),
        };

        var ex = Should.Throw<BusinessException>(() =>
            ChartOfAccountsImportService.ValidateMandatoryAccountTypes(rows));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Bank");
        ex.Data["detail"]!.ToString()!.ShouldContain("Cash");
    }

    [Fact]
    public void ChartOfAccountsImportService_StandardTemplates_PassAllValidations()
    {
        var myTemplate = ChartOfAccountsImportService.GetMalaysianTemplate();
        Should.NotThrow(() => ChartOfAccountsImportService.ValidateRootTypes(myTemplate));
        Should.NotThrow(() => ChartOfAccountsImportService.ValidateMandatoryAccountTypes(myTemplate));
    }
}
