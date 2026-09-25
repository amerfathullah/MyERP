using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class SplitJobCardSecondaryItemAndPhantomBomTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fgItemId = Guid.NewGuid();
    private readonly Guid _scrapItemId = Guid.NewGuid();
    private readonly Guid _coProductItemId = Guid.NewGuid();
    private readonly Guid _rawMaterialId = Guid.NewGuid();
    private readonly Guid _kitItemId = Guid.NewGuid();

    // =========================================================================
    // ERPNext PR #59436 / commit 1d1562a68e:
    // Secondary items across split job cards
    // =========================================================================

    [Fact]
    public void PopulateSecondaryItemsFromBom_DistributesProportionallyAcrossSplitJobCards()
    {
        // BOM: Quantity = 10
        // Secondary items:
        // - Scrap: Quantity = 2 (0.2 per FG)
        // - CoProduct: Quantity = 1 (0.1 per FG)
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-TEST-001", _fgItemId)
        {
            Quantity = 10m
        };
        bom.AddSecondaryItem(new BomSecondaryItem(Guid.NewGuid(), bom.Id, _scrapItemId, SecondaryItemType.Scrap, 2m)
        {
            ItemName = "Scrap Item",
            Rate = 5m
        });
        bom.AddSecondaryItem(new BomSecondaryItem(Guid.NewGuid(), bom.Id, _coProductItemId, SecondaryItemType.CoProduct, 1m)
        {
            ItemName = "CoProduct Item",
            Rate = 10m
        });

        var woId = Guid.NewGuid();
        var opId = Guid.NewGuid();

        // Job Card 1 (first split): for 4 units
        var jc1 = new JobCard(Guid.NewGuid(), _companyId, woId, opId, 4m, 1);
        JobCardManager.PopulateSecondaryItemsFromBom(jc1, bom, jc1.ForQuantity);

        jc1.SecondaryItems.Count.ShouldBe(2);
        var jc1Scrap = jc1.SecondaryItems.First(s => s.ItemId == _scrapItemId);
        var jc1CoProduct = jc1.SecondaryItems.First(s => s.ItemId == _coProductItemId);
        jc1Scrap.StockQty.ShouldBe(0.8m); // (2 / 10) * 4
        jc1CoProduct.StockQty.ShouldBe(0.4m); // (1 / 10) * 4

        // Job Card 2 (second split): for 6 units
        var jc2 = new JobCard(Guid.NewGuid(), _companyId, woId, opId, 6m, 2);
        JobCardManager.PopulateSecondaryItemsFromBom(jc2, bom, jc2.ForQuantity);

        jc2.SecondaryItems.Count.ShouldBe(2);
        var jc2Scrap = jc2.SecondaryItems.First(s => s.ItemId == _scrapItemId);
        var jc2CoProduct = jc2.SecondaryItems.First(s => s.ItemId == _coProductItemId);
        jc2Scrap.StockQty.ShouldBe(1.2m); // (2 / 10) * 6
        jc2CoProduct.StockQty.ShouldBe(0.6m); // (1 / 10) * 6

        // Total across split job cards matches total BOM output
        (jc1Scrap.StockQty + jc2Scrap.StockQty).ShouldBe(2.0m);
        (jc1CoProduct.StockQty + jc2CoProduct.StockQty).ShouldBe(1.0m);
    }

    [Fact]
    public async Task CreateJobCardsFromWorkOrderAsync_PopulatesSecondaryItemsOnAllSplitCards()
    {
        var jcRepo = Substitute.For<IRepository<JobCard, Guid>>();
        var wsRepo = Substitute.For<IRepository<Workstation, Guid>>();
        var manager = new JobCardManager(jcRepo, wsRepo);

        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-001", _fgItemId)
        {
            Quantity = 1m
        };
        bom.AddSecondaryItem(new BomSecondaryItem(Guid.NewGuid(), bom.Id, _scrapItemId, SecondaryItemType.Scrap, 2m)
        {
            ItemName = "Scrap Item",
            Rate = 5m
        });

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _fgItemId, bom.Id, 10m);
        var routing = new Routing(Guid.NewGuid(), "Routing 1");
        routing.AddOperation(Guid.NewGuid(), sequenceId: 1, timeInMins: 30m);
        routing.Operations[0].BatchSize = 5m; // Splits into 2 job cards of 5 each

        var jobCards = await manager.CreateJobCardsFromWorkOrderAsync(wo, routing, bom: bom);

        jobCards.Length.ShouldBe(2);
        jobCards[0].ForQuantity.ShouldBe(5m);
        jobCards[1].ForQuantity.ShouldBe(5m);

        // Each split Job Card gets 10 scrap: (2 / 1) * 5
        jobCards[0].SecondaryItems.Count.ShouldBe(1);
        jobCards[0].SecondaryItems[0].StockQty.ShouldBe(10m);

        jobCards[1].SecondaryItems.Count.ShouldBe(1);
        jobCards[1].SecondaryItems[0].StockQty.ShouldBe(10m);
    }

    // =========================================================================
    // ERPNext PR #59445 / commit fd8e6230f3:
    // Explode phantom BOM rows by their stock qty
    // =========================================================================

    [Fact]
    public async Task SubcontractingRmTransferService_ExplodesDirectBomItemByStockQty()
    {
        var scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var service = new SubcontractingRmTransferService(scoRepo, bomRepo);

        var scoId = Guid.NewGuid();
        var bomId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var sco = new SubcontractingOrder(scoId, _companyId, "SCO-001", DateTime.UtcNow, supplierId);
        var scoItem = new SubcontractingOrderItem(Guid.NewGuid(), scoId, _fgItemId, "Finished Good", 2m, 100m)
        {
            WarehouseId = whId,
            BomId = bomId
        };
        sco.AddItem(scoItem);

        scoRepo.WithDetailsAsync().Returns(new[] { sco }.AsQueryable());

        // BOM has 1 raw material row: Quantity = 2 Box, ConversionFactor = 5 (StockQty = 10 Nos)
        var bom = new BillOfMaterials(bomId, _companyId, "BOM-001", _fgItemId)
        {
            Quantity = 1m
        };
        bom.AddItem(new BomItem(Guid.NewGuid(), bomId, _rawMaterialId, "Raw Material", quantity: 2m, rate: 10m, uom: "Box", conversionFactor: 5m, stockUom: "Unit"));
        bomRepo.GetAsync(bomId).Returns(bom);

        var requirements = await service.CalculateRmRequirementsAsync(scoId);

        requirements.Count.ShouldBe(1);
        // RequiredQty = (StockQty (10) / bomQty (1)) * scoQty (2) = 20 Nos
        requirements[0].RequiredQty.ShouldBe(20m);
        requirements[0].ItemId.ShouldBe(_rawMaterialId);
    }

    [Fact]
    public async Task SubcontractingRmTransferService_ExplodesPhantomBomByStockQty()
    {
        var scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var service = new SubcontractingRmTransferService(scoRepo, bomRepo);

        var scoId = Guid.NewGuid();
        var mainBomId = Guid.NewGuid();
        var phantomBomId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var sco = new SubcontractingOrder(scoId, _companyId, "SCO-001", DateTime.UtcNow, supplierId);
        var scoItem = new SubcontractingOrderItem(Guid.NewGuid(), scoId, _fgItemId, "Finished Good", 1m, 100m)
        {
            WarehouseId = whId,
            BomId = mainBomId
        };
        sco.AddItem(scoItem);

        scoRepo.WithDetailsAsync().Returns(new[] { sco }.AsQueryable());

        // Phantom sub-BOM: producing 1 Kit consumes 1 RM (StockQty = 1)
        var phantomBom = new BillOfMaterials(phantomBomId, _companyId, "BOM-KIT", _kitItemId)
        {
            Quantity = 1m
        };
        phantomBom.AddItem(new BomItem(Guid.NewGuid(), phantomBomId, _rawMaterialId, "Raw Material", quantity: 1m, rate: 10m, uom: "Unit"));

        // Main BOM: producing 1 FG consumes 2 Box of Kit with ConversionFactor = 5 (StockQty = 10)
        var mainBom = new BillOfMaterials(mainBomId, _companyId, "BOM-MAIN", _fgItemId)
        {
            Quantity = 1m
        };
        var kitBomItem = new BomItem(Guid.NewGuid(), mainBomId, _kitItemId, "Kit Item", quantity: 2m, rate: 50m, uom: "Box", conversionFactor: 5m, stockUom: "Unit")
        {
            SubBomId = phantomBomId,
            IsPhantom = true
        };
        mainBom.AddItem(kitBomItem);

        bomRepo.GetAsync(mainBomId).Returns(mainBom);
        bomRepo.GetAsync(phantomBomId).Returns(phantomBom);

        var requirements = await service.CalculateRmRequirementsAsync(scoId);

        requirements.Count.ShouldBe(1);
        // Kit StockQty = 2 Box * 5 = 10 Nos.
        // Phantom explosion: 10 Kit * 1 RM = 10 RM
        // Previously without stock_qty fix, it would have taken Quantity (2) instead of StockQty (10)!
        requirements[0].RequiredQty.ShouldBe(10m);
        requirements[0].ItemId.ShouldBe(_rawMaterialId);
    }
}
