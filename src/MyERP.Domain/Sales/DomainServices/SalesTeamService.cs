using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Domain service managing Sales Team allocations and commission calculations.
/// Enforces upstream ERPNext PR #61835 (commits 6869de7511, 53f265e9c5, 1fe56c013a, 32569b28fb):
/// 1. Reject invalid sales team members and splits:
///    - Cannot assign Group Sales Person (is_group == true).
///    - Cannot assign Disabled Sales Person (enabled == false).
///    - Members must be unique (no repeated sales persons).
///    - Allocated percentage per row must be between 0 and 100%.
///    - Total allocated percentage must sum to exactly 100%.
/// 2. Authoritative commission rates:
///    - Explicit rate if specified (0..100%).
///    - Preserve saved assignment commission rate when updating existing member (historical snapshot).
///    - Inherit original transaction commission rate for returns (is_return == true, return_against != null).
///    - Default to active SalesPerson.CommissionRate master rate.
/// 3. Recalculate allocations and incentives (allocated_amount * commission_rate / 100) before saving.
/// 4. Shared validation and post-submit updates across Sales Order, Delivery Note, Sales Invoice, POS Invoice.
/// </summary>
public class SalesTeamService : DomainService
{
    private readonly IRepository<SalesPerson, Guid> _salesPersonRepository;
    private readonly IRepository<SalesTeamEntry, Guid> _salesTeamRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public SalesTeamService(
        IRepository<SalesPerson, Guid> salesPersonRepository,
        IRepository<SalesTeamEntry, Guid> salesTeamRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _salesPersonRepository = salesPersonRepository;
        _salesTeamRepository = salesTeamRepository;
        _itemRepository = itemRepository;
    }

    /// <summary>
    /// Calculates total eligible amount for commission from document items.
    /// Filters out items where GrantCommission == false (per ERPNext grant_commission flag).
    /// Converts to company currency using the document exchange rate.
    /// </summary>
    public async Task<decimal> CalculateEligibleAmountAsync(
        IEnumerable<(Guid ItemId, decimal LineTotal)> items,
        decimal exchangeRate = 1m)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0) return 0m;

        var itemIds = itemList.Select(i => i.ItemId).Distinct().ToList();
        var itemEntities = await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id));
        var itemGrantMap = itemEntities.ToDictionary(i => i.Id, i => i.GrantCommission);

        var rate = exchangeRate > 0 ? exchangeRate : 1m;
        var eligible = itemList
            .Where(i => !itemGrantMap.TryGetValue(i.ItemId, out var grant) || grant)
            .Sum(i => i.LineTotal) * rate;

        return Math.Round(eligible, 2);
    }

    /// <summary>
    /// Validates, recalculates, and persists sales team entries for a transaction.
    /// </summary>
    public async Task<List<SalesTeamEntry>> SetSalesTeamAsync(
        string parentType,
        Guid parentId,
        decimal eligibleAmount,
        IEnumerable<SalesTeamAllocationInput>? inputRows,
        bool isReturn = false,
        Guid? returnAgainstId = null)
    {
        Check.NotNullOrWhiteSpace(parentType, nameof(parentType));
        Check.NotDefaultOrNull<Guid>(parentId, nameof(parentId));

        var rows = inputRows?.ToList() ?? new List<SalesTeamAllocationInput>();

        // If team is empty, remove existing entries and return
        if (rows.Count == 0)
        {
            await _salesTeamRepository.DeleteAsync(e => e.ParentType == parentType && e.ParentId == parentId, autoSave: true);
            return new List<SalesTeamEntry>();
        }

        // 1. Validate duplicates and individual percentage bounds (PR #61835 bug 4)
        var seen = new HashSet<Guid>();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (!seen.Add(row.SalesPersonId))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Sales Person is repeated in the sales team.");
            }
            if (row.AllocatedPercentage < 0m || row.AllocatedPercentage > 100m)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{i + 1}: Contribution (%) must be between 0 and 100.");
            }
        }

        // 2. Total allocation percentage must sum to 100%
        var totalPercentage = rows.Sum(r => r.AllocatedPercentage);
        if (Math.Round(totalPercentage, 2) != 100m)
        {
            throw new BusinessException(MyERPDomainErrorCodes.SalesTeamPercentageMustTotal100)
                .WithData("total", Math.Round(totalPercentage, 2))
                .WithData("detail", $"Sales team allocated percentage must sum to exactly 100%. Current sum: {Math.Round(totalPercentage, 2)}%.");
        }

        // 3. Validate sales persons exist, are not groups, and are enabled (PR #61835 bug 4)
        var spIds = rows.Select(r => r.SalesPersonId).Distinct().ToList();
        var spEntities = await _salesPersonRepository.GetListAsync(sp => spIds.Contains(sp.Id));
        var spDict = spEntities.ToDictionary(sp => sp.Id);

        foreach (var row in rows)
        {
            if (!spDict.TryGetValue(row.SalesPersonId, out var sp))
            {
                throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound)
                    .WithData("entity", "SalesPerson")
                    .WithData("id", row.SalesPersonId);
            }
            if (sp.IsGroup)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Cannot assign Group Sales Person '{sp.Name}' to Sales Team.");
            }
            if (!sp.IsEnabled)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Cannot assign Disabled Sales Person '{sp.Name}' to Sales Team.");
            }
        }

        // 4. Resolve authoritative commission rates (PR #61835 bug 2):
        // - Existing entries for this document: preserve previous commission rate if not explicitly specified.
        var existingEntries = await _salesTeamRepository.GetListAsync(e => e.ParentType == parentType && e.ParentId == parentId);
        var existingRates = existingEntries.ToDictionary(e => e.SalesPersonId, e => e.CommissionRate);

        // - Return documents: look up source transaction's sales team rates
        var returnRates = new Dictionary<Guid, decimal>();
        if (isReturn && returnAgainstId.HasValue)
        {
            var returnEntries = await _salesTeamRepository.GetListAsync(e => e.ParentType == parentType && e.ParentId == returnAgainstId.Value);
            returnRates = returnEntries.ToDictionary(e => e.SalesPersonId, e => e.CommissionRate);
        }

        var newEntries = new List<SalesTeamEntry>();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            decimal commissionRate;

            if (row.CommissionRate.HasValue)
            {
                commissionRate = row.CommissionRate.Value;
            }
            else if (existingRates.TryGetValue(row.SalesPersonId, out var savedRate))
            {
                commissionRate = savedRate;
            }
            else if (returnRates.TryGetValue(row.SalesPersonId, out var returnRate))
            {
                commissionRate = returnRate;
            }
            else
            {
                commissionRate = spDict[row.SalesPersonId].CommissionRate;
            }

            if (commissionRate < 0m || commissionRate > 100m)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{i + 1}: Commission Rate must be between 0 and 100.");
            }

            var entry = new SalesTeamEntry(
                Guid.NewGuid(),
                row.SalesPersonId,
                parentType,
                parentId,
                row.AllocatedPercentage,
                eligibleAmount,
                commissionRate);

            newEntries.Add(entry);
        }

        // Delete existing and save new rows
        await _salesTeamRepository.DeleteAsync(e => e.ParentType == parentType && e.ParentId == parentId, autoSave: true);
        await _salesTeamRepository.InsertManyAsync(newEntries, autoSave: true);

        return newEntries;
    }

    /// <summary>
    /// Copies sales team from a source document (e.g. Sales Order or original Delivery Note/Invoice)
    /// to a target document, recalculating allocations and incentives with target eligibleAmount.
    /// </summary>
    public async Task<List<SalesTeamEntry>> CopySalesTeamFromSourceAsync(
        string sourceType,
        Guid sourceId,
        string targetType,
        Guid targetId,
        decimal eligibleAmount)
    {
        var sourceEntries = await _salesTeamRepository.GetListAsync(e => e.ParentType == sourceType && e.ParentId == sourceId);
        if (sourceEntries.Count == 0) return new List<SalesTeamEntry>();

        var inputs = sourceEntries.Select(e => new SalesTeamAllocationInput
        {
            SalesPersonId = e.SalesPersonId,
            AllocatedPercentage = e.AllocatedPercentage,
            CommissionRate = e.CommissionRate
        });

        return await SetSalesTeamAsync(targetType, targetId, eligibleAmount, inputs);
    }
}
