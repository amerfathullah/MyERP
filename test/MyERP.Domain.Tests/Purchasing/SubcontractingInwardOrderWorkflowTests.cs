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
    private readonly IRepository<global::MyERP.Inventory.Entities.StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<global::MyERP.Inventory.Entities.StockLedgerEntry, Guid>>();
    private readonly IRepository<global::MyERP.Inventory.Entities.Item, Guid> _itemRepo = Substitute.For<IRepository<global::MyERP.Inventory.Entities.Item, Guid>>();
    private readonly StockValuationService _stockValuationService;
    private readonly BinService _binService = Substitute.For<BinService>(
        Substitute.For<IRepository<global::MyERP.Inventory.Entities.Bin, Guid>>());
    private readonly SubcontractingInwardOrderAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();

    public SubcontractingInwardOrderWorkflowTests()
    {
        _stockValuationService = Substitute.For<StockValuationService>(
            _sleRepo,
            _itemRepo,
            Substitute.For<Volo.Abp.Settings.ISettingProvider>());

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

    [Fact]
    public async Task ReceiveItemsAsync_MatchesBySubcontractingInwardOrderItemId_DisambiguatesDuplicateItemRows()
    {
        // Per ERPNext PR #58949 / commit d5e63b8a9e:
        // Support selecting against finished good by row ID (name) as well as item_code.
        var scioId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, _companyId, "SCIO-2026-008", DateTime.UtcNow, _supplierId);

        var row1Id = Guid.NewGuid();
        var row2Id = Guid.NewGuid();
        var wh1 = Guid.NewGuid();
        var wh2 = Guid.NewGuid();

        var itemRow1 = new SubcontractingInwardOrderItem(row1Id, scio.Id, itemId, 10m, 50m)
        {
            WarehouseId = wh1,
            ServiceCostPerQty = 15m
        };
        var itemRow2 = new SubcontractingInwardOrderItem(row2Id, scio.Id, itemId, 20m, 60m)
        {
            WarehouseId = wh2,
            ServiceCostPerQty = 18m
        };

        scio.AddItem(itemRow1);
        scio.AddItem(itemRow2);
        scio.Submit();

        _scioRepo.GetAsync(scioId).Returns(scio);
        _itemRepo.GetAsync(itemId).Returns(new global::MyERP.Inventory.Entities.Item(itemId, _companyId, "FG-ITEM", "FG Item", global::MyERP.Inventory.ItemType.Goods));
        _sleRepo.GetQueryableAsync().Returns(new List<global::MyERP.Inventory.Entities.StockLedgerEntry>().AsQueryable());

        var lazyProvider = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        var guidGen = Substitute.For<Volo.Abp.Guids.IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());
        lazyProvider.LazyGetService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetRequiredService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);
        lazyProvider.LazyGetRequiredService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);
        _stockValuationService.LazyServiceProvider = lazyProvider;

        // Receive specifically against row 2 using SubcontractingInwardOrderItemId
        var result = await _appService.ReceiveItemsAsync(scioId, new ScioReceiveItemsDto
        {
            PostingDate = DateTime.UtcNow,
            Items = new List<ScioReceiveItemDto>
            {
                new()
                {
                    SubcontractingInwardOrderItemId = row2Id,
                    ItemId = itemId,
                    Qty = 8m
                }
            }
        });

        Assert.Equal(0m, itemRow1.ReceivedQty);
        Assert.Equal(8m, itemRow2.ReceivedQty);
        Assert.Equal(12m, itemRow2.PendingReceiptQty);
    }
}
