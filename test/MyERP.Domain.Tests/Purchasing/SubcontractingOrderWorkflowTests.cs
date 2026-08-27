using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Purchasing;

/// <summary>
/// Unit tests for Subcontracting Order Reopening, validation, and summary metrics.
/// Verifies rules from erpnext/subcontracting/doctype/subcontracting_order (#5993).
/// </summary>
public class SubcontractingOrderWorkflowTests
{
    private readonly IRepository<SubcontractingOrder, Guid> _scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
    private readonly IRepository<SubcontractingReceipt, Guid> _scrRepo = Substitute.For<IRepository<SubcontractingReceipt, Guid>>();
    private readonly IDocumentNumberGenerator _numGen = Substitute.For<IDocumentNumberGenerator>();
    private readonly StockValuationService _stockValuationService = Substitute.For<StockValuationService>(
        Substitute.For<IRepository<global::MyERP.Inventory.Entities.StockLedgerEntry, Guid>>(),
        Substitute.For<IRepository<global::MyERP.Inventory.Entities.Item, Guid>>(),
        Substitute.For<Volo.Abp.Settings.ISettingProvider>());
    private readonly BinService _binService = Substitute.For<BinService>(
        Substitute.For<IRepository<global::MyERP.Inventory.Entities.Bin, Guid>>());

    private readonly SubcontractingAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _rmItemId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    public SubcontractingOrderWorkflowTests()
    {
        _appService = new SubcontractingAppService(
            _scoRepo, _scrRepo, _numGen, _stockValuationService, _binService);
    }

    [Fact]
    public async Task ReopenOrderAsync_ClosedOrder_ReopensAndRestoresReservation()
    {
        var scoId = Guid.NewGuid();
        var sco = new SubcontractingOrder(scoId, _companyId, "SCO-2026-00001", new DateTime(2026, 6, 1), _supplierId);
        var item = new SubcontractingOrderItem(Guid.NewGuid(), scoId, _itemId, "Finished Part", 10m, 50m);
        sco.AddItem(item);
        sco.Submit();

        var suppliedItem = new SubcontractingOrderSuppliedItem(Guid.NewGuid(), scoId, _rmItemId, "Raw Steel", 20m)
        {
            ReserveWarehouseId = _warehouseId,
            ConsumedQty = 5m // 15m pending
        };
        sco.AddSuppliedItem(suppliedItem);

        sco.Close();
        Assert.Equal(SubcontractingOrderStatus.Closed, sco.Status);

        _scoRepo.GetAsync(scoId, includeDetails: true).Returns(Task.FromResult(sco));

        var updated = await _appService.ReopenOrderAsync(scoId);

        Assert.Equal(SubcontractingOrderStatus.Open, sco.Status);
        await _binService.Received(1).UpdateReservedQtyForSubContractAsync(_rmItemId, _warehouseId, 15m);
    }

    [Fact]
    public async Task ReopenOrderAsync_NonClosedOrder_ThrowsValidationException()
    {
        var scoId = Guid.NewGuid();
        var sco = new SubcontractingOrder(scoId, _companyId, "SCO-2026-00001", new DateTime(2026, 6, 1), _supplierId);
        var item = new SubcontractingOrderItem(Guid.NewGuid(), scoId, _itemId, "Finished Part", 10m, 50m);
        sco.AddItem(item);
        sco.Submit(); // Open

        _scoRepo.GetAsync(scoId, includeDetails: true).Returns(Task.FromResult(sco));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.ReopenOrderAsync(scoId));
        Assert.Equal(MyERPDomainErrorCodes.InvalidStatusTransition, ex.Code);
    }

    [Fact]
    public async Task GetOrderSummaryAsync_ReturnsAccurateMetrics()
    {
        var scoId = Guid.NewGuid();
        var sco = new SubcontractingOrder(scoId, _companyId, "SCO-2026-00001", new DateTime(2026, 6, 1), _supplierId);
        var item1 = new SubcontractingOrderItem(Guid.NewGuid(), scoId, _itemId, "Part 1", 10m, 50m);
        var item2 = new SubcontractingOrderItem(Guid.NewGuid(), scoId, Guid.NewGuid(), "Part 2", 5m, 100m);
        sco.AddItem(item1);
        sco.AddItem(item2);
        sco.Submit();

        var supplied = new SubcontractingOrderSuppliedItem(Guid.NewGuid(), scoId, _rmItemId, "Raw Steel", 30m);
        sco.AddSuppliedItem(supplied);

        _scoRepo.GetAsync(scoId, includeDetails: true).Returns(Task.FromResult(sco));

        var summary = await _appService.GetOrderSummaryAsync(scoId);

        Assert.NotNull(summary);
        Assert.Equal(scoId, summary.Id);
        Assert.Equal(2, summary.TotalItemsCount);
        Assert.Equal(1, summary.TotalSuppliedItemsCount);
        Assert.Equal(15m, summary.TotalOrderedQty);
        Assert.Equal(1000m, summary.NetTotal);
        Assert.True(summary.CanClose);
        Assert.False(summary.CanReopen);
    }
}
