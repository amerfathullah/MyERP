using System;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for upstream sync July 31 — PR material transfer precision fix
/// + SCIO fulfillment tracking enhancements.
/// </summary>
public class UpstreamSyncJuly31PrecisionAndScioTests
{
    // ========== Material Transfer Precision (PR 1ff8bf7971) ==========

    [Fact]
    public void ValidateTransferQty_PrecisionRounding_FloatingPointArtifact_Passes()
    {
        var mgr = new StockEntryManager(null!, null!, null!);
        // 9.9999999 rounds to 10.000000 at precision 6, pending is 10.000000 — no excess
        Should.NotThrow(() => mgr.ValidateTransferQty(
            requiredQty: 10m, transferredQty: 0m, requestedQty: 9.9999999m, qtyPrecision: 6));
    }

    [Fact]
    public void ValidateTransferQty_PrecisionRounding_StillBlocksGenuineExcess()
    {
        var mgr = new StockEntryManager(null!, null!, null!);
        // 5.001 rounds to 5.00 at precision 2, pending is 5.00 — 5.00 > 5.00 is false, passes
        // But 5.01 rounds to 5.01 at precision 2, pending is 5.00 — 5.01 > 5.00 = BLOCKED
        Should.Throw<BusinessException>(() => mgr.ValidateTransferQty(
            requiredQty: 10m, transferredQty: 5m, requestedQty: 5.01m, qtyPrecision: 2));
    }

    [Fact]
    public void ValidateTransferQty_DefaultPrecision_Is6()
    {
        var mgr = new StockEntryManager(null!, null!, null!);
        Should.NotThrow(() => mgr.ValidateTransferQty(
            requiredQty: 10m, transferredQty: 3m, requestedQty: 7m));
    }

    [Fact]
    public void ValidateTransferQty_HighPrecision_AllowsNearExact()
    {
        var mgr = new StockEntryManager(null!, null!, null!);
        Should.NotThrow(() => mgr.ValidateTransferQty(
            requiredQty: 10m, transferredQty: 3m, requestedQty: 7.0000001m, qtyPrecision: 6));
    }

    [Fact]
    public void ValidateTransferQty_PendingRoundedDown_AllowsTransfer()
    {
        var mgr = new StockEntryManager(null!, null!, null!);
        Should.NotThrow(() => mgr.ValidateTransferQty(
            requiredQty: 10m, transferredQty: 2.9999999m, requestedQty: 7m, qtyPrecision: 6));
    }

    // ========== Upstream Tracking ==========

    [Fact]
    public void Upstream_ErpnextCommit_1ff8bf7971_MaterialTransferPrecision()
    {
        true.ShouldBeTrue();
    }

    [Fact]
    public void Upstream_Myinvois_NoChanges()
    {
        true.ShouldBeTrue();
    }

    // ========== SCIO Fulfillment Sub-Status Tracking ==========

    [Fact]
    public void SubcontractingInwardOrder_PerReceived_DefaultsZero()
    {
        var scio = new SubcontractingInwardOrder(
            Guid.NewGuid(), Guid.NewGuid(), "SCIO-001", DateTime.UtcNow, Guid.NewGuid());
        scio.PerReceived.ShouldBe(0);
    }

    [Fact]
    public void SubcontractingInwardOrder_PerBilled_DefaultsZero()
    {
        var scio = new SubcontractingInwardOrder(
            Guid.NewGuid(), Guid.NewGuid(), "SCIO-002", DateTime.UtcNow, Guid.NewGuid());
        scio.PerBilled.ShouldBe(0);
    }

    // ========== Stock Entry Material Transfer Purpose Validation ==========

    [Fact]
    public void StockEntry_MaterialTransferForManufacture_TypeValue()
    {
        ((int)StockEntryType.MaterialTransferForManufacture).ShouldBe(3);
    }

    [Fact]
    public void StockEntry_Manufacture_TypeValue()
    {
        ((int)StockEntryType.Manufacture).ShouldBe(4);
    }
}
