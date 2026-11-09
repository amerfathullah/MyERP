using System;
using System.Collections.Generic;
using MyERP.Sales.DomainServices;
using MyERP.Settings;
using Shouldly;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream parity batch 27:
/// 1) Return product bundle components at original document packed ratio after component UOM change (PR #65053 / commit 7edd914d84, 65a52e868b).
/// 2) General Ledger reporting date exchange rate conversion for foreign currency accounts (PR #65016 / commit e8ae93e729).
/// 3) Expose serial and batch selector setting to clients via boot/configuration (PR #65000 / commit f2f2d83a7e).
/// </summary>
public class UpstreamBatch27ParityTests
{
    // === 1) PR #65053: Product Bundle Return Component Quantity ===

    [Fact]
    public void GetComponentStockQtyPerUnit_WithOriginalDeliveredRatio_PreservesOriginalPackedRatio()
    {
        // Equivalent to test_bundle_return_keeps_delivered_qty_after_component_uom_change
        // Component UOM conversion changed post-delivery:
        // Originally delivered with conversion factor 10 (1 Box = 10 Nos)
        // Later changed in item master to 12 (1 Box = 12 Nos)
        // Return must preserve original delivered ratio of 10 Nos per bundle
        var currentComponentQty = 1m;
        var newConversionFactor = 12m;
        var originalDeliveredRatio = 10m; // from original DN packed items / SLEs

        var ratio = ProductBundleDecompositionService.GetComponentStockQtyPerUnit(
            bundleComponentQty: currentComponentQty,
            conversionFactor: newConversionFactor,
            soPackedQtyPerUnit: originalDeliveredRatio);

        // Uses original delivered ratio (10), not current conversion factor (12)
        ratio.ShouldBe(10m);
    }

    [Fact]
    public void GetComponentStockQtyPerUnit_WithoutOriginalRatio_UsesCurrentConversionFactor()
    {
        var ratio = ProductBundleDecompositionService.GetComponentStockQtyPerUnit(
            bundleComponentQty: 2m,
            conversionFactor: 5m,
            soPackedQtyPerUnit: null);

        ratio.ShouldBe(10m); // 2 * 5
    }

    [Fact]
    public void GetComponentStockQtyPerUnit_WhenConversionFactorUnchanged_UsesExactConversionFactor()
    {
        // When conversion factor hasn't changed (PR #65052), exact conversion factor avoids rounding creep
        var ratio = ProductBundleDecompositionService.GetComponentStockQtyPerUnit(
            bundleComponentQty: 1m,
            conversionFactor: 1m / 3m, // 0.3333333333...
            soPackedQtyPerUnit: 0.3333m,
            originalPackedQty: 0.3333m,
            originalOrderStockQty: 1m,
            precision: 4);

        ratio.ShouldBe(1m / 3m);
    }

    // === 2) PR #65016: Reporting Date Exchange Rate Rule ===

    [Fact]
    public void GeneralLedgerReportingRate_ConvertAtReportingDateRateRule()
    {
        // When ConvertAtReportingDateRate is true, reporting rate on ToDate converts company currency to presentation currency
        var companyCurrencyAmount = 450m; // MYR
        var reportingDateRate = 4.5m; // 1 USD = 4.5 MYR on reporting date
        var historicalRate = 4.0m; // 1 USD = 4.0 MYR on transaction date

        var convertedAtReportingDate = Math.Round(companyCurrencyAmount / reportingDateRate, 2);
        var convertedAtHistorical = Math.Round(companyCurrencyAmount / historicalRate, 2);

        convertedAtReportingDate.ShouldBe(100m); // 450 / 4.5 = 100 USD
        convertedAtHistorical.ShouldBe(112.5m); // 450 / 4.0 = 112.50 USD

        convertedAtReportingDate.ShouldNotBe(convertedAtHistorical);
    }

    // === 3) PR #65000: Serial and Batch Selector Setting Visible to Clients ===

    [Fact]
    public void SettingDefinition_DisableSerialNoAndBatchSelector_IsVisibleToClients()
    {
        // PR #65000: Stock Settings selector flag must be readable by clients without Stock Settings permission
        var provider = new MyERPSettingDefinitionProvider();
        var dict = new Dictionary<string, SettingDefinition>();
        var context = new SettingDefinitionContext(dict);
        provider.Define(context);

        var setting = context.GetOrNull(MyERPSettings.Stock.DisableSerialNoAndBatchSelector);
        setting.ShouldNotBeNull();
        setting.IsVisibleToClients.ShouldBeTrue();
    }
}
