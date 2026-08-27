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
/// Unit tests for Subcontracting Receipt Return creation, validation, and summary metrics.
/// Verifies rules from erpnext/subcontracting/doctype/subcontracting_receipt (#5997).
/// </summary>
public class SubcontractingReceiptWorkflowTests
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
    private readonly Guid _scoId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    public SubcontractingReceiptWorkflowTests()
    {
        _appService = new SubcontractingAppService(
            _scoRepo, _scrRepo, _numGen, _stockValuationService, _binService);

        _numGen.GenerateAsync(Arg.Any<string>(), Arg.Any<Guid>())
            .Returns(Task.FromResult("SCR-RET-2026-00001"));
    }

    [Fact]
    public async Task CreateReceiptReturnAsync_ValidQuantities_CreatesReturnReceiptWithNegativeQty()
    {
        var origReceiptId = Guid.NewGuid();
        var origReceipt = new SubcontractingReceipt(
            origReceiptId, _companyId, "SCR-2026-00001", new DateTime(2026, 6, 10), _supplierId, _scoId)
        {
            WarehouseId = _warehouseId
        };
        var origItem = new SubcontractingReceiptItem(
            Guid.NewGuid(), origReceiptId, _itemId, "Subcontracted Part", 100m, 25m)
        {
            WarehouseId = _warehouseId
        };
        origReceipt.AddItem(origItem);
        origReceipt.Submit();

        _scrRepo.GetAsync(origReceiptId, includeDetails: true).Returns(Task.FromResult(origReceipt));

        var emptyList = new List<SubcontractingReceipt>();
        _scrRepo.GetQueryableAsync().Returns(Task.FromResult(emptyList.AsQueryable()));

        SubcontractingReceipt? savedReturn = null;
        await _scrRepo.InsertAsync(Arg.Do<SubcontractingReceipt>(r => savedReturn = r), autoSave: true);

        var input = new CreateSubcontractingReceiptReturnDto
        {
            ReturnAgainstReceiptId = origReceiptId,
            PostingDate = new DateTime(2026, 6, 15),
            Items = new List<CreateScrReturnItemDto>
            {
                new CreateScrReturnItemDto
                {
                    ItemId = _itemId,
                    ItemName = "Subcontracted Part",
                    Qty = 20m, // return 20 units
                    Rate = 25m,
                    WarehouseId = _warehouseId
                }
            }
        };

        var result = await _appService.CreateReceiptReturnAsync(input);

        Assert.NotNull(savedReturn);
        Assert.True(savedReturn.IsReturn);
        Assert.Equal(origReceiptId, savedReturn.ReturnAgainstReceiptId);
        Assert.Single(savedReturn.Items);
        var returnItem = savedReturn.Items[0];
        Assert.Equal(-20m, returnItem.Qty);
        Assert.Equal(-500m, savedReturn.NetTotal);
    }

    [Fact]
    public async Task CreateReceiptReturnAsync_ExcessiveQuantity_ThrowsValidationException()
    {
        var origReceiptId = Guid.NewGuid();
        var origReceipt = new SubcontractingReceipt(
            origReceiptId, _companyId, "SCR-2026-00001", new DateTime(2026, 6, 10), _supplierId, _scoId);
        var origItem = new SubcontractingReceiptItem(
            Guid.NewGuid(), origReceiptId, _itemId, "Subcontracted Part", 100m, 25m);
        origReceipt.AddItem(origItem);
        origReceipt.Submit();

        _scrRepo.GetAsync(origReceiptId, includeDetails: true).Returns(Task.FromResult(origReceipt));

        var emptyList = new List<SubcontractingReceipt>();
        _scrRepo.GetQueryableAsync().Returns(Task.FromResult(emptyList.AsQueryable()));

        var input = new CreateSubcontractingReceiptReturnDto
        {
            ReturnAgainstReceiptId = origReceiptId,
            PostingDate = new DateTime(2026, 6, 15),
            Items = new List<CreateScrReturnItemDto>
            {
                new CreateScrReturnItemDto
                {
                    ItemId = _itemId,
                    ItemName = "Subcontracted Part",
                    Qty = 150m, // exceeds 100
                    Rate = 25m
                }
            }
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.CreateReceiptReturnAsync(input));
        Assert.Equal(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal, ex.Code);
    }

    [Fact]
    public async Task GetReceiptSummaryAsync_ReturnsAccurateMetrics()
    {
        var receiptId = Guid.NewGuid();
        var receipt = new SubcontractingReceipt(
            receiptId, _companyId, "SCR-2026-00001", new DateTime(2026, 6, 10), _supplierId, _scoId);
        var item1 = new SubcontractingReceiptItem(
            Guid.NewGuid(), receiptId, _itemId, "Part A", 50m, 10m);
        var item2 = new SubcontractingReceiptItem(
            Guid.NewGuid(), receiptId, Guid.NewGuid(), "Part B", 25m, 20m);
        receipt.AddItem(item1);
        receipt.AddItem(item2);

        _scrRepo.GetAsync(receiptId, includeDetails: true).Returns(Task.FromResult(receipt));

        var summary = await _appService.GetReceiptSummaryAsync(receiptId);

        Assert.NotNull(summary);
        Assert.Equal(receiptId, summary.Id);
        Assert.Equal(2, summary.TotalItemsCount);
        Assert.Equal(75m, summary.TotalReceivedQty);
        Assert.Equal(1000m, summary.NetTotal);
        Assert.False(summary.IsReturn);
    }
}
