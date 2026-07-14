using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Accounting;

public class AccountingDimensionServiceTests
{
    private readonly IRepository<AccountingDimension, Guid> _dimensionRepo;
    private readonly IRepository<AccountingDimensionFilter, Guid> _filterRepo;
    private readonly IRepository<GlDimensionValue, Guid> _glDimValueRepo;
    private readonly AccountingDimensionService _service;

    public AccountingDimensionServiceTests()
    {
        _dimensionRepo = Substitute.For<IRepository<AccountingDimension, Guid>>();
        _filterRepo = Substitute.For<IRepository<AccountingDimensionFilter, Guid>>();
        _glDimValueRepo = Substitute.For<IRepository<GlDimensionValue, Guid>>();
        _service = new AccountingDimensionService(_dimensionRepo, _filterRepo, _glDimValueRepo);
    }

    [Fact]
    public async Task GetEnabledDimensions_ReturnsGlobalAndCompanySpecific()
    {
        var companyId = Guid.NewGuid();
        var globalDim = new AccountingDimension(Guid.NewGuid(), "Branch", "Branch");
        var companyDim = new AccountingDimension(Guid.NewGuid(), "Department", "Department") { CompanyId = companyId };
        var otherCompanyDim = new AccountingDimension(Guid.NewGuid(), "Region", "Region") { CompanyId = Guid.NewGuid() };

        _dimensionRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountingDimension, bool>>>())
            .Returns(new List<AccountingDimension> { globalDim, companyDim, otherCompanyDim });

        var result = await _service.GetEnabledDimensionsAsync(companyId);

        result.ShouldContain(globalDim);
        result.ShouldContain(companyDim);
        result.ShouldNotContain(otherCompanyDim);
    }

    [Fact]
    public async Task GetMandatoryDimensions_OnlyReturnsMandatory()
    {
        var mandatoryDim = new AccountingDimension(Guid.NewGuid(), "CostCenter", "Cost Center") { IsMandatory = true };
        var optionalDim = new AccountingDimension(Guid.NewGuid(), "Branch", "Branch") { IsMandatory = false };

        _dimensionRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountingDimension, bool>>>())
            .Returns(new List<AccountingDimension> { mandatoryDim, optionalDim });

        var result = await _service.GetMandatoryDimensionsAsync(null);

        result.Count.ShouldBe(1);
        result[0].DocumentType.ShouldBe("CostCenter");
    }

    [Fact]
    public async Task ValidateMandatoryDimensions_MissingDimension_Throws()
    {
        var companyId = Guid.NewGuid();
        var mandatoryDim = new AccountingDimension(Guid.NewGuid(), "Branch", "Branch") { IsMandatory = true };

        _dimensionRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountingDimension, bool>>>())
            .Returns(new List<AccountingDimension> { mandatoryDim });

        var lines = new List<JournalEntryLine>
        {
            new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true)
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ValidateMandatoryDimensionsAsync(companyId, lines, null));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.MandatoryDimensionMissing);
    }

    [Fact]
    public async Task ValidateMandatoryDimensions_CostCenterFilled_Passes()
    {
        var companyId = Guid.NewGuid();
        var costCenterDim = new AccountingDimension(Guid.NewGuid(), "CostCenter", "Cost Center") { IsMandatory = true };

        _dimensionRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountingDimension, bool>>>())
            .Returns(new List<AccountingDimension> { costCenterDim });

        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        line.CostCenterId = Guid.NewGuid(); // Filled!

        var lines = new List<JournalEntryLine> { line };

        // Should NOT throw
        await _service.ValidateMandatoryDimensionsAsync(companyId, lines, null);
    }

    [Fact]
    public async Task ValidateMandatoryDimensions_CustomDimensionFilled_Passes()
    {
        var companyId = Guid.NewGuid();
        var branchDim = new AccountingDimension(Guid.NewGuid(), "Branch", "Branch") { IsMandatory = true };

        _dimensionRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountingDimension, bool>>>())
            .Returns(new List<AccountingDimension> { branchDim });

        var lineId = Guid.NewGuid();
        var line = new JournalEntryLine(lineId, Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        var lines = new List<JournalEntryLine> { line };

        var dimValues = new List<GlDimensionValue>
        {
            new GlDimensionValue(Guid.NewGuid(), lineId, branchDim.Id, "branch_id", Guid.NewGuid())
        };

        // Should NOT throw
        await _service.ValidateMandatoryDimensionsAsync(companyId, lines, dimValues);
    }

    [Fact]
    public async Task ValidateDimensionFilters_AllowedValue_Passes()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var dimensionId = Guid.NewGuid();
        var allowedValueId = Guid.NewGuid();

        var filter = new AccountingDimensionFilter(Guid.NewGuid(), dimensionId, accountId, companyId, isAllowList: true);
        filter.DimensionValueIds = allowedValueId.ToString();

        _filterRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountingDimensionFilter, bool>>>())
            .Returns(new List<AccountingDimensionFilter> { filter });

        var lineId = Guid.NewGuid();
        var line = new JournalEntryLine(lineId, Guid.NewGuid(), accountId, 100m, true);
        var lines = new List<JournalEntryLine> { line };

        var dimValues = new List<GlDimensionValue>
        {
            new GlDimensionValue(Guid.NewGuid(), lineId, dimensionId, "branch_id", allowedValueId)
        };

        // Should NOT throw
        await _service.ValidateDimensionFiltersAsync(companyId, lines, dimValues);
    }

    [Fact]
    public async Task ValidateDimensionFilters_BlockedValue_Throws()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var dimensionId = Guid.NewGuid();
        var allowedValueId = Guid.NewGuid();
        var blockedValueId = Guid.NewGuid();

        var filter = new AccountingDimensionFilter(Guid.NewGuid(), dimensionId, accountId, companyId, isAllowList: true);
        filter.DimensionValueIds = allowedValueId.ToString(); // Only this one is allowed

        _filterRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountingDimensionFilter, bool>>>())
            .Returns(new List<AccountingDimensionFilter> { filter });

        var lineId = Guid.NewGuid();
        var line = new JournalEntryLine(lineId, Guid.NewGuid(), accountId, 100m, true);
        var lines = new List<JournalEntryLine> { line };

        var dimValues = new List<GlDimensionValue>
        {
            new GlDimensionValue(Guid.NewGuid(), lineId, dimensionId, "branch_id", blockedValueId) // NOT in allow list
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ValidateDimensionFiltersAsync(companyId, lines, dimValues));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.DimensionValueRestricted);
    }

    [Fact]
    public async Task ValidateDimensionFilters_NoFilters_AlwaysPasses()
    {
        var companyId = Guid.NewGuid();

        _filterRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountingDimensionFilter, bool>>>())
            .Returns(new List<AccountingDimensionFilter>());

        var lineId = Guid.NewGuid();
        var line = new JournalEntryLine(lineId, Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        var lines = new List<JournalEntryLine> { line };

        var dimValues = new List<GlDimensionValue>
        {
            new GlDimensionValue(Guid.NewGuid(), lineId, Guid.NewGuid(), "branch_id", Guid.NewGuid())
        };

        // Should NOT throw
        await _service.ValidateDimensionFiltersAsync(companyId, lines, dimValues);
    }

    [Fact]
    public async Task ValidateMandatoryDimensions_NoMandatoryDimensions_AlwaysPasses()
    {
        var companyId = Guid.NewGuid();

        _dimensionRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountingDimension, bool>>>())
            .Returns(new List<AccountingDimension>());

        var lines = new List<JournalEntryLine>
        {
            new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true)
        };

        // Should NOT throw (no mandatory dimensions configured)
        await _service.ValidateMandatoryDimensionsAsync(companyId, lines, null);
    }

    [Fact]
    public async Task ValidateMandatoryDimensions_ProjectIdFilled_Passes()
    {
        var companyId = Guid.NewGuid();
        var projectDim = new AccountingDimension(Guid.NewGuid(), "Project", "Project") { IsMandatory = true };

        _dimensionRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountingDimension, bool>>>())
            .Returns(new List<AccountingDimension> { projectDim });

        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        line.ProjectId = Guid.NewGuid(); // Filled!

        var lines = new List<JournalEntryLine> { line };

        // Should NOT throw
        await _service.ValidateMandatoryDimensionsAsync(companyId, lines, null);
    }

    [Fact]
    public async Task ValidateMandatoryDimensions_MultipleLinesFirstMissing_ThrowsWithLineIndex()
    {
        var companyId = Guid.NewGuid();
        var branchDim = new AccountingDimension(Guid.NewGuid(), "Branch", "Branch") { IsMandatory = true };

        _dimensionRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountingDimension, bool>>>())
            .Returns(new List<AccountingDimension> { branchDim });

        var line1Id = Guid.NewGuid();
        var line2Id = Guid.NewGuid();
        var line1 = new JournalEntryLine(line1Id, Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        var line2 = new JournalEntryLine(line2Id, Guid.NewGuid(), Guid.NewGuid(), 100m, false);
        var lines = new List<JournalEntryLine> { line1, line2 };

        // Only line2 has the dimension filled
        var dimValues = new List<GlDimensionValue>
        {
            new GlDimensionValue(Guid.NewGuid(), line2Id, branchDim.Id, "branch_id", Guid.NewGuid())
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ValidateMandatoryDimensionsAsync(companyId, lines, dimValues));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.MandatoryDimensionMissing);
    }
}
