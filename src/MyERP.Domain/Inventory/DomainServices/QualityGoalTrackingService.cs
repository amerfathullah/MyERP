using System;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Domain service for evaluating Quality Reviews against Quality Goals.
/// </summary>
public class QualityGoalTrackingService : DomainService
{
    private readonly IRepository<QualityGoal, Guid> _goalRepository;
    private readonly IRepository<QualityReview, Guid> _reviewRepository;
    private readonly IGuidGenerator _guidGenerator;

    public QualityGoalTrackingService(
        IRepository<QualityGoal, Guid> goalRepository,
        IRepository<QualityReview, Guid> reviewRepository,
        IGuidGenerator guidGenerator)
    {
        _goalRepository = goalRepository;
        _reviewRepository = reviewRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Creates a Quality Review for a Goal and evaluates whether it met the target.
    /// Assuming higher is better by default unless goal name implies a defect rate.
    /// </summary>
    public async Task<QualityReview> EvaluateGoalAsync(Guid goalId, decimal actualValue, DateTime reviewDate, string? notes = null)
    {
        var goal = await _goalRepository.GetAsync(goalId);

        var review = new QualityReview(
            id: _guidGenerator.Create(),
            qualityGoalId: goalId,
            reviewDate: reviewDate,
            tenantId: goal.TenantId
        );

        // Simple heuristic: If goal name has "Defect" or "Reject", lower is better.
        bool isLowerBetter = goal.Name.Contains("Defect", StringComparison.OrdinalIgnoreCase) || 
                             goal.Name.Contains("Reject", StringComparison.OrdinalIgnoreCase) ||
                             (goal.Goal?.Contains("Defect", StringComparison.OrdinalIgnoreCase) == true);

        bool isMet = isLowerBetter ? actualValue <= goal.TargetValue : actualValue >= goal.TargetValue;

        if (isMet)
        {
            review.Pass(actualValue, notes);
        }
        else
        {
            review.Fail(actualValue, notes);
        }

        await _reviewRepository.InsertAsync(review);
        // Note: ERPNext doesn't change QualityGoal status, it just records the review.

        return review;
    }
}
