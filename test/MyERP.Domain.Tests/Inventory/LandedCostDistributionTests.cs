using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Dtos;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Unit tests for Landed Cost Voucher multi-receipt querying and distribution calculation rules.
/// Verifies rules from erpnext/stock/doctype/landed_cost_voucher (#5996).
/// </summary>
public class LandedCostDistributionTests
{
    private readonly IRepository<LandedCostVoucher, Guid> _lcvRepo = Substitute.For<IRepository<LandedCostVoucher, Guid>>();
    private readonly IRepository<SerialNo, Guid> _serialRepo = Substitute.For<IRepository<SerialNo, Guid>>();
    private readonly IRepository<PurchaseReceipt, Guid> _prRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
    private readonly IRepository<PurchaseInvoice, Guid> _piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly StockValuationService _valuationService = Substitute.For<StockValuationService>(
        Substitute.For<IRepository<StockLedgerEntry, Guid>>(),
        Substitute.For<IRepository<Item, Guid>>(),
        Substitute.For<Volo.Abp.Settings.ISettingProvider>());
    private readonly BinService _binService = Substitute.For<BinService>(
        Substitute.For<IRepository<Bin, Guid>>());
    private readonly WarehouseAccountService _warehouseAccountService = Substitute.For<WarehouseAccountService>(
        Substitute.For<IRepository<WarehouseAccount, Guid>>(),
        Substitute.For<IRepository<Warehouse, Guid>>(),
        Substitute.For<IRepository<Company, Guid>>(),
        Substitute.For<IRepository<Account, Guid>>());
    private readonly IDocumentNumberGenerator _numGen = Substitute.For<IDocumentNumberGenerator>();

    private readonly LandedCostVoucherAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    public LandedCostDistributionTests()
    {
        _appService = new LandedCostVoucherAppService(
            _lcvRepo, _serialRepo, _prRepo, _piRepo, _jeRepo, _fyRepo,
            _valuationService, _binService, _warehouseAccountService, _numGen);
    }

    [Fact]
    public async Task GetReceiptItemsAsync_PurchaseReceipt_ReturnsItems()
    {
        var prId = Guid.NewGuid();
        var pr = new PurchaseReceipt(prId, _companyId, _supplierId, _warehouseId, "PR-2026-0001", DateTime.UtcNow);
        var itemId1 = Guid.NewGuid();
        var itemId2 = Guid.NewGuid();
        pr.AddItem(itemId1, "Steel Rod", 10m, 50m, 0m);
        pr.AddItem(itemId2, "Copper Wire", 5m, 100m, 0m);
        pr.Submit();

        var prList = new List<PurchaseReceipt> { pr };
        _prRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseReceipt, object>>[]>())
            .Returns(Task.FromResult(prList.AsQueryable()));

        var input = new GetLandedCostReceiptItemsInput
        {
            CompanyId = _companyId,
            ReceiptType = "PurchaseReceipt",
            ReceiptIds = new List<Guid> { prId }
        };

        var result = await _appService.GetReceiptItemsAsync(input);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Steel Rod", result[0].Description);
        Assert.Equal(10m, result[0].Quantity);
        Assert.Equal(500m, result[0].Amount);
        Assert.Equal(5m, result[1].Quantity);
        Assert.Equal(500m, result[1].Amount);
    }

    [Fact]
    public async Task GetReceiptItemsAsync_UnsubmittedReceipt_ThrowsValidationException()
    {
        var prId = Guid.NewGuid();
        var pr = new PurchaseReceipt(prId, _companyId, _supplierId, _warehouseId, "PR-2026-DRAFT", DateTime.UtcNow);
        pr.AddItem(Guid.NewGuid(), "Item", 10m, 20m, 0m);
        // Not submitted

        var prList = new List<PurchaseReceipt> { pr };
        _prRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseReceipt, object>>[]>())
            .Returns(Task.FromResult(prList.AsQueryable()));

        var input = new GetLandedCostReceiptItemsInput
        {
            CompanyId = _companyId,
            ReceiptType = "PurchaseReceipt",
            ReceiptIds = new List<Guid> { prId }
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.GetReceiptItemsAsync(input));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public async Task GetReceiptItemsAsync_PurchaseInvoiceWithoutUpdateStock_ThrowsValidationException()
    {
        var piId = Guid.NewGuid();
        var pi = new PurchaseInvoice(piId, _companyId, _supplierId, "PINV-2026-0001", DateTime.UtcNow)
        {
            UpdateStock = false // UpdateStock disabled
        };
        pi.AddItem(Guid.NewGuid(), "Service item", 1m, 200m, 0m);
        pi.Submit();

        var piList = new List<PurchaseInvoice> { pi };
        _piRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseInvoice, object>>[]>())
            .Returns(Task.FromResult(piList.AsQueryable()));

        var input = new GetLandedCostReceiptItemsInput
        {
            CompanyId = _companyId,
            ReceiptType = "PurchaseInvoice",
            ReceiptIds = new List<Guid> { piId }
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.GetReceiptItemsAsync(input));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public async Task CalculateDistributionAsync_BasedOnAmount_CalculatesCorrectlyWithRounding()
    {
        var input = new CalculateLandedCostDistributionDto
        {
            DistributionMethod = LandedCostDistributionMethod.BasedOnAmount,
            TotalCharges = 100m,
            Items = new List<LandedCostItemDto>
            {
                new() { ItemId = Guid.NewGuid(), Quantity = 10m, Amount = 100m },
                new() { ItemId = Guid.NewGuid(), Quantity = 10m, Amount = 200m }
            }
        };

        var result = await _appService.CalculateDistributionAsync(input);

        Assert.NotNull(result);
        Assert.Equal(100m, result.TotalCharges);
        Assert.Equal(100m, result.TotalDistributedAmount);
        Assert.Equal(33.33m, result.DistributedItems[0].ApplicableCharges);
        Assert.Equal(66.67m, result.DistributedItems[1].ApplicableCharges); // Last item absorbs rounding difference
    }

    [Fact]
    public async Task CalculateDistributionAsync_BasedOnQuantity_CalculatesProportionally()
    {
        var input = new CalculateLandedCostDistributionDto
        {
            DistributionMethod = LandedCostDistributionMethod.BasedOnQuantity,
            TotalCharges = 300m,
            Items = new List<LandedCostItemDto>
            {
                new() { ItemId = Guid.NewGuid(), Quantity = 30m, Amount = 1000m },
                new() { ItemId = Guid.NewGuid(), Quantity = 10m, Amount = 500m }
            }
        };

        var result = await _appService.CalculateDistributionAsync(input);

        Assert.NotNull(result);
        Assert.Equal(300m, result.TotalCharges);
        Assert.Equal(300m, result.TotalDistributedAmount);
        Assert.Equal(225m, result.DistributedItems[0].ApplicableCharges); // 30/40 * 300
        Assert.Equal(75m, result.DistributedItems[1].ApplicableCharges);  // 10/40 * 300
    }
}
