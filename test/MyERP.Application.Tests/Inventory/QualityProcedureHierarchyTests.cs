using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

public abstract class QualityProcedureHierarchyTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IQualityManagementAppService _service;

    protected QualityProcedureHierarchyTests()
    {
        _service = GetRequiredService<IQualityManagementAppService>();
    }

    [Fact]
    public async Task CreateProcedureAsync_UnderNonGroupParent_PromotesParentToGroup()
    {
        var parent = await _service.CreateProcedureAsync(new CreateUpdateQualityProcedureDto
        {
            Name = "Incoming Inspection Root",
            IsGroup = false,
            Sequence = 1,
        });
        parent.IsGroup.ShouldBeFalse();

        var child = await _service.CreateProcedureAsync(new CreateUpdateQualityProcedureDto
        {
            Name = "Visual Check",
            ParentQualityProcedureId = parent.Id,
            IsGroup = false,
            Sequence = 1,
        });

        child.ParentQualityProcedureId.ShouldBe(parent.Id);
        var reloadedParent = await _service.GetProcedureAsync(parent.Id);
        reloadedParent.IsGroup.ShouldBeTrue();
        reloadedParent.Steps.ShouldContain(s => s.ChildProcedureId == child.Id);

        // Moving child to root removes the child step from the old parent
        await _service.UpdateProcedureAsync(child.Id, new CreateUpdateQualityProcedureDto
        {
            Name = "Visual Check",
            ParentQualityProcedureId = null,
            IsGroup = false,
            Sequence = 1,
        });

        var parentAfterUnlink = await _service.GetProcedureAsync(parent.Id);
        parentAfterUnlink.Steps.ShouldNotContain(s => s.ChildProcedureId == child.Id);
    }

    [Fact]
    public async Task UpdateProcedureAsync_SelfParent_ThrowsValidationException()
    {
        var proc = await _service.CreateProcedureAsync(new CreateUpdateQualityProcedureDto
        {
            Name = "Calibration Check",
            IsGroup = false,
            Sequence = 1,
        });

        await Should.ThrowAsync<BusinessException>(() =>
            _service.UpdateProcedureAsync(proc.Id, new CreateUpdateQualityProcedureDto
            {
                Name = "Calibration Check",
                ParentQualityProcedureId = proc.Id,
                IsGroup = false,
                Sequence = 1,
            }));
    }
}
