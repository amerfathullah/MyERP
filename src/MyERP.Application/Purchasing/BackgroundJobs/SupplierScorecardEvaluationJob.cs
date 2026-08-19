using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing.BackgroundJobs;

/// <summary>
/// Background job that periodically re-evaluates supplier delivery performance and recalculates supplier scorecard standings.
/// Per ERPNext: supplier_scorecard.generate_scorecards (daily/weekly scheduler).
/// </summary>
public class SupplierScorecardEvaluationJob : AsyncBackgroundJob<SupplierScorecardEvaluationJobArgs>, ITransientDependency
{
    private readonly IRepository<SupplierScorecard, Guid> _scorecardRepository;
    private readonly IRepository<PurchaseOrder, Guid> _poRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly ILogger<SupplierScorecardEvaluationJob> _logger;

    public SupplierScorecardEvaluationJob(
        IRepository<SupplierScorecard, Guid> scorecardRepository,
        IRepository<PurchaseOrder, Guid> poRepository,
        IRepository<Supplier, Guid> supplierRepository,
        ILogger<SupplierScorecardEvaluationJob> logger)
    {
        _scorecardRepository = scorecardRepository;
        _poRepository = poRepository;
        _supplierRepository = supplierRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(SupplierScorecardEvaluationJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var lookbackDate = asOfDate.AddMonths(-6);

        _logger.LogInformation("SupplierScorecardEvaluationJob: Evaluating supplier scorecards for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var scorecardsQuery = await _scorecardRepository.WithDetailsAsync(s => s.Standings, s => s.Criteria);
        var activeScorecards = scorecardsQuery
            .Where(s => s.CompanyId == args.CompanyId)
            .ToList();

        if (!activeScorecards.Any())
            return;

        var poQuery = await _poRepository.GetQueryableAsync();
        var recentOrders = poQuery
            .Where(po => po.CompanyId == args.CompanyId &&
                         po.OrderDate >= lookbackDate &&
                         po.OrderDate <= asOfDate &&
                         po.Status != DocumentStatus.Draft &&
                         po.Status != DocumentStatus.Cancelled)
            .ToList();

        var evaluatedCount = 0;
        foreach (var scorecard in activeScorecards)
        {
            var supplierOrders = recentOrders.Where(po => po.SupplierId == scorecard.SupplierId).ToList();
            if (!supplierOrders.Any())
                continue;

            var withExpectedDate = supplierOrders.Where(po => po.ExpectedDeliveryDate.HasValue).ToList();
            var onTimeCount = withExpectedDate.Count(po =>
                po.PerReceived >= 100 &&
                po.OrderDate <= po.ExpectedDeliveryDate!.Value);

            // Base score calculated as on-time delivery percentage (or 100 if no historical data)
            var newScore = withExpectedDate.Any()
                ? Math.Round((decimal)onTimeCount / withExpectedDate.Count * 100m, 2)
                : 100m;

            var prevScore = scorecard.Score;
            scorecard.UpdateScore(newScore);
            await _scorecardRepository.UpdateAsync(scorecard);

            // Sync supplier enforcement flags
            var (preventPos, preventRfqs, _, _) = scorecard.GetEnforcementFlags();
            var supplier = await _supplierRepository.FindAsync(scorecard.SupplierId);
            if (supplier != null)
            {
                supplier.PreventPurchaseOrders = preventPos;
                supplier.PreventRfqs = preventRfqs;
                await _supplierRepository.UpdateAsync(supplier);
            }

            if (scorecard.Score != prevScore)
            {
                evaluatedCount++;
            }
        }

        _logger.LogInformation("SupplierScorecardEvaluationJob: Evaluated {Count} supplier scorecards for company {CompanyId}",
            evaluatedCount, args.CompanyId);
    }
}

public class SupplierScorecardEvaluationJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
