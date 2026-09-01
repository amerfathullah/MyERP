using System;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Unit tests for Batch and Stock Entry invariants:
/// - Batch auto-derives ManufacturingDate from reference doc PostingDate if unset (Gotcha #242)
/// - ExpiryDate is derived from ManufacturingDate + ShelfLifeInDays
/// - StockEntry has IsAdditionalTransferEntry flag for tracking excess WO material transfers (Gotcha #179)
/// </summary>
public class BatchAndStockEntryInvariantTests
{
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void Batch_DerivesManufacturingDate_FromReferenceDocPostingDate_WhenUnset()
    {
        var postingDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        var batch = new Batch(Guid.NewGuid(), _itemId, "BATCH-2026-001")
        {
            ShelfLifeInDays = 30
        };

        batch.DeriveManufacturingDateAndExpiry(postingDate);

        Assert.Equal(postingDate, batch.ManufacturingDate);
        Assert.Equal(postingDate.AddDays(30), batch.ExpiryDate);
    }

    [Fact]
    public void Batch_PreservesExplicitManufacturingDate_OverReferenceDocPostingDate()
    {
        var explicitMfgDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var postingDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        var batch = new Batch(Guid.NewGuid(), _itemId, "BATCH-2026-002")
        {
            ManufacturingDate = explicitMfgDate,
            ShelfLifeInDays = 60
        };

        batch.DeriveManufacturingDateAndExpiry(postingDate);

        Assert.Equal(explicitMfgDate, batch.ManufacturingDate);
        Assert.Equal(explicitMfgDate.AddDays(60), batch.ExpiryDate);
    }

    [Fact]
    public void StockEntry_IsAdditionalTransferEntry_DefaultsFalse_AndCanBeSet()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);

        Assert.False(se.IsAdditionalTransferEntry);

        se.IsAdditionalTransferEntry = true;
        Assert.True(se.IsAdditionalTransferEntry);
    }

    [Fact]
    public void StockEntry_IsFgConversion_DefaultsFalse_AndCanBeSet()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow);

        Assert.False(se.IsFgConversion);

        se.IsFgConversion = true;
        Assert.True(se.IsFgConversion);
    }

    [Fact]
    public void Batch_ParentBatchId_CanBeSet()
    {
        var parentId = Guid.NewGuid();
        var child = new Batch(Guid.NewGuid(), _itemId, "BATCH-CHILD-001")
        {
            ParentBatchId = parentId
        };

        Assert.Equal(parentId, child.ParentBatchId);
    }

    [Fact]
    public void StockEntry_WeightPerPiece_DefaultsZero_CanBeSet()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow);

        Assert.Equal(0m, se.WeightPerPiece);

        se.WeightPerPiece = 10m;
        Assert.Equal(10m, se.WeightPerPiece);
    }

    [Fact]
    public void ValidateBatchSplit_Throws_WhenNonRepack()
    {
        var manager = new MyERP.Inventory.DomainServices.StockEntryManager(
            NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Warehouse, Guid>>(),
            NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Item, Guid>>(),
            new MyERP.Core.DomainServices.CompanyRestrictionValidationService(
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.CompanyRestrictionEntry, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Item, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.Customer, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Accounting.Entities.Account, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Warehouse, Guid>>()));

        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WeightPerPiece = 10m
        };

        var ex = Assert.Throws<Volo.Abp.BusinessException>(() => manager.ValidateBatchSplit(se));
        Assert.Contains("Repack", ex.Data["detail"]!.ToString());
    }

    [Fact]
    public void ValidateBatchSplit_Throws_WhenMultiRawMaterials()
    {
        var manager = new MyERP.Inventory.DomainServices.StockEntryManager(
            NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Warehouse, Guid>>(),
            NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Item, Guid>>(),
            new MyERP.Core.DomainServices.CompanyRestrictionValidationService(
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.CompanyRestrictionEntry, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Item, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.Customer, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Accounting.Entities.Account, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Warehouse, Guid>>()));

        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            WeightPerPiece = 10m
        };
        se.AddItem(Guid.NewGuid(), 10m, Guid.NewGuid(), null);
        se.AddItem(Guid.NewGuid(), 10m, Guid.NewGuid(), null); // 2 distinct RM items
        se.AddItem(Guid.NewGuid(), 20m, null, Guid.NewGuid(), isFinishedItem: true);

        var ex = Assert.Throws<Volo.Abp.BusinessException>(() => manager.ValidateBatchSplit(se));
        Assert.Contains("exactly one raw material item type", ex.Data["detail"]!.ToString());
    }

    [Fact]
    public void ValidateBatchSplit_Throws_WhenNotWholePieceCapacity()
    {
        var manager = new MyERP.Inventory.DomainServices.StockEntryManager(
            NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Warehouse, Guid>>(),
            NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Item, Guid>>(),
            new MyERP.Core.DomainServices.CompanyRestrictionValidationService(
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.CompanyRestrictionEntry, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Item, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.Customer, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Accounting.Entities.Account, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Warehouse, Guid>>()));

        var rmId = Guid.NewGuid();
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            WeightPerPiece = 10m
        };
        se.AddItem(rmId, 25m, Guid.NewGuid(), null); // 25 is not divisible by 10
        se.AddItem(Guid.NewGuid(), 25m, null, Guid.NewGuid(), isFinishedItem: true);

        var ex = Assert.Throws<Volo.Abp.BusinessException>(() => manager.ValidateBatchSplit(se));
        Assert.Contains("must be an exact multiple", ex.Data["detail"]!.ToString());
    }

    [Fact]
    public void ValidateBatchSplit_Succeeds_WhenValid()
    {
        var manager = new MyERP.Inventory.DomainServices.StockEntryManager(
            NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Warehouse, Guid>>(),
            NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Item, Guid>>(),
            new MyERP.Core.DomainServices.CompanyRestrictionValidationService(
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.CompanyRestrictionEntry, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Item, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.Customer, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Accounting.Entities.Account, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Warehouse, Guid>>()));

        var rmId = Guid.NewGuid();
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            WeightPerPiece = 10m
        };
        se.AddItem(rmId, 50m, Guid.NewGuid(), null);
        se.AddItem(Guid.NewGuid(), 50m, null, Guid.NewGuid(), isFinishedItem: true);

        manager.ValidateBatchSplit(se);
    }

    [Fact]
    public void CalculateMaterialCoverage_CapsToMinimumCoverageAcrossRawMaterials()
    {
        var manager = new MyERP.Inventory.DomainServices.StockEntryManager(
            NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Warehouse, Guid>>(),
            NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Item, Guid>>(),
            new MyERP.Core.DomainServices.CompanyRestrictionValidationService(
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.CompanyRestrictionEntry, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Item, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.Customer, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Accounting.Entities.Account, Guid>>(),
                NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Warehouse, Guid>>()));

        var rm1 = Guid.NewGuid();
        var rm2 = Guid.NewGuid();

        var required = new System.Collections.Generic.Dictionary<Guid, decimal>
        {
            [rm1] = 10m,
            [rm2] = 20m
        };

        // Half of rm1 transferred (5/10 = 50%), full rm2 transferred (20/20 = 100%)
        // Target FG qty = 10 -> min coverage = 50% * 10 = 5
        var transferred = new System.Collections.Generic.Dictionary<Guid, decimal>
        {
            [rm1] = 5m,
            [rm2] = 20m
        };

        var coveredFgQty = manager.CalculateMaterialCoverage(10m, required, transferred);
        Assert.Equal(5m, coveredFgQty);
    }
}
