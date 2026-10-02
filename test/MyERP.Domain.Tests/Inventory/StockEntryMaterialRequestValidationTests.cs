using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Inventory;

public class StockEntryMaterialRequestValidationTests
{
    private readonly StockEntryManager _manager;

    public StockEntryMaterialRequestValidationTests()
    {
        _manager = new StockEntryManager(null!, null!, null!);
    }

    [Fact]
    public async Task ValidateMaterialRequestItemsAsync_NoLinkedRows_Succeeds()
    {
        var companyId = Guid.NewGuid();
        var se = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);
        se.AddItem(Guid.NewGuid(), 5m, Guid.NewGuid(), Guid.NewGuid());

        var repo = Substitute.For<IRepository<MaterialRequest, Guid>>();

        await Should.NotThrowAsync(() => _manager.ValidateMaterialRequestItemsAsync(se, repo));
    }

    [Fact]
    public async Task ValidateMaterialRequestItemsAsync_MatchingRow_Succeeds()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var mr = new MaterialRequest(Guid.NewGuid(), companyId, "MR-001", MaterialRequestType.MaterialTransfer, DateTime.UtcNow);
        mr.AddItem(itemId, "Item 1", 10m, "Unit");
        var mrItemId = mr.Items[0].Id;

        var repo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        repo.GetQueryableAsync().Returns(Task.FromResult(new List<MaterialRequest> { mr }.AsQueryable()));

        var se = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);
        se.AddItem(itemId, 5m, Guid.NewGuid(), Guid.NewGuid());
        se.Items[0].MaterialRequestItemId = mrItemId;

        await Should.NotThrowAsync(() => _manager.ValidateMaterialRequestItemsAsync(se, repo));
    }

    [Fact]
    public async Task ValidateMaterialRequestItemsAsync_UnlinkedRowBeforeMismatchedRow_CatchesMismatch()
    {
        // Per ERPNext PR #59679 / commit bd4bf5ee25:
        // An unlinked row must NOT return early and skip validation of subsequent linked rows.
        var companyId = Guid.NewGuid();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var item3Id = Guid.NewGuid();

        var mr = new MaterialRequest(Guid.NewGuid(), companyId, "MR-001", MaterialRequestType.MaterialTransfer, DateTime.UtcNow);
        mr.AddItem(item2Id, "Item 2", 10m, "Unit");
        var mrItemId = mr.Items[0].Id;

        var repo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        repo.GetQueryableAsync().Returns(Task.FromResult(new List<MaterialRequest> { mr }.AsQueryable()));

        var se = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);
        // Row 0 is unlinked
        se.AddItem(item1Id, 5m, Guid.NewGuid(), Guid.NewGuid());
        // Row 1 is linked to mrItemId (which is item2Id), but row specifies item3Id
        se.AddItem(item3Id, 3m, Guid.NewGuid(), Guid.NewGuid());
        se.Items[1].MaterialRequestItemId = mrItemId;

        var ex = await Should.ThrowAsync<BusinessException>(() => _manager.ValidateMaterialRequestItemsAsync(se, repo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task ValidateMaterialRequestItemsAsync_MissingMrItem_ThrowsNotFound()
    {
        var companyId = Guid.NewGuid();
        var repo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        repo.GetQueryableAsync().Returns(Task.FromResult(new List<MaterialRequest>().AsQueryable()));

        var se = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);
        se.AddItem(Guid.NewGuid(), 5m, Guid.NewGuid(), Guid.NewGuid());
        se.Items[0].MaterialRequestItemId = Guid.NewGuid();

        var ex = await Should.ThrowAsync<BusinessException>(() => _manager.ValidateMaterialRequestItemsAsync(se, repo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.MaterialRequestItemNotFound);
    }
}
