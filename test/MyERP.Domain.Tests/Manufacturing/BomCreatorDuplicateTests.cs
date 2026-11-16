using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class BomCreatorDuplicateTests
{
    [Fact]
    public void Validate_DuplicateItemUnderSameParent_ThrowsValidationFailed()
    {
        var fgItemId = Guid.NewGuid();
        var creator = new BomCreator(Guid.NewGuid(), Guid.NewGuid(), fgItemId, 1);

        var componentId = Guid.NewGuid();

        // Add component once under FG
        creator.AddItem(componentId, "Raw Material A", fgItemId, 2, 10);

        // Add same component again under same FG (per ERPNext PR #58614)
        creator.AddItem(componentId, "Raw Material A", fgItemId, 3, 10);

        var ex = Should.Throw<BusinessException>(() => creator.Validate());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Validate_SameItemUnderDifferentParents_Succeeds()
    {
        var topFgId = Guid.NewGuid();
        var subFgId = Guid.NewGuid();
        var creator = new BomCreator(Guid.NewGuid(), Guid.NewGuid(), topFgId, 1);

        var commonNutId = Guid.NewGuid();

        // Add common nut under top FG
        creator.AddItem(commonNutId, "Nut M6", topFgId, 4, 0.5m);

        // Add common nut under sub-assembly FG (valid multi-level reuse)
        creator.AddItem(commonNutId, "Nut M6", subFgId, 2, 0.5m);

        Should.NotThrow(() => creator.Validate());
    }

    [Fact]
    public async Task CreateBomsAsync_ExceedsMaxDesignParts_ThrowsValidationFailed()
    {
        var fgItemId = Guid.NewGuid();
        var subId = Guid.NewGuid();
        var leafId1 = Guid.NewGuid();
        var leafId2 = Guid.NewGuid();
        var creator = new BomCreator(Guid.NewGuid(), Guid.NewGuid(), fgItemId, 1m);

        creator.AddItem(subId, "Sub Assembly", fgItemId, 1m, 0m, isExpandable: true);
        creator.AddItem(leafId1, "Leaf 1", subId, 1m, 5m);
        creator.AddItem(leafId2, "Leaf 2", subId, 1m, 5m);

        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var service = new BomCreatorService(bomRepo);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            service.CreateBomsAsync(creator, _ => Task.FromResult("BOM-001"), maxParts: 3));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        creator.Status.ShouldBe(BomCreatorStatus.Failed);
    }
}
