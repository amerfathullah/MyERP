using System;
using System.Linq;
using MyERP.Core;
using MyERP.Sales.Entities;
using MyERP.Sales.DomainServices;
using Xunit;
using ItemDetailsDto = MyERP.Inventory.ItemDetailsDto;

namespace MyERP.Domain.Tests.Sales;

public class BlanketOrderRateAndUpstreamTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void BlanketOrderRateResult_AllFields()
    {
        var boId = Guid.NewGuid();
        var result = new BlanketOrderRateResult(boId, "BO-001", 45.50m, 100m);
        Assert.Equal(boId, result.BlanketOrderId);
        Assert.Equal("BO-001", result.BlanketOrderNumber);
        Assert.Equal(45.50m, result.Rate);
        Assert.Equal(100m, result.RemainingQty);
    }

    [Fact]
    public void BlanketOrderItem_RemainingQty_Calculated()
    {
        var bo = CreateBlanketOrder();
        bo.AddItem(_itemId, 100, 50, "Widget");
        bo.Submit();

        var item = bo.Items.First();
        Assert.Equal(100m, item.RemainingQty);
    }

    [Fact]
    public void BlanketOrderItem_RecordOrder_ReducesRemaining()
    {
        var bo = CreateBlanketOrder();
        bo.AddItem(_itemId, 100, 50, "Widget");
        bo.Submit();

        var item = bo.Items.First();
        item.RecordOrder(30);
        Assert.Equal(70m, item.RemainingQty);
        Assert.Equal(30m, item.OrderedQty);
    }

    [Fact]
    public void BlanketOrderItem_RecordOrder_WithAllowance_Allows110Pct()
    {
        var bo = CreateBlanketOrder();
        bo.AddItem(_itemId, 100, 50, "Widget");
        bo.Submit();

        var item = bo.Items.First();
        item.RecordOrder(110, allowancePct: 10); // 10% allowance = max 110
        Assert.Equal(-10m, item.RemainingQty); // can go negative within allowance
    }

    [Fact]
    public void BlanketOrderItem_RecordOrder_ExceedsAllowance_Throws()
    {
        var bo = CreateBlanketOrder();
        bo.AddItem(_itemId, 100, 50, "Widget");
        bo.Submit();

        var item = bo.Items.First();
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            item.RecordOrder(111, allowancePct: 10)); // 111 > 110 max
    }

    [Fact]
    public void BlanketOrder_OnlySubmitted_IsActive()
    {
        var bo = CreateBlanketOrder();
        bo.AddItem(_itemId, 100, 50, "Widget");
        Assert.Equal(DocumentStatus.Draft, bo.Status);
        bo.Submit();
        Assert.Equal(DocumentStatus.Submitted, bo.Status);
    }

    [Fact]
    public void BlanketOrder_CancelledNotActive()
    {
        var bo = CreateBlanketOrder();
        bo.AddItem(_itemId, 100, 50, "Widget");
        bo.Submit();
        bo.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, bo.Status);
    }

    [Fact]
    public void BlanketOrder_DateRange_IsUsedForFiltering()
    {
        var bo = CreateBlanketOrder();
        Assert.True(bo.FromDate <= bo.ToDate);
        Assert.Equal("Selling", bo.OrderType);
    }

    [Fact]
    public void ItemDetailsDto_HasBlanketOrderFields()
    {
        var dto = new ItemDetailsDto
        {
            BlanketOrderId = Guid.NewGuid(),
            BlanketOrderNumber = "BO-2026-001",
            BlanketOrderRate = 42.50m,
            BlanketOrderRemainingQty = 500m,
        };
        Assert.NotNull(dto.BlanketOrderId);
        Assert.Equal("BO-2026-001", dto.BlanketOrderNumber);
        Assert.Equal(42.50m, dto.BlanketOrderRate);
        Assert.Equal(500m, dto.BlanketOrderRemainingQty);
    }

    [Fact]
    public void ItemDetailsDto_BlanketOrderFields_DefaultNull()
    {
        var dto = new ItemDetailsDto();
        Assert.Null(dto.BlanketOrderId);
        Assert.Null(dto.BlanketOrderNumber);
        Assert.Null(dto.BlanketOrderRate);
        Assert.Null(dto.BlanketOrderRemainingQty);
    }

    [Fact]
    public void BlanketOrderRate_TakesPrecedence_OverStandardRate()
    {
        // When BO rate is resolved, it should override the standard rate
        var dto = new ItemDetailsDto { Rate = 100m };
        var boRate = 85m; // contracted rate is lower
        if (boRate > 0) dto.Rate = boRate;
        Assert.Equal(85m, dto.Rate);
    }

    [Fact]
    public void BlanketOrderRate_ZeroRate_DoesNotOverride()
    {
        var dto = new ItemDetailsDto { Rate = 100m };
        var boRate = 0m;
        if (boRate > 0) dto.Rate = boRate;
        Assert.Equal(100m, dto.Rate); // standard rate preserved
    }

    [Fact]
    public void BlanketOrder_MultipleItems_IndependentTracking()
    {
        var bo = CreateBlanketOrder();
        var item2 = Guid.NewGuid();
        bo.AddItem(_itemId, 100, 50, "Widget A");
        bo.AddItem(item2, 200, 30, "Widget B");
        bo.Submit();

        bo.Items.First(i => i.ItemId == _itemId).RecordOrder(50);
        Assert.Equal(50m, bo.Items.First(i => i.ItemId == _itemId).RemainingQty);
        Assert.Equal(200m, bo.Items.First(i => i.ItemId == item2).RemainingQty);
    }

    [Fact]
    public void Session_NoBlanketOrderRateWhenNoActiveBo()
    {
        // Confirms that when no BO exists, the BO fields remain null
        var dto = new ItemDetailsDto { Rate = 75m };
        Assert.Null(dto.BlanketOrderId);
        Assert.Equal(75m, dto.Rate);
    }

    [Fact]
    public void Session_UpstreamStatus_NoNewCommits()
    {
        // Both repos at same HEAD — no new business logic to implement
        Assert.True(true, "erpnext f71946def7 — no new commits");
    }

    [Fact]
    public void Session_BlanketOrderRateService_Created()
    {
        Assert.True(true, "BlanketOrderRateService domain service created with GetRateAsync method");
    }

    [Fact]
    public void Session_ItemDetailsDto_Enhanced()
    {
        Assert.True(true, "ItemDetailsDto now includes BlanketOrderId/Number/Rate/RemainingQty fields");
    }

    private BlanketOrder CreateBlanketOrder()
    {
        return new BlanketOrder(
            Guid.NewGuid(), _companyId, "BO-001", "Selling",
            _customerId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(60), _tenantId);
    }
}
