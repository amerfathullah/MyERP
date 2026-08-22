using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Regression coverage for SupplierScorecardAppService.SubmitPeriodAsync: had zero callers anywhere
/// in Angular (the scorecard detail page only ever displayed the score, no way to submit a new period
/// evaluation) and zero test coverage, despite driving a real side effect — syncing
/// Supplier.PreventPurchaseOrders/PreventRfqs to the score's enforcement band. Added a "Submit Period
/// Evaluation" panel to the detail page; this test covers the backend it now actually reaches,
/// including the enforcement-flag sync across a standing-band crossing.
/// </summary>
public abstract class SupplierScorecardSubmitPeriodTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubmitPeriodAsync_LowScore_PersistsPeriod_AndSyncsSupplierEnforcementFlags()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var scorecardRepository = GetRequiredService<IRepository<SupplierScorecard, Guid>>();
            var periodRepository = GetRequiredService<IRepository<ScorecardPeriod, Guid>>();
            var scorecardAppService = GetRequiredService<ISupplierScorecardAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Scorecard Period Test Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Scorecard Period Test Supplier"), autoSave: true);

            var scorecard = new SupplierScorecard(Guid.NewGuid(), supplier.Id, company.Id);
            scorecard.AddStanding("Poor", 0, 30, preventPos: true, preventRfqs: true);
            scorecard.AddStanding("Good", 30, 100, preventPos: false, preventRfqs: false);
            await scorecardRepository.InsertAsync(scorecard, autoSave: true);

            await scorecardAppService.SubmitPeriodAsync(scorecard.Id, new CreateScorecardPeriodDto
            {
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 1, 31),
                Score = 20m,
            });

            var reloadedScorecard = await scorecardRepository.GetAsync(scorecard.Id);
            reloadedScorecard.Score.ShouldBe(20m);
            reloadedScorecard.CurrentStanding.ShouldBe("Poor");

            var periods = (await periodRepository.GetQueryableAsync()).Where(p => p.SupplierScorecardId == scorecard.Id).ToList();
            periods.Count.ShouldBe(1);
            periods.Single().TotalScore.ShouldBe(20m);

            var reloadedSupplier = await supplierRepository.GetAsync(supplier.Id);
            reloadedSupplier.PreventPurchaseOrders.ShouldBeTrue();
            reloadedSupplier.PreventRfqs.ShouldBeTrue();
        });
    }

    [Fact]
    public async Task SubmitPeriodAsync_ScoreCrossingIntoGoodBand_ClearsSupplierEnforcementFlags()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var scorecardRepository = GetRequiredService<IRepository<SupplierScorecard, Guid>>();
            var scorecardAppService = GetRequiredService<ISupplierScorecardAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Scorecard Period Test Co 2"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Scorecard Period Test Supplier 2")
            {
                PreventPurchaseOrders = true,
                PreventRfqs = true,
            }, autoSave: true);

            var scorecard = new SupplierScorecard(Guid.NewGuid(), supplier.Id, company.Id);
            scorecard.AddStanding("Poor", 0, 30, preventPos: true, preventRfqs: true);
            scorecard.AddStanding("Good", 30, 100, preventPos: false, preventRfqs: false);
            await scorecardRepository.InsertAsync(scorecard, autoSave: true);

            await scorecardAppService.SubmitPeriodAsync(scorecard.Id, new CreateScorecardPeriodDto
            {
                StartDate = new DateTime(2026, 2, 1),
                EndDate = new DateTime(2026, 2, 28),
                Score = 80m,
            });

            var reloadedSupplier = await supplierRepository.GetAsync(supplier.Id);
            reloadedSupplier.PreventPurchaseOrders.ShouldBeFalse();
            reloadedSupplier.PreventRfqs.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task SubmitPeriodAsync_EndDateBeforeStartDate_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var scorecardRepository = GetRequiredService<IRepository<SupplierScorecard, Guid>>();
            var scorecardAppService = GetRequiredService<ISupplierScorecardAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Scorecard Period Test Co 3"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Scorecard Period Test Supplier 3"), autoSave: true);

            var scorecard = new SupplierScorecard(Guid.NewGuid(), supplier.Id, company.Id);
            scorecard.AddStanding("Good", 0, 100);
            await scorecardRepository.InsertAsync(scorecard, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() => scorecardAppService.SubmitPeriodAsync(scorecard.Id, new CreateScorecardPeriodDto
            {
                StartDate = new DateTime(2026, 2, 28),
                EndDate = new DateTime(2026, 2, 1),
                Score = 50m,
            }));
        });
    }
}
