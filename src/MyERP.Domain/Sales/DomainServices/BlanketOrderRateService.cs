using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.CRM.Entities;
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
    /// Supports multi-currency rate resolution per ERPNext PR #58472.
    /// Returns null when no active BO covers this item.
    /// </summary>
    public async Task<BlanketOrderRateResult?> GetRateAsync(
        Guid companyId, Guid partyId, Guid itemId,
        string orderType, DateTime transactionDate,
        string? targetCurrency = null, decimal targetExchangeRate = 1m,
        Guid? blanketOrderId = null)
    {
        if (partyId == Guid.Empty || itemId == Guid.Empty)
            return null;

        var queryable = await _boRepository.GetQueryableAsync();
        var orders = queryable
            .Where(bo => bo.CompanyId == companyId
                && (!blanketOrderId.HasValue || bo.Id == blanketOrderId.Value)
                && bo.PartyId == partyId
                && bo.OrderType == orderType
                && bo.Status == Core.DocumentStatus.Submitted
                && bo.FromDate <= transactionDate
                && bo.ToDate >= transactionDate)
            .ToList();

        foreach (var bo in orders)
        {
            var matchingItem = bo.Items
                .FirstOrDefault(i => i.ItemId == itemId && !i.IsClosed && i.RemainingQty > 0);

            if (matchingItem != null)
            {
                var baseRate = matchingItem.BaseRate > 0 ? matchingItem.BaseRate : Math.Round(matchingItem.Rate * bo.ExchangeRate, 4);
                decimal resolvedRate;

                if (string.IsNullOrEmpty(targetCurrency) || string.Equals(targetCurrency, bo.Currency, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedRate = matchingItem.Rate;
                }
                else
                {
                    // Convert through base company currency
                    resolvedRate = targetExchangeRate > 0
                        ? Math.Round(baseRate / targetExchangeRate, 4)
                        : matchingItem.Rate;
                }

                return new BlanketOrderRateResult(
                    bo.Id, bo.OrderNumber,
                    resolvedRate, matchingItem.RemainingQty,
                    baseRate, bo.Currency, bo.ExchangeRate);
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the Customer ID converted from a Lead or Prospect (per ERPNext
    /// get_existing_customer_name / commits ad16ee4b4b, b8c9594a17).
    /// Returns null when the Lead/Prospect has no converted Customer.
    /// </summary>
    public async Task<Guid?> ResolveCustomerIdAsync(
        Guid? customerId,
        Guid? leadId,
        Guid? prospectId,
        Guid? companyId = null)
    {
        if (customerId.HasValue && customerId.Value != Guid.Empty)
            return customerId.Value;

        var customerRepo = LazyServiceProvider?.LazyGetService<IRepository<Customer, Guid>>();

        if (prospectId.HasValue && prospectId.Value != Guid.Empty)
        {
            var prospectRepo = LazyServiceProvider?.LazyGetService<IRepository<Prospect, Guid>>();
            if (prospectRepo != null)
            {
                var prospect = await prospectRepo.FindAsync(prospectId.Value);
                if (prospect?.ConvertedCustomerId is Guid converted && converted != Guid.Empty)
                    return converted;
            }

            if (customerRepo != null)
            {
                var custQuery = await customerRepo.GetQueryableAsync();
                if (custQuery != null)
                {
                    var match = custQuery.FirstOrDefault(c =>
                        c.ProspectId == prospectId.Value &&
                        (!companyId.HasValue || c.CompanyId == companyId.Value));
                    if (match != null)
                        return match.Id;
                }
            }
        }

        if (leadId.HasValue && leadId.Value != Guid.Empty)
        {
            var leadRepo = LazyServiceProvider?.LazyGetService<IRepository<Lead, Guid>>();
            if (leadRepo != null)
            {
                var lead = await leadRepo.FindAsync(leadId.Value);
                if (lead?.ConvertedCustomerId is Guid converted && converted != Guid.Empty)
                    return converted;
            }

            if (customerRepo != null)
            {
                var custQuery = await customerRepo.GetQueryableAsync();
                if (custQuery != null)
                {
                    var match = custQuery.FirstOrDefault(c =>
                        c.LeadId == leadId.Value &&
                        (!companyId.HasValue || c.CompanyId == companyId.Value));
                    if (match != null)
                        return match.Id;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves or clears a row's Blanket Order link and rate when the row's item is changed
    /// (per ERPNext commits 51df5c2ef0, f6cca3f59e, and 09fd286163):
    /// - If againstBlanketOrder is false, or partyId is null/empty (e.g. Quotation to a Lead without a converted Customer),
    ///   clears (BlanketOrderId: null, BlanketOrderRate: 0m).
    /// - If againstBlanketOrder is true, queries for an eligible Blanket Order matching company, party, item, orderType,
    ///   transactionDate, currency, and (when set) existingBlanketOrderId. If no match exists, clears the stale
    ///   BlanketOrderId and resets BlanketOrderRate to 0m.
    /// </summary>
    public async Task<(Guid? BlanketOrderId, decimal BlanketOrderRate)> ResolveRowBlanketOrderOnItemChangeAsync(
        Guid companyId,
        Guid? partyId,
        Guid itemId,
        string orderType,
        DateTime transactionDate,
        bool againstBlanketOrder,
        Guid? existingBlanketOrderId = null,
        string? targetCurrency = null,
        decimal targetExchangeRate = 1m)
    {
        if (!againstBlanketOrder || !partyId.HasValue || partyId.Value == Guid.Empty)
        {
            return (null, 0m);
        }

        var match = await GetRateAsync(
            companyId,
            partyId.Value,
            itemId,
            orderType,
            transactionDate,
            targetCurrency,
            targetExchangeRate,
            existingBlanketOrderId);

        if (match == null)
        {
            return (null, 0m);
        }

        return (match.BlanketOrderId, match.Rate);
    }

    /// <summary>
    /// Filters active Blanket Orders for link-field search using search text (<paramref name="searchText"/>) and
    /// pagination (<paramref name="start"/>, <paramref name="pageLength"/>)
    /// per ERPNext commit 1f8513d996 (<c>queries.py</c>).
    /// </summary>
    public static List<(Guid Id, string OrderNumber)> SearchBlanketOrdersForLinkField(
        IEnumerable<(Guid Id, string OrderNumber, Guid CompanyId, Guid PartyId, string OrderType, Core.DocumentStatus Status, DateTime ToDate)> orders,
        Guid companyId,
        Guid partyId,
        string orderType,
        DateTime transactionDate,
        string? searchText = null,
        int start = 0,
        int pageLength = 20)
    {
        if (orders == null)
        {
            return new List<(Guid Id, string OrderNumber)>();
        }

        var query = orders.Where(bo =>
            bo.CompanyId == companyId
            && bo.PartyId == partyId
            && string.Equals(bo.OrderType, orderType, StringComparison.OrdinalIgnoreCase)
            && bo.Status == Core.DocumentStatus.Submitted
            && bo.ToDate >= transactionDate);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            query = query.Where(bo => bo.OrderNumber != null && bo.OrderNumber.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .Skip(Math.Max(0, start))
            .Take(pageLength > 0 ? pageLength : 20)
            .Select(bo => (bo.Id, bo.OrderNumber))
            .ToList();
    }
}

/// <summary>Result when a blanket order rate is found for an item.</summary>
public record BlanketOrderRateResult(
    Guid BlanketOrderId,
    string BlanketOrderNumber,
    decimal Rate,
    decimal RemainingQty,
    decimal BaseRate = 0m,
    string Currency = "MYR",
    decimal ExchangeRate = 1m);
