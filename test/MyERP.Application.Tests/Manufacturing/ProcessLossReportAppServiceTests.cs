using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Unit tests for ProcessLossReportAppService per ERPNext PR #60680 / commits ff394e3ab4d, 14d424e4bc1, dac1d259fa:
/// - Values process loss against processed qty (output_qty + process_loss_qty), NOT planned WO qty.
/// - Counts all finished goods output in the process loss denominator, including intermediate semi-FG rows.
/// - Distinguishes between produced_qty (production item only) and output_qty (all finished goods).
/// - Excludes work orders without any process loss.
/// </summary>
public class ProcessLossReportAppServiceTests
{
    private readonly IRepository<StockEntry, Guid> _stockEntryRepo = Substitute.For<IRepository<StockEntry, Guid>>();
    private readonly IRepository<WorkOrder, Guid> _workOrderRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();

    private readonly ProcessLossReportAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly DateTime _fromDate = new DateTime(2026, 1, 1);
    private readonly DateTime _toDate = new DateTime(2026, 12, 31);

    public ProcessLossReportAppServiceTests()
    {
        _appService = new ProcessLossReportAppService(
            _stockEntryRepo,
            _workOrderRepo,
            _itemRepo);
    }

    [Fact]
    public async Task GetReportAsync_PartialProduction_ValuesLossAgainstProcessedQty_NotPlannedQty()
    {
        // Work Order planned for 10 units, but this entry produces 4 units with 1 unit process loss
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-FG", "Finished Product", ItemType.Goods);

        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, _companyId, "WO-2026-0001", itemId, Guid.NewGuid(), 10m);

        var targetWh = Guid.NewGuid();
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, new DateTime(2026, 6, 1))
        {
            WorkOrderId = woId,
            ProcessLossQty = 1m
        };
        // Add 4 units FG with valuation rate 100 -> Incoming value = 400
        se.AddItem(itemId, 4m, sourceWarehouseId: null, targetWarehouseId: targetWh, valuationRate: 100m, isFinishedItem: true);
        se.Submit();

        var stockEntries = new List<StockEntry> { se };
        _stockEntryRepo.WithDetailsAsync(Arg.Any<Expression<Func<StockEntry, object>>[]>())
            .Returns(Task.FromResult(stockEntries.AsQueryable()));

        _workOrderRepo.GetListAsync(Arg.Any<Expression<Func<WorkOrder, bool>>>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo }));

        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { item }));

        var request = new ProcessLossReportRequestDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        };

        var report = await _appService.GetReportAsync(request);

        report.Rows.Count.ShouldBe(1);
        var row = report.Rows[0];
        row.WorkOrderId.ShouldBe(woId);
        row.QtyToManufacture.ShouldBe(10m);
        row.ProducedQty.ShouldBe(4m);
        row.OutputQty.ShouldBe(4m);
        row.ProcessLossQty.ShouldBe(1m);
        row.TotalFgValue.ShouldBe(400m);

        // Per ERPNext PR #60680:
        // processedQty = outputQty (4) + processLossQty (1) = 5
        // totalPlValue = processLossQty (1) * totalFgValue (400) / processedQty (5) = 80
        // (Old buggy behavior would have divided by qty_to_manufacture 10 = 40)
        row.TotalProcessLossValue.ShouldBe(80m);
        report.TotalProcessLossValue.ShouldBe(80m);
    }

    [Fact]
    public async Task GetReportAsync_IntermediateOperation_CountsAllFinishedOutputInDenominator()
    {
        // Work Order is for Item A, but intermediate Job Card stock entry produces Semi-FG Item B
        var itemAId = Guid.NewGuid();
        var itemBId = Guid.NewGuid();
        var itemA = new Item(itemAId, _companyId, "ITEM-FINAL", "Final Assembly", ItemType.Goods);
        var itemB = new Item(itemBId, _companyId, "ITEM-SEMI", "Semi-Finished Subassembly", ItemType.Goods);

        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, _companyId, "WO-SEMI-0001", itemAId, Guid.NewGuid(), 5m);

        var targetWh = Guid.NewGuid();
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, new DateTime(2026, 7, 10))
        {
            WorkOrderId = woId,
            ProcessLossQty = 2m
        };
        // Produces 3 units of Item B (not Item A), rate 50 -> total FG value = 150
        se.AddItem(itemBId, 3m, sourceWarehouseId: null, targetWarehouseId: targetWh, valuationRate: 50m, isFinishedItem: true);
        se.Submit();

        var stockEntries = new List<StockEntry> { se };
        _stockEntryRepo.WithDetailsAsync(Arg.Any<Expression<Func<StockEntry, object>>[]>())
            .Returns(Task.FromResult(stockEntries.AsQueryable()));

        _workOrderRepo.GetListAsync(Arg.Any<Expression<Func<WorkOrder, bool>>>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo }));

        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { itemA, itemB }));

        var request = new ProcessLossReportRequestDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        };

        var report = await _appService.GetReportAsync(request);

        report.Rows.Count.ShouldBe(1);
        var row = report.Rows[0];
        // ProducedQty only counts primary item (Item A) -> 0
        row.ProducedQty.ShouldBe(0m);
        // OutputQty counts all finished goods -> 3
        row.OutputQty.ShouldBe(3m);
        row.ProcessLossQty.ShouldBe(2m);
        row.TotalFgValue.ShouldBe(150m);

        // processedQty = 3 + 2 = 5
        // totalPlValue = 2 * 150 / 5 = 60
        row.TotalProcessLossValue.ShouldBe(60m);
    }

    [Fact]
    public async Task GetReportAsync_ExcludesWorkOrdersWithoutProcessLoss()
    {
        var itemId = Guid.NewGuid();
        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, _companyId, "WO-NOLOSS", itemId, Guid.NewGuid(), 10m);

        var targetWh = Guid.NewGuid();
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, new DateTime(2026, 8, 1))
        {
            WorkOrderId = woId,
            ProcessLossQty = 0m // Zero process loss
        };
        se.AddItem(itemId, 10m, sourceWarehouseId: null, targetWarehouseId: targetWh, valuationRate: 20m, isFinishedItem: true);
        se.Submit();

        var stockEntries = new List<StockEntry> { se };
        _stockEntryRepo.WithDetailsAsync(Arg.Any<Expression<Func<StockEntry, object>>[]>())
            .Returns(Task.FromResult(stockEntries.AsQueryable()));

        var request = new ProcessLossReportRequestDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        };

        var report = await _appService.GetReportAsync(request);

        // Having process_loss_qty > 0 excludes this work order
        report.Rows.ShouldBeEmpty();
        report.TotalProcessLossQty.ShouldBe(0m);
    }
}
