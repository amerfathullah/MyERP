using System;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for QualityManagementAppService.EvaluateGoalAsync — wires up
/// QualityGoalTrackingService.EvaluateGoalAsync (zero callers anywhere before this session), which
/// creates a Quality Review AND auto-determines Pass/Fail against the Goal's TargetValue in one call,
/// instead of the manual CreateReviewAsync + user-judged EvaluateReviewAsync two-step flow the
/// quality-review-form UI otherwise required for every single review.
/// </summary>
public abstract class QualityGoalEvaluationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task EvaluateGoalAsync_HigherIsBetterGoal_ActualAboveTarget_Passes()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var appService = GetRequiredService<IQualityManagementAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Quality Goal Eval Test Co 1"), autoSave: true);

            var goal = await appService.CreateGoalAsync(new CreateUpdateQualityGoalDto
            {
                Name = "First Pass Yield",
                Frequency = "Monthly",
                TargetValue = 95m,
                Uom = "%",
            });

            var review = await appService.EvaluateGoalAsync(goal.Id, new EvaluateGoalDto
            {
                ActualValue = 97.5m,
                ReviewDate = DateTime.UtcNow.Date,
                Notes = "March batch",
            });

            review.QualityGoalId.ShouldBe(goal.Id);
            review.ActualValue.ShouldBe(97.5m);
            review.Status.ShouldBe(QualityReviewStatus.Passed);
        });
    }

    [Fact]
    public async Task EvaluateGoalAsync_HigherIsBetterGoal_ActualBelowTarget_Fails()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var appService = GetRequiredService<IQualityManagementAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Quality Goal Eval Test Co 2"), autoSave: true);

            var goal = await appService.CreateGoalAsync(new CreateUpdateQualityGoalDto
            {
                Name = "First Pass Yield",
                Frequency = "Monthly",
                TargetValue = 95m,
                Uom = "%",
            });

            var review = await appService.EvaluateGoalAsync(goal.Id, new EvaluateGoalDto
            {
                ActualValue = 90m,
                ReviewDate = DateTime.UtcNow.Date,
            });

            review.Status.ShouldBe(QualityReviewStatus.Failed);
        });
    }

    [Fact]
    public async Task EvaluateGoalAsync_DefectRateGoal_LowerIsBetter_ActualBelowTarget_Passes()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var appService = GetRequiredService<IQualityManagementAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Quality Goal Eval Test Co 3"), autoSave: true);

            // Name contains "Defect" — QualityGoalTrackingService's heuristic flips to lower-is-better.
            var goal = await appService.CreateGoalAsync(new CreateUpdateQualityGoalDto
            {
                Name = "Defect Rate",
                Frequency = "Weekly",
                TargetValue = 2m,
                Uom = "%",
            });

            var review = await appService.EvaluateGoalAsync(goal.Id, new EvaluateGoalDto
            {
                ActualValue = 1.5m,
                ReviewDate = DateTime.UtcNow.Date,
            });

            review.Status.ShouldBe(QualityReviewStatus.Passed);
        });
    }
}
