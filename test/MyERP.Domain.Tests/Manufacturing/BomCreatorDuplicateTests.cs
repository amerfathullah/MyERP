using System;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
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
}
