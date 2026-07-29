using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Resolves contracted rates from active Blanket Orders for item selection.
/// Per ERPNext get_item_details.py update_party_blanket_order:
/// when an item is added to SO/PO, checks for active BO with remaining qty.
/// </summary>
public class BlanketOrderRateService : DomainService
{
    private readonly IRepository<BlanketOrder, Guid> _boRepository;

    public BlanketOrderRateService(IRepository<BlanketOrder, Guid> boRepository)
    {
        _boRepository = boRepository;
    }

    /// <summary>
    /// Finds the best matching blanket order rate for an item+party combination.
    /// Returns null when no active BO covers this item.
    /// </summary>
    public async Task<BlanketOrderRateResult?> GetRateAsync(
        Guid companyId, Guid partyId, Guid itemId,
        string orderType, DateTime transactionDate)
    {
        var queryable = await _boRepository.GetQueryableAsync();
        var orders = queryable
            .Where(bo => bo.CompanyId == companyId
                && bo.PartyId == partyId
                && bo.OrderType == orderType
                && bo.Status == Core.DocumentStatus.Submitted
                && bo.FromDate <= transactionDate
                && bo.ToDate >= transactionDate)
            .ToList();

        foreach (var bo in orders)
        {
            var matchingItem = bo.Items
                .FirstOrDefault(i => i.ItemId == itemId && i.RemainingQty > 0);

            if (matchingItem != null)
            {
                return new BlanketOrderRateResult(
                    bo.Id, bo.OrderNumber,
                    matchingItem.Rate, matchingItem.RemainingQty);
            }
        }

        return null;
    }
}

/// <summary>Result when a blanket order rate is found for an item.</summary>
public record BlanketOrderRateResult(
    Guid BlanketOrderId,
    string BlanketOrderNumber,
    decimal Rate,
    decimal RemainingQty);
