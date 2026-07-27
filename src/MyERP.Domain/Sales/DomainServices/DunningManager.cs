using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Dunning Manager — enforces level sequencing, interest calculation, and auto-resolution.
/// 
/// Per ERPNext dunning.py:
/// - Level sequencing: must have Level N before Level N+1 (determined by counting existing submitted dunnings)
/// - Interest formula: rate / 100 / 365 × overdue_days × outstanding (per invoice)
/// - Fee: flat amount per dunning level (from Dunning Type configuration)
/// - Auto-resolve: when ALL linked invoices are fully paid, dunning auto-resolves
/// 
/// Per DO-NOT: "Skip dunning level sequencing (must have Level N before Level N+1)"
/// </summary>
public class DunningManager : DomainService
{
    private readonly IRepository<Dunning, Guid> _dunningRepository;

    public DunningManager(IRepository<Dunning, Guid> dunningRepository)
    {
        _dunningRepository = dunningRepository;
    }

    /// <summary>
    /// Determines the correct dunning level for a new dunning based on existing submitted dunnings for the customer.
    /// Per ERPNext set_dunning_level: count of existing submitted dunnings for the customer + 1.
    /// </summary>
    public async Task<int> DetermineDunningLevelAsync(Guid customerId, Guid companyId, Guid? tenantId)
    {
        var query = await _dunningRepository.GetQueryableAsync();
        var existingCount = query
            .Count(d => d.CustomerId == customerId
                     && d.CompanyId == companyId
                     && d.Status == DocumentStatus.Submitted);

        return existingCount + 1;
    }

    /// <summary>
    /// Validates that level sequencing is respected.
    /// Level N requires at least (N-1) submitted dunnings for the customer to exist.
    /// Level 1 is always allowed.
    /// </summary>
    public async Task ValidateLevelSequencingAsync(Guid customerId, Guid companyId, int proposedLevel, Guid? tenantId)
    {
        if (proposedLevel <= 1) return;

        var query = await _dunningRepository.GetQueryableAsync();
        var existingSubmittedCount = query
            .Count(d => d.CustomerId == customerId
                     && d.CompanyId == companyId
                     && d.Status == DocumentStatus.Submitted);

        if (existingSubmittedCount < proposedLevel - 1)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("reason", $"Dunning Level {proposedLevel} requires Level {proposedLevel - 1} to exist first")
                .WithData("currentMaxLevel", existingSubmittedCount)
                .WithData("proposedLevel", proposedLevel);
        }
    }

    /// <summary>
    /// Calculates interest amount for a set of overdue invoices.
    /// Per ERPNext: interest = rate / 100 / 365 × overdue_days × outstanding_amount (per invoice).
    /// </summary>
    public static decimal CalculateInterest(
        decimal interestRatePerAnnum,
        IReadOnlyList<(decimal outstanding, int overdueDays)> overdueInvoices)
    {
        if (interestRatePerAnnum <= 0 || !overdueInvoices.Any())
            return 0;

        var dailyRate = interestRatePerAnnum / 100m / 365m;
        return overdueInvoices.Sum(inv => dailyRate * inv.overdueDays * inv.outstanding);
    }

    /// <summary>
    /// Checks if all linked invoices are fully paid (outstanding = 0).
    /// If so, the dunning should be auto-resolved.
    /// Per ERPNext update_linked_dunnings: multi-invoice resolution check.
    /// </summary>
    public static bool ShouldAutoResolve(Dunning dunning)
    {
        if (dunning.Status != DocumentStatus.Submitted) return false;
        return dunning.OverduePayments.All(p => p.OutstandingAmount <= 0);
    }
}
