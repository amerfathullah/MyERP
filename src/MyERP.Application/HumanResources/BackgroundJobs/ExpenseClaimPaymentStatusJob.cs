using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.HumanResources.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources.BackgroundJobs;

/// <summary>
/// Background job that syncs reimbursed amounts from Payment Entries to approved Expense Claims.
/// Per ERPNext: expense_claim.update_status_for_expense_claim (daily scheduler).
/// </summary>
public class ExpenseClaimPaymentStatusJob : AsyncBackgroundJob<ExpenseClaimPaymentStatusJobArgs>, ITransientDependency
{
    private readonly IRepository<ExpenseClaim, Guid> _expenseClaimRepository;
    private readonly IRepository<PaymentEntryReference, Guid> _referenceRepository;
    private readonly ILogger<ExpenseClaimPaymentStatusJob> _logger;

    public ExpenseClaimPaymentStatusJob(
        IRepository<ExpenseClaim, Guid> expenseClaimRepository,
        IRepository<PaymentEntryReference, Guid> referenceRepository,
        ILogger<ExpenseClaimPaymentStatusJob> logger)
    {
        _expenseClaimRepository = expenseClaimRepository;
        _referenceRepository = referenceRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(ExpenseClaimPaymentStatusJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("ExpenseClaimPaymentStatusJob: Syncing reimbursed amounts for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var query = await _expenseClaimRepository.GetQueryableAsync();
        var submittedClaims = query
            .Where(c => c.CompanyId == args.CompanyId &&
                        (c.Status == DocumentStatus.Submitted || c.Status == DocumentStatus.Approved))
            .ToList();

        if (!submittedClaims.Any())
            return;

        var claimIds = submittedClaims.Select(c => c.Id).ToList();
        var refQuery = await _referenceRepository.GetQueryableAsync();
        var payments = refQuery
            .Where(r => r.ReferenceType == "ExpenseClaim" && claimIds.Contains(r.ReferenceId))
            .ToList();

        var updatedCount = 0;
        foreach (var claim in submittedClaims)
        {
            var reimbursed = payments
                .Where(r => r.ReferenceId == claim.Id)
                .Sum(r => r.AllocatedAmount);

            if (claim.TotalAmountReimbursed != reimbursed)
            {
                claim.TotalAmountReimbursed = reimbursed;
                await _expenseClaimRepository.UpdateAsync(claim);
                updatedCount++;
            }
        }

        _logger.LogInformation("ExpenseClaimPaymentStatusJob: Updated payment status on {Count} of {Total} expense claims for company {CompanyId}",
            updatedCount, submittedClaims.Count, args.CompanyId);
    }
}

public class ExpenseClaimPaymentStatusJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
