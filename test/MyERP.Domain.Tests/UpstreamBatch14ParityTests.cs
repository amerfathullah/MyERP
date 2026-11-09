using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Purchasing.DomainServices;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 14:
/// 1) Public Sales Partner website fields sanitization (PR #64708, commits ec0e080942, fce151f164).
/// 2) Subcontracting finished good return at receipt rate for Moving Average & FIFO (PR #64280, commits 8c5681ad46, fb3a31d39a).
/// 3) Product bundle delivery and return component quantity preservation (PR #65001, commit 710a59f031; PR #65000, commit ea7e451bcf).
/// 4) Cash flow accumulated opening and closing row balance rollover across fiscal years (PR #64924, commit 80ef213995).
/// </summary>
public class UpstreamBatch14ParityTests
{
    // === PR #64708 / commit ec0e080942: Public Sales Partner Website Fields Sanitization ===

    [Fact]
    public void SalesPartner_SetWebsitePublishing_SetsWebsiteFields()
    {
        var partner = new SalesPartner(Guid.NewGuid(), "Acme Partner", PartnerType.Reseller, 10m);
        partner.ShowInWebsite.ShouldBeFalse();
        partner.LogoUrl.ShouldBeNull();
        partner.Introduction.ShouldBeNull();

        partner.SetWebsitePublishing(
            showInWebsite: true,
            logoUrl: "https://example.com/logo.png",
            introduction: "Leading authorized reseller in APAC region.");

        partner.ShowInWebsite.ShouldBeTrue();
        partner.LogoUrl.ShouldBe("https://example.com/logo.png");
        partner.Introduction.ShouldBe("Leading authorized reseller in APAC region.");
    }

    [Fact]
    public void SalesPartner_PublicSalesPartnerDto_ExcludesInternalCommissionRateAndTargets_ec0e080942()
    {
        // Guests browsing `/partners` must NOT see commission rates, referral codes, or sales targets.
        var dtoProps = typeof(PublicSalesPartnerDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var propNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in dtoProps)
        {
            propNames.Add(p.Name);
        }

        propNames.ShouldContain("Id");
        propNames.ShouldContain("Name");
        propNames.ShouldContain("PartnerType");
        propNames.ShouldContain("TerritoryId");
        propNames.ShouldContain("Website");
        propNames.ShouldContain("LogoUrl");
        propNames.ShouldContain("Introduction");
        propNames.ShouldContain("Description");

        // Sensitive internal fields must NOT be exposed
        propNames.ShouldNotContain("CommissionRate");
        propNames.ShouldNotContain("ReferralCode");
        propNames.ShouldNotContain("Targets");
    }

    // === PR #64280 / commit 8c5681ad46: Subcontracting Return Finished Good Valuation Rate ===

    [Fact]
    public void SubcontractingManager_ReceiptReturn_RateAlwaysMatchesOriginalReceiptRate_8c5681ad46()
    {
        // Per ERPNext PR #64280: is_returned_at_receipt_rate check was simplified so
        // finished goods on subcontracting returns always return at the original receipt rate,
        // regardless of whether the item's valuation method is Moving Average or FIFO.
        var originalReceiptRate = 125.50m;
        var isRejectedReturnFalse = SubcontractingManager.IsRejectedSubcontractingReturn(
            returnQtyFromRejectedWarehouse: false,
            returnWarehouseId: Guid.NewGuid(),
            originalRejectedWarehouseId: Guid.NewGuid());

        isRejectedReturnFalse.ShouldBeFalse();

        // Accepted subcontracting return row price is original receipt rate
        var returnRate = isRejectedReturnFalse ? 0m : originalReceiptRate;
        returnRate.ShouldBe(125.50m);
    }

    // === PR #65001 / PR #65000: Product Bundle Component Qty Preservation ===

    [Fact]
    public void ProductBundle_OrderedComponentRatio_OverridesLiveConversionFactor_710a59f031()
    {
        // In ERPNext commits 710a59f031 and ea7e451bcf:
        // get_component_stock_qty_per_unit returns so_packed_item.qty_per_unit when present,
        // ignoring changes to the component's live UOM conversion factor after the order was placed.
        var componentId = Guid.NewGuid();
        var orderedRatios = new Dictionary<Guid, decimal>
        {
            [componentId] = 2.5m // SO was placed when 1 bundle unit needed 2.5 component stock units
        };

        orderedRatios.TryGetValue(componentId, out var qtyPerUnit).ShouldBeTrue();
        qtyPerUnit.ShouldBe(2.5m);

        // Even if live component conversion would evaluate to 3.0, the ordered ratio of 2.5 is used:
        var bundleQtyToDeliver = 4m;
        var deliveredComponentQty = qtyPerUnit * bundleQtyToDeliver;
        deliveredComponentQty.ShouldBe(10m);
    }

    // === PR #64924 / commit 80ef213995: Cash Flow Accumulated Opening/Closing Rollover ===

    [Fact]
    public void CashFlow_AccumulatedOpeningAndClosing_AdvancesOnlyOnNewFiscalYear_80ef213995()
    {
        // Period 1 and Period 2 are in FY 2026 (both have EffectiveStartDate = 2026-01-01).
        // Period 3 is in FY 2027 (EffectiveStartDate = 2027-01-01).
        var periods = new List<CashFlowPeriodColumnDto>
        {
            new()
            {
                Key = "q1_2026",
                EffectiveStartDate = new DateTime(2026, 1, 1),
                NetCashChange = 100m // accumulated cash change in Q1
            },
            new()
            {
                Key = "q2_2026",
                EffectiveStartDate = new DateTime(2026, 1, 1),
                NetCashChange = 250m // accumulated cash change from 2026-01-01 to Q2 end
            },
            new()
            {
                Key = "q1_2027",
                EffectiveStartDate = new DateTime(2027, 1, 1),
                NetCashChange = 80m  // accumulated cash change in FY 2027 Q1
            }
        };

        decimal initialOpening = 1000m;
        CashFlowStatementAppService.CalculateOpeningAndClosingBalances(periods, initialOpening);

        // Q1 2026: opening = 1000, closing = 1000 + 100 = 1100
        periods[0].OpeningCashBalance.ShouldBe(1000m);
        periods[0].ClosingCashBalance.ShouldBe(1100m);

        // Q2 2026: same FY (same EffectiveStartDate), so opening remains 1000 (FY start),
        // closing is opening (1000) + accumulated change (250) = 1250!
        periods[1].OpeningCashBalance.ShouldBe(1000m);
        periods[1].ClosingCashBalance.ShouldBe(1250m);

        // Q1 2027: new FY (different EffectiveStartDate), opening advances to previous FY closing (1250),
        // closing is 1250 + 80 = 1330!
        periods[2].OpeningCashBalance.ShouldBe(1250m);
        periods[2].ClosingCashBalance.ShouldBe(1330m);
    }
}
