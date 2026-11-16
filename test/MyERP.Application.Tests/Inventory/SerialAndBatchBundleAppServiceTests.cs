using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Inventory;

public class SerialAndBatchBundleAppServiceTests
{
    [Fact]
    public async Task GetListAndGetAsync_IncludeEntriesWithDetails()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();

        var bundle = new SerialAndBatchBundle(
            bundleId,
            companyId,
            itemId,
            warehouseId,
            BundleTransactionType.Inward,
            "Stock Entry",
            Guid.NewGuid(),
            new DateTime(2026, 11, 10));
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 1m, 250m, "SN-001"));
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 1m, 250m, "SN-002"));

        var bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
        bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { bundle }.AsQueryable()));

        var item = new Item(itemId, companyId, "LAP-01", "Laptop", ItemType.Goods);
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));
        itemRepo.FindAsync(itemId, Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult<Item?>(item));

        var service = new SerialAndBatchBundleAppService(bundleRepo, itemRepo);

        var listResult = await service.GetListAsync(new GetBundleListDto { CompanyId = companyId });
        listResult.TotalCount.ShouldBe(1);
        listResult.Items[0].ItemName.ShouldBe("Laptop");
        listResult.Items[0].EntryCount.ShouldBe(2);

        var detailResult = await service.GetAsync(bundleId);
        detailResult.EntryCount.ShouldBe(2);
        detailResult.Entries.ShouldNotBeNull();
        detailResult.Entries!.Count.ShouldBe(2);
        detailResult.Entries[0].SerialNo.ShouldBe("SN-001");
        detailResult.Entries[0].Rate.ShouldBe(250m);
    }
}
