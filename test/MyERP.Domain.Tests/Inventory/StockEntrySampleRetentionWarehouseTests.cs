using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Inventory;

public class StockEntrySampleRetentionWarehouseTests
{
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private StockEntryManager CreateManager()
    {
        var restrictionEntryRepo = Substitute.For<IRepository<CompanyRestrictionEntry, Guid>>();
        var custRepo = Substitute.For<IRepository<MyERP.Sales.Entities.Customer, Guid>>();
        var suppRepo = Substitute.For<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
        var accRepo = Substitute.For<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
        var companyRestriction = new CompanyRestrictionValidationService(
            restrictionEntryRepo, _itemRepo, custRepo, suppRepo, accRepo, _warehouseRepo);
        return new StockEntryManager(_warehouseRepo, _itemRepo, companyRestriction, _companyRepo);
    }

    [Fact]
    public async Task ValidateWarehousesAsync_Throws_WhenSourceWarehouseIsSampleRetentionWarehouse()
    {
        var companyId = Guid.NewGuid();
        var sampleRetentionWhId = Guid.NewGuid();
        var targetWhId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var company = new Company(companyId, "Test Co")
        {
            SampleRetentionWarehouseId = sampleRetentionWhId
        };
        _companyRepo.FindAsync(companyId).Returns(company);

        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);
        entry.AddItem(itemId, 5, sampleRetentionWhId, targetWhId, 100);

        var ex = await Should.ThrowAsync<BusinessException>(() => manager.ValidateWarehousesAsync(entry));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.CannotConsumeFromSampleRetentionWarehouse);
        ex.Data["warehouseId"].ShouldBe(sampleRetentionWhId);
    }

    [Fact]
    public async Task ValidateWarehousesAsync_Allows_WhenTargetWarehouseIsSampleRetentionWarehouse()
    {
        var companyId = Guid.NewGuid();
        var sampleRetentionWhId = Guid.NewGuid();
        var sourceWhId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var company = new Company(companyId, "Test Co")
        {
            SampleRetentionWarehouseId = sampleRetentionWhId
        };
        _companyRepo.FindAsync(companyId).Returns(company);

        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);
        entry.AddItem(itemId, 5, sourceWhId, sampleRetentionWhId, 100);

        // Should not throw CannotConsumeFromSampleRetentionWarehouse
        await manager.ValidateWarehousesAsync(entry);
    }

    [Fact]
    public async Task ValidateWarehousesAsync_Allows_WhenSourceWarehouseIsNotSampleRetentionWarehouse()
    {
        var companyId = Guid.NewGuid();
        var sampleRetentionWhId = Guid.NewGuid();
        var sourceWhId = Guid.NewGuid();
        var targetWhId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var company = new Company(companyId, "Test Co")
        {
            SampleRetentionWarehouseId = sampleRetentionWhId
        };
        _companyRepo.FindAsync(companyId).Returns(company);

        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);
        entry.AddItem(itemId, 5, sourceWhId, targetWhId, 100);

        await manager.ValidateWarehousesAsync(entry);
    }

    [Fact]
    public async Task ValidateWarehousesAsync_Allows_WhenCompanyHasNoSampleRetentionWarehouse()
    {
        var companyId = Guid.NewGuid();
        var sourceWhId = Guid.NewGuid();
        var targetWhId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var company = new Company(companyId, "Test Co")
        {
            SampleRetentionWarehouseId = null
        };
        _companyRepo.FindAsync(companyId).Returns(company);

        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);
        entry.AddItem(itemId, 5, sourceWhId, targetWhId, 100);

        await manager.ValidateWarehousesAsync(entry);
    }
}
