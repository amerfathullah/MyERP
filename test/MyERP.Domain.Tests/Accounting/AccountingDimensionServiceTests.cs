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
    private readonly IRepository<Account, Guid> _accountRepo;
    private readonly IRepository<MyERP.Core.Entities.Company, Guid> _companyRepo;
    private readonly AccountingDimensionService _service;

    public AccountingDimensionServiceTests()
    {
        _dimensionRepo = Substitute.For<IRepository<AccountingDimension, Guid>>();
        _filterRepo = Substitute.For<IRepository<AccountingDimensionFilter, Guid>>();
        _glDimValueRepo = Substitute.For<IRepository<GlDimensionValue, Guid>>();
        _accountRepo = Substitute.For<IRepository<Account, Guid>>();
        _accountRepo.GetQueryableAsync().Returns(new List<Account>().AsQueryable());
        _companyRepo = Substitute.For<IRepository<MyERP.Core.Entities.Company, Guid>>();
        _companyRepo.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns((MyERP.Core.Entities.Company?)null);
        _service = new AccountingDimensionService(_dimensionRepo, _filterRepo, _glDimValueRepo, _accountRepo, _companyRepo);
    }

    private static Account MakeAccount(Guid id, AccountType type) =>
        new Account(id, Guid.NewGuid(), "1000", "Test Account", type);

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

    [Fact]
    public async Task ValidatePlAccountsHaveCostCenter_ExpenseAccountNoCostCenterNoCompanyDefault_Throws()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var expenseAccount = MakeAccount(accountId, AccountType.Expense);
        _accountRepo.GetQueryableAsync().Returns(new List<Account> { expenseAccount }.AsQueryable());

        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), accountId, 100m, true);
        var lines = new List<JournalEntryLine> { line };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ValidatePlAccountsHaveCostCenterAsync(companyId, lines));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.CostCenterRequiredForPlAccount);
    }

    [Fact]
    public async Task ValidatePlAccountsHaveCostCenter_ExpenseAccountNoCostCenterButCompanyDefault_AutoFillsAndPasses()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var defaultCostCenterId = Guid.NewGuid();
        var expenseAccount = MakeAccount(accountId, AccountType.Expense);
        _accountRepo.GetQueryableAsync().Returns(new List<Account> { expenseAccount }.AsQueryable());

        var company = new MyERP.Core.Entities.Company(companyId, "Test Co")
        {
            DefaultCostCenterId = defaultCostCenterId,
        };
        _companyRepo.FindAsync(companyId, Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(company);

        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), accountId, 100m, true);
        var lines = new List<JournalEntryLine> { line };

        await _service.ValidatePlAccountsHaveCostCenterAsync(companyId, lines);

        line.CostCenterId.ShouldBe(defaultCostCenterId);
    }

    [Fact]
    public async Task ValidatePlAccountsHaveCostCenter_RevenueAccountWithCostCenter_Passes()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var revenueAccount = MakeAccount(accountId, AccountType.Revenue);
        _accountRepo.GetQueryableAsync().Returns(new List<Account> { revenueAccount }.AsQueryable());

        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), accountId, 100m, false);
        line.CostCenterId = Guid.NewGuid();
        var lines = new List<JournalEntryLine> { line };

        await _service.ValidatePlAccountsHaveCostCenterAsync(companyId, lines);
    }

    [Fact]
    public async Task ValidatePlAccountsHaveCostCenter_BalanceSheetAccountNoCostCenter_Passes()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var assetAccount = MakeAccount(accountId, AccountType.Asset);
        _accountRepo.GetQueryableAsync().Returns(new List<Account> { assetAccount }.AsQueryable());

        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), accountId, 100m, true);
        var lines = new List<JournalEntryLine> { line };

        await _service.ValidatePlAccountsHaveCostCenterAsync(companyId, lines);
    }

    [Fact]
    public async Task ValidateMandatoryDimensions_AlsoEnforcesPlCostCenter()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var expenseAccount = MakeAccount(accountId, AccountType.Expense);
        _accountRepo.GetQueryableAsync().Returns(new List<Account> { expenseAccount }.AsQueryable());
        _dimensionRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountingDimension, bool>>>())
            .Returns(new List<AccountingDimension>());

        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), accountId, 100m, true);
        var lines = new List<JournalEntryLine> { line };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ValidateMandatoryDimensionsAsync(companyId, lines, null));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.CostCenterRequiredForPlAccount);
    }

    [Fact]
    public void ChildTable_NotAllowed_AsAccountingDimension_Throws()
    {
        var ex = Assert.Throws<BusinessException>(() =>
            new AccountingDimension(Guid.NewGuid(), "SalesOrderItem", "Sales Order Item"));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }
}
