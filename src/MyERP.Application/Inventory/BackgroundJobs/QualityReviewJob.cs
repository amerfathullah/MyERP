using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Inventory.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory.BackgroundJobs;

/// <summary>
/// Background job that re-evaluates and rolls up status for active Quality Reviews.
/// Per ERPNext: quality_review.review (daily scheduler).
/// </summary>
public class QualityReviewJob : AsyncBackgroundJob<QualityReviewJobArgs>, ITransientDependency
{
    private readonly IRepository<QualityReview, Guid> _repository;
    private readonly ILogger<QualityReviewJob> _logger;

    public QualityReviewJob(
        IRepository<QualityReview, Guid> repository,
        ILogger<QualityReviewJob> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(QualityReviewJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("QualityReviewJob: Evaluating quality reviews as of {Date}", asOfDate);

        var query = await _repository.GetQueryableAsync();
        var openReviews = query
            .Where(r => r.Status == QualityReviewStatus.Open)
            .ToList();

        var updatedCount = 0;
        foreach (var review in openReviews)
        {
            var prevStatus = review.Status;
            review.EvaluateStatus();
            if (review.Status != prevStatus)
            {
                await _repository.UpdateAsync(review);
                updatedCount++;
            }
        }

        _logger.LogInformation("QualityReviewJob: Re-evaluated {Count} of {Total} open quality reviews",
            updatedCount, openReviews.Count);
    }
}

public class QualityReviewJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
