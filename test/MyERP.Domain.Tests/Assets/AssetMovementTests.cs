using System;
using MyERP.Assets.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Assets;

public class AssetMovementTests
{
    [Fact]
    public void Create_SetsDefaults()
    {
        var am = new AssetMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Transfer", DateTime.UtcNow);
        am.Status.ShouldBe(Core.DocumentStatus.Draft);
        am.MovementType.ShouldBe("Transfer");
    }

    [Fact]
    public void Submit_Succeeds()
    {
        var am = new AssetMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Transfer", DateTime.UtcNow);
        am.Submit();
        am.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Cancel_Submitted_Succeeds()
    {
        var am = new AssetMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Issue", DateTime.UtcNow);
        am.Submit();
        am.Cancel();
        am.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_Draft_Throws()
    {
        var am = new AssetMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Receipt", DateTime.UtcNow);
        Should.Throw<BusinessException>(() => am.Cancel());
    }
}
