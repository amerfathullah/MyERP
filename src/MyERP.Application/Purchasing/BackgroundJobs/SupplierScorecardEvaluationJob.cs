using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace MyERP.Purchasing.BackgroundJobs;

/// <summary>
/// Background job that recalculates periodic Supplier Scorecards based on delivery and quality metrics
/// and updates standing enforcement rules.
/// Per ERPNext: supplier_scorecard.evaluate_scorecards (daily/monthly scheduler).
/// </summary>
public class SupplierScorecardEvaluationJob : AsyncBackgroundJob<SupplierScorecardEvaluationJobArgs>, ITransientDependency
{
    private readonly IRepository<SupplierScorecard, Guid> _scorecardRepository;
    private readonly IRepository<PurchaseOrder, Guid> _poRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _receiptRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SupplierScorecardEvaluationJob> _logger;

    public SupplierScorecardEvaluationJob(
        IRepository<SupplierScorecard, Guid> scorecardRepository,
        IRepository<PurchaseOrder, Guid> poRepository,
        IRepository<PurchaseReceipt, Guid> receiptRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IEmailSender emailSender,
        ILogger<SupplierScorecardEvaluationJob> logger)
    {
        _scorecardRepository = scorecardRepository;
        _poRepository = poRepository;
        _receiptRepository = receiptRepository;
        _supplierRepository = supplierRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(SupplierScorecardEvaluationJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var lookbackDate = asOfDate.AddDays(-90);

        _logger.LogInformation("SupplierScorecardEvaluationJob: Evaluating scorecards for company {CompanyId} lookback from {Date}",
            args.CompanyId, lookbackDate.ToString("yyyy-MM-dd"));

        var scorecardQuery = await _scorecardRepository.WithDetailsAsync(s => s.Standings, s => s.Criteria);
        var scorecards = scorecardQuery
            .Where(s => s.CompanyId == args.CompanyId)
            .ToList();

        if (!scorecards.Any())
            return;

        var poQuery = await _poRepository.GetQueryableAsync();
        var pos = poQuery
            .Where(p => p.CompanyId == args.CompanyId &&
                        p.Status == DocumentStatus.Submitted &&
                        p.OrderDate >= lookbackDate)
            .ToList();

        var receiptQuery = await _receiptRepository.GetQueryableAsync();
        var receipts = receiptQuery
            .Where(r => r.CompanyId == args.CompanyId &&
                        r.Status == DocumentStatus.Submitted &&
                        r.PostingDate >= lookbackDate)
            .ToList();

        var suppliersQuery = await _supplierRepository.GetQueryableAsync();
        var suppliers = suppliersQuery
            .Where(s => s.CompanyId == args.CompanyId)
            .ToList();

        var degradedScorecards = 0;
        var alertItems = "";

        foreach (var scorecard in scorecards)
        {
            var supplierPos = pos.Where(p => p.SupplierId == scorecard.SupplierId).ToList();
            var supplierReceipts = receipts.Where(r => r.SupplierId == scorecard.SupplierId).ToList();

            if (!supplierPos.Any() && !supplierReceipts.Any())
            {
                scorecard.UpdateScore(100m); // Benefit of doubt
                await _scorecardRepository.UpdateAsync(scorecard);
                continue;
            }

            // Metric 1: On-time delivery rate (0-100)
            decimal deliveryScore = 100m;
            if (supplierReceipts.Any())
            {
                var onTimeReceipts = supplierReceipts.Count(r => r.PostingDate <= r.PostingDate.AddDays(2));
                deliveryScore = Math.Round((decimal)onTimeReceipts / supplierReceipts.Count * 100m, 1);
            }

            // Metric 2: Order fulfillment rate (0-100)
            decimal fulfillmentScore = 100m;
            if (supplierPos.Any())
            {
                var completedPos = supplierPos.Count(p => p.Status == DocumentStatus.Submitted);
                fulfillmentScore = Math.Round((decimal)completedPos / supplierPos.Count * 100m, 1);
            }

            // Combined overall score (weighted 50% delivery, 50% fulfillment)
            var finalScore = Math.Round((deliveryScore * 0.5m) + (fulfillmentScore * 0.5m), 1);
            var standing = scorecard.UpdateScore(finalScore);

            if (standing != null && (standing.PreventPos || standing.WarnPos))
            {
                degradedScorecards++;
                var supplierName = suppliers.FirstOrDefault(s => s.Id == scorecard.SupplierId)?.Name ?? scorecard.SupplierId.ToString();
                alertItems += $"<li><strong>{supplierName}</strong> - Score: {finalScore}/100 ({standing.Name}) | Delivery: {deliveryScore}% | Action: {(standing.PreventPos ? "PO Blocked" : "PO Warning")}</li>";
            }

            await _scorecardRepository.UpdateAsync(scorecard);
        }

        if (degradedScorecards > 0)
        {
            var usersQuery = await _userRepository.GetQueryableAsync();
            var procurementOfficers = usersQuery
                .Where(u => u.Email != null && u.Email.Length > 0 && u.IsActive)
                .Take(5)
                .ToList();

            var subject = $"[SUPPLIER ALERT] {degradedScorecards} Supplier Scorecards in Warning/Blocked Standing";
            var body = $@"<h3>Supplier Performance Scorecard Degradation Alert</h3>
<p>The following supplier(s) have received low performance scores during nightly evaluation:</p>
<ul>
{alertItems}
</ul>
<p><em>Review vendor performance records or update scorecard rules in MyERP.</em></p>";

            foreach (var user in procurementOfficers)
            {
                try
                {
                    await _emailSender.SendAsync(user.Email, subject, body, isBodyHtml: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SupplierScorecardEvaluationJob: Failed to send scorecard alert to {Email}", user.Email);
                }
            }
        }

        _logger.LogInformation("SupplierScorecardEvaluationJob: Evaluated {Total} supplier scorecards ({Degraded} warning/blocked) for company {CompanyId}",
            scorecards.Count, degradedScorecards, args.CompanyId);
    }
}

public class SupplierScorecardEvaluationJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
