using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Projects.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class DimensionWiseAccountsBalanceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IReportingAppService _reportingAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<FiscalYear, Guid> _fyRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<CostCenter, Guid> _ccRepository;
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<JournalEntry, Guid> _jeRepository;

    protected DimensionWiseAccountsBalanceTests()
    {
        _reportingAppService = GetRequiredService<IReportingAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _fyRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        _accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        _ccRepository = GetRequiredService<IRepository<CostCenter, Guid>>();
        _projectRepository = GetRequiredService<IRepository<Project, Guid>>();
        _jeRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
    }

    [Fact]
    public async Task Should_CrossTabulate_CostCenter_And_Project_And_Accumulate_Parents()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Dim Bal Co"), autoSave: true);
        var fy = await _fyRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)),
            autoSave: true);

        var parentExp = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "5000", "Expenses", AccountType.Expense) { IsGroup = true },
            autoSave: true);
        var travelExp = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "5100", "Travel", AccountType.Expense) { ParentAccountId = parentExp.Id },
            autoSave: true);
        var zeroExp = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "5200", "Unused Expense", AccountType.Expense) { ParentAccountId = parentExp.Id },
            autoSave: true);
        var bankAcc = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "1100", "Bank", AccountType.Asset),
            autoSave: true);

        var ccA = await _ccRepository.InsertAsync(
            new CostCenter(Guid.NewGuid(), company.Id, "Alpha CC"), autoSave: true);
        var ccB = await _ccRepository.InsertAsync(
            new CostCenter(Guid.NewGuid(), company.Id, "Beta CC"), autoSave: true);
        var proj1 = await _projectRepository.InsertAsync(
            new Project(Guid.NewGuid(), company.Id, "PRJ-DIM-1", "Project One"), autoSave: true);

        var je = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 6, 15));
        je.AddLine(travelExp.Id, 300m, true, costCenterId: ccA.Id, projectId: proj1.Id);
        je.AddLine(travelExp.Id, 200m, true, costCenterId: ccB.Id);
        je.AddLine(bankAcc.Id, 500m, false, costCenterId: ccA.Id);
        je.Post();
        await _jeRepository.InsertAsync(je, autoSave: true);

        // Period Closing voucher must be excluded
        var closingJe = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 12, 31))
        {
            VoucherType = JournalEntryVoucherType.PeriodClosing,
        };
        closingJe.AddLine(travelExp.Id, 999m, false, costCenterId: ccA.Id);
        closingJe.AddLine(bankAcc.Id, 999m, true, costCenterId: ccA.Id);
        closingJe.Post();
        await _jeRepository.InsertAsync(closingJe, autoSave: true);

        var ccReport = await _reportingAppService.GetDimensionWiseAccountsBalanceAsync(new DimensionWiseAccountsBalanceRequestDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            Dimension = "Cost Center",
            ShowZeroValues = false,
        });

        ccReport.Dimensions.Count.ShouldBe(2);
        ccReport.Rows.ShouldNotContain(r => r.AccountId == zeroExp.Id);
        var parentRow = ccReport.Rows.Single(r => r.AccountId == parentExp.Id);
        parentRow.IsGroup.ShouldBeTrue();
        parentRow.DimensionBalances.ShouldBe(new[] { 300m, 200m });
        parentRow.Total.ShouldBe(500m);

        var travelRow = ccReport.Rows.Single(r => r.AccountId == travelExp.Id);
        travelRow.Level.ShouldBe(1);
        travelRow.DimensionBalances.ShouldBe(new[] { 300m, 200m });

        // Bank (-500 in Alpha CC) + Travel (+300 in Alpha CC, +200 in Beta CC) => net totals [-200, +200], GrandTotal = 0
        ccReport.DimensionTotals.ShouldBe(new[] { -200m, 200m });
        ccReport.GrandTotal.ShouldBe(0m);

        var projReport = await _reportingAppService.GetDimensionWiseAccountsBalanceAsync(new DimensionWiseAccountsBalanceRequestDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            Dimension = "Project",
            ShowZeroValues = false,
        });
        projReport.Dimensions.Single().DimensionName.ShouldBe("Project One");
        projReport.Rows.Single(r => r.AccountId == travelExp.Id).Total.ShouldBe(300m);
        projReport.GrandTotal.ShouldBe(300m);
    }
}
