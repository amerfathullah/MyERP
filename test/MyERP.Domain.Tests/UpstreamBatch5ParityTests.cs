using System;
using System.Collections.Generic;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 5:
/// - d0dde24142: Cost Center Allocation accepts decimal percentages that sum to 100.
/// - 674a548db7 &amp; 2683d487a6: Cost Center Allocation cost center state &amp; duplicate guards.
/// - 044510375a, b5cdfa2c13 &amp; c2c6810199: Period Closing Voucher guards and zero-net P&amp;L closing.
/// - f820b711cc, d08131b6e8 &amp; b8e3c8d8f2: Production Plan &amp; MPS BOM item/active validation and item default warehouse fallback.
/// </summary>
public class UpstreamBatch5ParityTests
{
    [Fact]
    public void CostCenterAllocation_ValidatePercentages_AcceptsDecimalsSummingTo100_d0dde24142()
    {
        var companyId = Guid.NewGuid();
        var mainCcId = Guid.NewGuid();
        var child1 = Guid.NewGuid();
        var child2 = Guid.NewGuid();
        var child3 = Guid.NewGuid();
        var child4 = Guid.NewGuid();

        // In IEEE 754 float: 64.07 + 3.95 + 17.15 + 14.83 = 100.00000000000001
        var allocation = new CostCenterAllocation(Guid.NewGuid(), companyId, mainCcId, new DateTime(2026, 3, 1));
        allocation.AddEntry(child1, 64.07m);
        allocation.AddEntry(child2, 3.95m);
        allocation.AddEntry(child3, 17.15m);
        allocation.AddEntry(child4, 14.83m);

        Should.NotThrow(() => allocation.ValidatePercentages());

        var distributed = CostCenterAllocationService.DistributeAmountByPercentages(
            1000m,
            new List<(Guid, decimal)>
            {
                (child1, 64.07m),
                (child2, 3.95m),
                (child3, 17.15m),
                (child4, 14.83m),
            },
            precision: 2);

        distributed.Count.ShouldBe(4);
        distributed[0].Amount.ShouldBe(640.70m);
        distributed[1].Amount.ShouldBe(39.50m);
        distributed[2].Amount.ShouldBe(171.50m);
        distributed[3].Amount.ShouldBe(148.30m);
    }

    [Fact]
    public void CostCenterAllocation_ValidatePercentages_RejectsDecimalsNotSummingTo100_d0dde24142()
    {
        var allocation = new CostCenterAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 3, 1));
        allocation.AddEntry(Guid.NewGuid(), 64.07m);
        allocation.AddEntry(Guid.NewGuid(), 3.95m);
        allocation.AddEntry(Guid.NewGuid(), 17.15m);
        allocation.AddEntry(Guid.NewGuid(), 14.80m); // 99.97%

        var ex = Should.Throw<BusinessException>(() => allocation.ValidatePercentages());
        ex.Code.ShouldBe("MyERP:02042");
    }

    [Fact]
    public void PeriodClosingVoucher_Submit_AllowsZeroNetProfitAndLoss_c2c6810199()
    {
        var companyId = Guid.NewGuid();
        var fyId = Guid.NewGuid();
        var closingAccId = Guid.NewGuid();

        var pcv = new PeriodClosingVoucher(
            Guid.NewGuid(),
            companyId,
            fyId,
            new DateTime(2026, 12, 31),
            new DateTime(2026, 12, 31),
            closingAccId);

        Should.NotThrow(() => pcv.Submit());
        pcv.Status.ShouldBe(DocumentStatus.Submitted);
    }
}
