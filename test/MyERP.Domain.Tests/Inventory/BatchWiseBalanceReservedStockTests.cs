using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class BatchWiseBalanceReservedStockTests
{
    [Fact]
    public void BatchWiseBalanceDtos_SupportReservedStockFields()
    {
        var row = new BatchWiseBalanceRowDto
        {
            ItemId = Guid.NewGuid(),
            BatchId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            Balance = 100m,
            StockValue = 1000m,
            ReservedStockQty = 25m,
        };

        var report = new BatchWiseBalanceReportDto
        {
            Rows = new List<BatchWiseBalanceRowDto> { row },
            TotalBatches = 1,
            TotalQuantity = 100m,
            TotalStockValue = 1000m,
            TotalReservedStock = 25m,
        };

        Assert.Equal(25m, row.ReservedStockQty);
        Assert.Equal(25m, report.TotalReservedStock);
    }

    [Fact]
    public void GetBatchWiseBalanceRequestDto_SupportsBatchAndCompanyFilters()
    {
        var companyId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var request = new GetBatchWiseBalanceRequestDto
        {
            CompanyId = companyId,
            BatchId = batchId,
        };

        Assert.Equal(companyId, request.CompanyId);
        Assert.Equal(batchId, request.BatchId);
    }

    [Fact]
    public async Task GetBatchWiseBalanceAsync_ComputesReservedStock_FromActiveReservations()
    {
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var batchRepo = Substitute.For<IRepository<Batch, Guid>>();
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        var binService = new BinService(binRepo);

        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var item = new Item(itemId, companyId, "ITEM-001", "Test Item", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var batch = new Batch(batchId, itemId, "BATCH-001");
        batchRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Batch> { batch }.AsQueryable()));

        var warehouse = new Warehouse(whId, companyId, "Stores");
        whRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var sleList = new List<StockLedgerEntry>
        {
            new StockLedgerEntry(Guid.NewGuid(), companyId, itemId, whId, DateTime.UtcNow, 50m, 10m, 50m, 500m)
            {
                BatchId = batchId,
            }
        };
        sleRepo.GetQueryableAsync().Returns(Task.FromResult(sleList.AsQueryable()));

        // SRE with 15 reserved, 5 already delivered => 10 remaining active reserved stock
        var sre = new StockReservationEntry(
            Guid.NewGuid(), companyId, itemId, whId, "SalesOrder", Guid.NewGuid(), 15m)
        {
            BatchId = batchId,
        };
        sre.Submit();
        sre.RecordDelivery(5m);

        sreRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockReservationEntry> { sre }.AsQueryable()));

        var appService = new StockBalanceAppService(
            binRepo, itemRepo, whRepo, sleRepo, batchRepo, binService);

        var lazyProvider = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazyProvider.LazyGetService<IRepository<StockReservationEntry, Guid>>().Returns(sreRepo);
        appService.LazyServiceProvider = lazyProvider;

        var result = await appService.GetBatchWiseBalanceAsync(new GetBatchWiseBalanceRequestDto
        {
            CompanyId = companyId,
            ItemId = itemId,
            WarehouseId = whId,
        });

        Assert.Single(result.Rows);
        var row = result.Rows[0];
        Assert.Equal(50m, row.Balance);
        Assert.Equal(10m, row.ReservedStockQty);
        Assert.Equal(10m, result.TotalReservedStock);
        Assert.Equal(10m, row.ValuationRate);
    }

    [Fact]
    public async Task GetBatchWiseBalanceAsync_ResolvesSerialNosAndValuationRate()
    {
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var batchRepo = Substitute.For<IRepository<Batch, Guid>>();
        var serialRepo = Substitute.For<IRepository<SerialNo, Guid>>();
        var binService = new BinService(binRepo);

        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var item = new Item(itemId, companyId, "ITEM-SERIAL", "Serialized Item", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var batch = new Batch(batchId, itemId, "BATCH-002");
        batchRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Batch> { batch }.AsQueryable()));

        var warehouse = new Warehouse(whId, companyId, "Stores");
        whRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var sleList = new List<StockLedgerEntry>
        {
            new StockLedgerEntry(Guid.NewGuid(), companyId, itemId, whId, DateTime.UtcNow, 2m, 150m, 2m, 300m)
            {
                BatchId = batchId,
            }
        };
        sleRepo.GetQueryableAsync().Returns(Task.FromResult(sleList.AsQueryable()));

        var sn1 = new SerialNo(Guid.NewGuid(), itemId, "SN-001", companyId, whId) { BatchId = batchId, Status = SerialNoStatus.Active };
        var sn2 = new SerialNo(Guid.NewGuid(), itemId, "SN-002", companyId, whId) { BatchId = batchId, Status = SerialNoStatus.Active };

        serialRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SerialNo> { sn1, sn2 }.AsQueryable()));

        var appService = new StockBalanceAppService(binRepo, itemRepo, whRepo, sleRepo, batchRepo, binService);
        var lazyProvider = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazyProvider.LazyGetService<IRepository<SerialNo, Guid>>().Returns(serialRepo);
        appService.LazyServiceProvider = lazyProvider;

        var result = await appService.GetBatchWiseBalanceAsync(new GetBatchWiseBalanceRequestDto
        {
            CompanyId = companyId,
            ItemId = itemId,
            WarehouseId = whId,
        });

        Assert.Single(result.Rows);
        var row = result.Rows[0];
        Assert.Equal(2m, row.Balance);
        Assert.Equal(300m, row.StockValue);
        Assert.Equal(150m, row.ValuationRate);
        Assert.Equal("SN-001, SN-002", row.SerialNos);
    }
}

