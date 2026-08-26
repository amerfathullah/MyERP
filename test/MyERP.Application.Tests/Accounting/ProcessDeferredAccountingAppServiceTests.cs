using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class ProcessDeferredAccountingAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IProcessDeferredAccountingAppService _appService;
    private readonly IRepository<Company, Guid> _companyRepository;

    protected ProcessDeferredAccountingAppServiceTests()
    {
        _appService = GetRequiredService<IProcessDeferredAccountingAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
    }

    [Fact]
    public async Task ProcessDeferredAccounting_Should_Create_Update_Submit_And_Cancel()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = new Company(Guid.NewGuid(), "Test PDA Co");
            await _companyRepository.InsertAsync(company);

            var created = await _appService.CreateAsync(new CreateProcessDeferredAccountingDto
            {
                CompanyId = company.Id,
                Type = DeferredAccountingType.Income,
                PostingDate = DateTime.UtcNow.Date,
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 1, 31),
            });

            created.ShouldNotBeNull();
            created.ProcessNumber.ShouldStartWith("ACC-PDA-");
            created.CompanyName.ShouldBe("Test PDA Co");
            created.IsSubmitted.ShouldBeFalse();

            var updated = await _appService.UpdateAsync(created.Id, new UpdateProcessDeferredAccountingDto
            {
                CompanyId = company.Id,
                Type = DeferredAccountingType.Expense,
                PostingDate = DateTime.UtcNow.Date,
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 1, 31),
            });

            updated.Type.ShouldBe(DeferredAccountingType.Expense);

            var submitted = await _appService.SubmitAsync(created.Id);
            submitted.IsSubmitted.ShouldBeTrue();

            var cancelled = await _appService.CancelAsync(created.Id);
            cancelled.IsCancelled.ShouldBeTrue();
        });
    }
}
