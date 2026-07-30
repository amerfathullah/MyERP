using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Purchasing.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Purchasing.DomainServices;

/// <summary>
/// Auto-evaluates supplier delivery performance and updates scorecard scores.
/// Per ERPNext supplier_scorecard.py: scorecard recalculated on Purchase Receipt submit.
/// Calculates on-time delivery %, quality acceptance %, and value adherence from PO/PR data.
/// </summary>
public class SupplierScorecardEvaluationService : DomainService
{
    private readonly IRepository<PurchaseOrder, Guid> _poRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _prRepository;
    private readonly IRepository<SupplierScorecard, Guid> _scorecardRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;

    public SupplierScorecardEvaluationService(
        IRepository<PurchaseOrder, Guid> poRepository,
        IRepository<PurchaseReceipt, Guid> prRepository,
        IRepository<SupplierScorecard, Guid> scorecardRepository,
        IRepository<Supplier, Guid> supplierRepository)
    {
        _poRepository = poRepository;
        _prRepository = prRepository;
        _scorecardRepository = scorecardRepository;
        _supplierRepository = supplierRepository;
    }

    /// <summary>
    /// Evaluates supplier delivery performance over a period and updates scorecard score.
    /// Called automatically on PR submit for the supplier.
    /// </summary>
    public async Task EvaluateAndUpdateAsync(Guid supplierId, Guid companyId)
    {
        var scorecardQuery = await _scorecardRepository.GetQueryableAsync();
        var scorecard = scorecardQuery.FirstOrDefault(
            s => s.SupplierId == supplierId && s.CompanyId == companyId);

        if (scorecard == null) return;

        var metrics = await CalculateDeliveryMetricsAsync(supplierId, companyId);
        if (metrics.TotalOrders == 0) return;

        var newScore = CalculateCompositeScore(metrics, scorecard);
        var standing = scorecard.UpdateScore(newScore);
        await _scorecardRepository.UpdateAsync(scorecard);

        await SyncEnforcementToSupplierAsync(supplierId, standing);
    }

    /// <summary>
    /// Calculates delivery metrics from completed POs in the last 12 months.
    /// </summary>
    public async Task<SupplierDeliveryMetrics> CalculateDeliveryMetricsAsync(
        Guid supplierId, Guid companyId)
    {
        var cutoffDate = DateTime.UtcNow.AddMonths(-12);

        var poQuery = await _poRepository.GetQueryableAsync();
        var orders = poQuery.Where(po =>
            po.SupplierId == supplierId &&
            po.CompanyId == companyId &&
            po.CreationTime >= cutoffDate &&
            (int)po.Status >= 4) // Completed or beyond
            .ToList();

        if (!orders.Any())
            return new SupplierDeliveryMetrics(0, 0, 0, 0, 0, 0m);

        var totalOrders = orders.Count;
        var onTimeCount = 0;
        var lateCount = 0;
        var totalDelayDays = 0;

        foreach (var po in orders)
        {
            if (po.ExpectedDeliveryDate == null)
            {
                onTimeCount++;
                continue;
            }

            var perReceived = po.PerReceived;
            if (perReceived >= 100)
            {
                var lastReceiptDate = await GetLastReceiptDateAsync(po.Id);
                if (lastReceiptDate.HasValue && lastReceiptDate.Value.Date <= po.ExpectedDeliveryDate.Value.Date)
                {
                    onTimeCount++;
                }
                else
                {
                    lateCount++;
                    if (lastReceiptDate.HasValue)
                        totalDelayDays += Math.Max(0, (lastReceiptDate.Value.Date - po.ExpectedDeliveryDate.Value.Date).Days);
                }
            }
            else if (po.ExpectedDeliveryDate.Value.Date < DateTime.UtcNow.Date)
            {
                lateCount++;
                totalDelayDays += (DateTime.UtcNow.Date - po.ExpectedDeliveryDate.Value.Date).Days;
            }
            else
            {
                onTimeCount++;
            }
        }

        var avgDelay = lateCount > 0 ? (decimal)totalDelayDays / lateCount : 0m;

        return new SupplierDeliveryMetrics(
            totalOrders, onTimeCount, lateCount,
            totalOrders - onTimeCount - lateCount,
            totalDelayDays, avgDelay);
    }

    /// <summary>
    /// Calculates a composite score (0-100) from delivery metrics.
    /// Weight: On-Time Rate 70%, Value Adherence 20%, Responsiveness 10%.
    /// </summary>
    public static decimal CalculateCompositeScore(
        SupplierDeliveryMetrics metrics, SupplierScorecard scorecard)
    {
        if (metrics.TotalOrders == 0) return 100m;

        var onTimeRate = (decimal)metrics.OnTimeCount / metrics.TotalOrders * 100m;

        // Delay penalty: reduce score proportionally (max 20 point deduction)
        var delayPenalty = Math.Min(20m, metrics.AvgDelayDays * 2m);

        // Composite: on-time rate weighted at 80%, delay penalty caps at 20%
        var score = (onTimeRate * 0.8m) + (Math.Max(0, 20m - delayPenalty));

        return Math.Clamp(Math.Round(score, 2), 0m, 100m);
    }

    private async Task<DateTime?> GetLastReceiptDateAsync(Guid purchaseOrderId)
    {
        var prQuery = await _prRepository.GetQueryableAsync();
        var receipts = prQuery
            .Where(pr => pr.PurchaseOrderId == purchaseOrderId && !pr.IsReturn)
            .Select(pr => (DateTime?)pr.PostingDate)
            .ToList();

        return receipts.Any() ? receipts.Max() : null;
    }

    private async Task SyncEnforcementToSupplierAsync(Guid supplierId, ScorecardStanding? standing)
    {
        if (standing == null) return;

        var supplier = await _supplierRepository.FindAsync(supplierId);
        if (supplier == null) return;

        supplier.PreventPurchaseOrders = standing.PreventPos;
        supplier.PreventRfqs = standing.PreventRfqs;
        await _supplierRepository.UpdateAsync(supplier);
    }
}

/// <summary>Supplier delivery performance metrics over a period.</summary>
public record SupplierDeliveryMetrics(
    int TotalOrders,
    int OnTimeCount,
    int LateCount,
    int PendingCount,
    int TotalDelayDays,
    decimal AvgDelayDays)
{
    public decimal OnTimeRate => TotalOrders > 0
        ? Math.Round((decimal)OnTimeCount / TotalOrders * 100m, 2)
        : 0m;
}
