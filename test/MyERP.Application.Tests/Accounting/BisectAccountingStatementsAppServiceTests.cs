using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class BisectAccountingStatementsAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IBisectAccountingStatementsAppService _appService;
    private readonly IRepository<Company, Guid> _companyRepository;

    protected BisectAccountingStatementsAppServiceTests()
    {
        _appService = GetRequiredService<IBisectAccountingStatementsAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
    }

    [Fact]
    public async Task BisectAccountingStatements_Should_BuildTree_And_Navigate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = new Company(Guid.NewGuid(), "Bisect Test Co");
            await _companyRepository.InsertAsync(company);

            var created = await _appService.CreateAndBuildTreeAsync(new CreateBisectAccountingStatementsDto
            {
                CompanyId = company.Id,
                FromDate = new DateTime(2026, 1, 1),
                ToDate = new DateTime(2026, 1, 4),
                Algorithm = BisectAlgorithm.BFS,
            });

            created.ShouldNotBeNull();
            created.CompanyName.ShouldBe("Bisect Test Co");
            created.Nodes.Count.ShouldBeGreaterThan(1);
            created.CurrentNodeId.ShouldNotBeNull();

            // Navigate left
            var leftResult = await _appService.BisectLeftAsync(created.Id);
            leftResult.CurrentNodeId.ShouldNotBe(created.CurrentNodeId);

            // Move back up
            var upResult = await _appService.MoveUpAsync(created.Id);
            upResult.CurrentNodeId.ShouldBe(created.CurrentNodeId);
        });
    }
}
