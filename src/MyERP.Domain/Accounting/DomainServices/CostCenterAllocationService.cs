using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Manages Cost Center Allocations with DAG validation, valid_from enforcement,
/// and GL distribution logic.
/// 
/// Per ERPNext (gotchas #418, DO-NOT rules):
/// - Main CC cannot be a child in any other allocation
/// - Child CC cannot be a main in any other allocation
/// - valid_from cannot be before the last GL Entry posting_date for that cost center
/// - Only distributes: debit, credit, debit_in_account_currency, credit_in_account_currency
/// - Round-off remainder goes to FIRST sub-cost-center
/// </summary>
public class CostCenterAllocationService : DomainService
{
    private readonly IRepository<CostCenterAllocation, Guid> _allocationRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;

    public CostCenterAllocationService(
        IRepository<CostCenterAllocation, Guid> allocationRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository)
    {
        _allocationRepository = allocationRepository;
        _journalEntryRepository = journalEntryRepository;
    }

    /// <summary>
    /// Validates no DAG cycles exist when creating/updating an allocation.
    /// Per DO-NOT: "Allow Cost Center Allocation cycles (A→B and B→A)"
    /// 
    /// Rules:
    /// 1. Main CC cannot appear as a child in ANY other allocation
    /// 2. Child CCs cannot appear as main in ANY other allocation
    /// </summary>
    public async Task ValidateNoCycleAsync(
        Guid mainCostCenterId,
        IEnumerable<Guid> childCostCenterIds,
        Guid? excludeAllocationId = null)
    {
        var queryable = await _allocationRepository.GetQueryableAsync();

        // Exclude current allocation from checks (for updates)
        if (excludeAllocationId.HasValue)
            queryable = queryable.Where(a => a.Id != excludeAllocationId.Value);

        var activeAllocations = queryable.Where(a => a.IsActive).ToList();

        // Rule 1: Main CC cannot be a child in any other allocation
        foreach (var alloc in activeAllocations)
        {
            if (alloc.Entries.Any(e => e.ChildCostCenterId == mainCostCenterId))
            {
                throw new BusinessException("MyERP:02043")
                    .WithData("costCenter", mainCostCenterId.ToString())
                    .WithData("reason", "Main cost center is already used as a child in another allocation");
            }
        }

        // Rule 2: Child CCs cannot be main in any other allocation
        var childIds = childCostCenterIds.ToHashSet();
        foreach (var alloc in activeAllocations)
        {
            if (childIds.Contains(alloc.MainCostCenterId))
            {
                throw new BusinessException("MyERP:02043")
                    .WithData("costCenter", alloc.MainCostCenterId.ToString())
                    .WithData("reason", "Child cost center is already used as a main in another allocation");
            }
        }
    }

    /// <summary>
    /// Validates that valid_from is not before the last GL posting date for the main cost center.
    /// Per DO-NOT: "Set CCA valid_from date before the last GL Entry posting_date"
    /// </summary>
    public async Task ValidateValidFromAsync(Guid mainCostCenterId, DateTime validFrom, Guid companyId)
    {
        var jeQueryable = await _journalEntryRepository.GetQueryableAsync();

        var lastPostingDate = jeQueryable
            .Where(je => je.CompanyId == companyId && je.Status == DocumentStatus.Posted)
            .SelectMany(je => je.Lines)
            .Where(line => line.CostCenterId == mainCostCenterId)
            .OrderByDescending(line => line.CreationTime)
            .Select(line => line.CreationTime)
            .FirstOrDefault();

        if (lastPostingDate != default && validFrom < lastPostingDate.Date)
        {
            throw new BusinessException("MyERP:02044")
                .WithData("validFrom", validFrom.ToString("yyyy-MM-dd"))
                .WithData("lastGlDate", lastPostingDate.ToString("yyyy-MM-dd"));
        }
    }

    /// <summary>
    /// Gets the active allocation for a cost center as of a given date.
    /// Returns null if no allocation exists (GL posts directly to the cost center).
    /// </summary>
    public async Task<CostCenterAllocation?> GetActiveAllocationAsync(Guid costCenterId, DateTime asOfDate)
    {
        var queryable = await _allocationRepository.GetQueryableAsync();

        return queryable
            .Where(a => a.MainCostCenterId == costCenterId
                     && a.IsActive
                     && a.ValidFrom <= asOfDate)
            .OrderByDescending(a => a.ValidFrom)
            .FirstOrDefault();
    }

    /// <summary>
    /// Distributes GL amounts for a cost center based on active allocation.
    /// Returns null if no allocation exists (post directly to original CC).
    /// Per gotcha #418: only distributes debit, credit, debit_in_account_currency, credit_in_account_currency.
    /// </summary>
    public async Task<List<(Guid CostCenterId, decimal Debit, decimal Credit)>?> DistributeGlAmountsAsync(
        Guid costCenterId,
        decimal debit,
        decimal credit,
        DateTime postingDate)
    {
        var allocation = await GetActiveAllocationAsync(costCenterId, postingDate);
        if (allocation == null) return null; // No distribution needed

        var result = new List<(Guid CostCenterId, decimal Debit, decimal Credit)>();
        var entries = allocation.Entries.OrderBy(e => e.ChildCostCenterId).ToList();

        decimal totalDebitDistributed = 0, totalCreditDistributed = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            var pct = entries[i].Percentage / 100m;
            var entryDebit = Math.Round(debit * pct, 4);
            var entryCredit = Math.Round(credit * pct, 4);

            totalDebitDistributed += entryDebit;
            totalCreditDistributed += entryCredit;

            result.Add((entries[i].ChildCostCenterId, entryDebit, entryCredit));
        }

        // Round-off remainder to FIRST entry
        if (result.Count > 0)
        {
            var debitRemainder = debit - totalDebitDistributed;
            var creditRemainder = credit - totalCreditDistributed;
            if (debitRemainder != 0 || creditRemainder != 0)
            {
                result[0] = (result[0].CostCenterId, result[0].Debit + debitRemainder, result[0].Credit + creditRemainder);
            }
        }

        return result;
    }
}
