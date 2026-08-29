using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Purchasing;

/// <summary>
/// Unit tests for Subcontracting Inward Order workflow, reopen transitions,
/// Sales Order mapping, and action status summaries (#5994).
/// </summary>
public class SubcontractingInwardOrderWorkflowTests
{
    private readonly IRepository<SubcontractingInwardOrder, Guid> _scioRepo = Substitute.For<IRepository<SubcontractingInwardOrder, Guid>>();
    private readonly IRepository<DocumentSeries, Guid> _seriesRepo = Substitute.For<IRepository<DocumentSeries, Guid>>();
    private readonly StockValuationService _stockValuationService = Substitute.For<StockValuationService>(
        Substitute.For<IRepository<global::MyERP.Inventory.Entities.StockLedgerEntry, Guid>>(),
        Substitute.For<IRepository<global::MyERP.Inventory.Entities.Item, Guid>>(),
        Substitute.For<Volo.Abp.Settings.ISettingProvider>());
    private readonly BinService _binService = Substitute.For<BinService>(
        Substitute.For<IRepository<global::MyERP.Inventory.Entities.Bin, Guid>>());
    private readonly SubcontractingInwardOrderAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();

    public SubcontractingInwardOrderWorkflowTests()
    {
        _appService = new SubcontractingInwardOrderAppService(
            _scioRepo, _seriesRepo, _stockValuationService, _binService);
    }

    [Fact]
    public void Reopen_ClosedOrder_RestoresOpenStatus()
    {
        var scio = new SubcontractingInwardOrder(Guid.NewGuid(), _companyId, "SCIO-2026-001", DateTime.UtcNow, _supplierId);
        scio.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, Guid.NewGuid(), 10m, 50m));
        scio.Submit();
        scio.Close();

        Assert.Equal(SubcontractingInwardOrderStatus.Closed, scio.Status);

        scio.Reopen();

        Assert.Equal(SubcontractingInwardOrderStatus.Open, scio.Status);
    }

    [Fact]
    public void Reopen_OpenOrder_ThrowsValidationException()
    {
        var scio = new SubcontractingInwardOrder(Guid.NewGuid(), _companyId, "SCIO-2026-002", DateTime.UtcNow, _supplierId);
        scio.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, Guid.NewGuid(), 5m, 20m));
        scio.Submit();

        var ex = Assert.Throws<BusinessException>(() => scio.Reopen());
        Assert.Equal(MyERPDomainErrorCodes.InvalidStatusTransition, ex.Code);
    }

    [Fact]
    public async Task ReopenAsync_ClosedOrder_UpdatesRepositoryAndReturnsDto()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, _companyId, "SCIO-2026-003", DateTime.UtcNow, _supplierId);
        scio.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, Guid.NewGuid(), 20m, 10m));
        scio.Submit();
        scio.Close();

        _scioRepo.GetAsync(scioId).Returns(scio);

        var result = await _appService.ReopenAsync(scioId);

        Assert.NotNull(result);
        Assert.Equal(SubcontractingInwardOrderStatus.Open, result.Status);
        await _scioRepo.Received(1).UpdateAsync(scio);
    }

    [Fact]
    public async Task GetActionSummaryAsync_ReturnsCorrectActionFlags()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, _companyId, "SCIO-2026-004", DateTime.UtcNow, _supplierId);
        var item1 = new SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, Guid.NewGuid(), 10m, 50m)
        {
            ReceivedQty = 5m // pending 5
        };
        scio.AddItem(item1);
        scio.Submit();
        scio.UpdateReceivedStatus();

        _scioRepo.GetAsync(scioId).Returns(scio);

        var summary = await _appService.GetActionSummaryAsync(scioId);

        Assert.NotNull(summary);
        Assert.Equal(SubcontractingInwardOrderStatus.PartiallyReceived, summary.Status);
        Assert.Equal(50m, summary.PerReceived);
        Assert.True(summary.CanClose);
        Assert.True(summary.CanCancel);
        Assert.False(summary.CanReopen);
        Assert.Equal(1, summary.PendingItemCount);
    }

    [Fact]
    public async Task ReceiveItemsAsync_QtyExceedsPending_ThrowsValidationException()
    {
        var scioId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, _companyId, "SCIO-2026-006", DateTime.UtcNow, _supplierId);
        scio.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, itemId, 10m, 50m)
        {
            WarehouseId = Guid.NewGuid()
        });
        scio.Submit();
        _scioRepo.GetAsync(scioId).Returns(scio);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.ReceiveItemsAsync(scioId, new ScioReceiveItemsDto
        {
            PostingDate = DateTime.UtcNow,
            Items = new List<ScioReceiveItemDto> { new() { ItemId = itemId, Qty = 11m } }
        }));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public async Task ReceiveItemsAsync_DraftOrder_ThrowsInvalidStatusTransition()
    {
        var scioId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, _companyId, "SCIO-2026-007", DateTime.UtcNow, _supplierId);
        scio.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, itemId, 10m, 50m)
        {
            WarehouseId = Guid.NewGuid()
        });
        _scioRepo.GetAsync(scioId).Returns(scio);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.ReceiveItemsAsync(scioId, new ScioReceiveItemsDto
        {
            PostingDate = DateTime.UtcNow,
            Items = new List<ScioReceiveItemDto> { new() { ItemId = itemId, Qty = 1m } }
        }));
        Assert.Equal(MyERPDomainErrorCodes.InvalidStatusTransition, ex.Code);
    }
}
