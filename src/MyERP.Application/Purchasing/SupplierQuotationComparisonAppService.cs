using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

/// <summary>
/// Supplier Quotation Comparison — procurement decision-support tool.
/// Creates a side-by-side comparison matrix of quotes from multiple suppliers for the same items.
/// Highlights lowest prices for each item.
/// Per ERPNext: supplier_quotation_comparison report (gotcha #5330).
/// </summary>
[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class SupplierQuotationComparisonAppService : ApplicationService, ISupplierQuotationComparisonAppService
{
    private readonly IRepository<SupplierQuotation, Guid> _sqRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<RequestForQuotation, Guid> _rfqRepository;

    public SupplierQuotationComparisonAppService(
        IRepository<SupplierQuotation, Guid> sqRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<RequestForQuotation, Guid> rfqRepository)
    {
        _sqRepository = sqRepository;
        _supplierRepository = supplierRepository;
        _rfqRepository = rfqRepository;
    }

    /// <summary>
    /// Gets a comparison matrix for all supplier quotations against a specific RFQ.
    /// Supports status filtering (Draft, Submitted, or all non-cancelled) and order status filtering ("Not Ordered", "Partially Ordered", "Ordered").
    /// </summary>
    public async Task<SupplierQuotationComparisonDto> GetComparisonByRfqAsync(Guid rfqId, string? status = null, string? orderStatus = null)
    {
        var sqQueryable = await _sqRepository.GetQueryableAsync();
        var supplierQueryable = await _supplierRepository.GetQueryableAsync();

        var query = sqQueryable.Where(sq => sq.RequestForQuotationId == rfqId);

        if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase))
            query = query.Where(sq => sq.Status == DocumentStatus.Draft);
        else if (string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase))
            query = query.Where(sq => sq.Status == DocumentStatus.Submitted || sq.Status == DocumentStatus.ToDeliverAndBill || sq.Status == DocumentStatus.Completed);
        else
            query = query.Where(sq => sq.Status != DocumentStatus.Cancelled);

        var quotations = query.OrderBy(sq => sq.SupplierName).ToList();

        // Filter by OrderStatus if specified (per ERPNext PR #58572)
        if (!string.IsNullOrWhiteSpace(orderStatus))
        {
            if (string.Equals(orderStatus, "Not Ordered", StringComparison.OrdinalIgnoreCase))
                quotations = quotations.Where(sq => sq.OrderStatus == "Not Ordered").ToList();
            else if (string.Equals(orderStatus, "Partially Ordered", StringComparison.OrdinalIgnoreCase))
                quotations = quotations.Where(sq => sq.OrderStatus == "Partially Ordered").ToList();
            else if (string.Equals(orderStatus, "Ordered", StringComparison.OrdinalIgnoreCase))
                quotations = quotations.Where(sq => sq.OrderStatus == "Ordered").ToList();
        }

        if (!quotations.Any())
            return new SupplierQuotationComparisonDto { RfqId = rfqId };

        // Build supplier lookup
        var supplierIds = quotations.Select(sq => sq.SupplierId).Distinct().ToArray();
        var suppliers = supplierQueryable
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s.Name);

        // Collect all unique items across all quotations
        var allItems = quotations
            .SelectMany(sq => sq.Items)
            .Select(i => new { i.ItemId, i.Description })
            .DistinctBy(x => x.ItemId)
            .OrderBy(x => x.Description)
            .ToList();

        // Build comparison matrix
        var result = new SupplierQuotationComparisonDto
        {
            RfqId = rfqId,
            Suppliers = quotations.Select(sq => new ComparisonSupplierDto
            {
                SupplierId = sq.SupplierId,
                SupplierName = suppliers.GetValueOrDefault(sq.SupplierId) ?? sq.SupplierName ?? "Unknown",
                QuotationId = sq.Id,
                QuotationNumber = sq.QuotationNumber,
                Currency = sq.Currency,
                ValidTill = sq.ValidTill,
                GrandTotal = sq.GrandTotal,
                OrderStatus = sq.OrderStatus,
                Status = (int)sq.Status,
            }).ToList(),
            Items = new List<ComparisonItemDto>(),
        };

        foreach (var item in allItems)
        {
            var compItem = new ComparisonItemDto
            {
                ItemId = item.ItemId,
                ItemDescription = item.Description ?? "",
                SupplierPrices = new List<ComparisonPriceDto>(),
            };

            decimal? lowestRate = null;

            foreach (var sq in quotations)
            {
                var sqItem = sq.Items.FirstOrDefault(i => i.ItemId == item.ItemId);
                var price = new ComparisonPriceDto
                {
                    SupplierId = sq.SupplierId,
                    QuotationId = sq.Id,
                    Rate = sqItem?.UnitPrice ?? 0,
                    Quantity = sqItem?.Quantity ?? 0,
                    Amount = sqItem?.Amount ?? 0,
                    LeadTimeDays = sqItem?.LeadTimeDays,
                    IsQuoted = sqItem != null,
                };

                if (sqItem != null && sqItem.UnitPrice > 0)
                {
                    if (!lowestRate.HasValue || sqItem.UnitPrice < lowestRate.Value)
                        lowestRate = sqItem.UnitPrice;
                }

                compItem.SupplierPrices.Add(price);
            }

            // Mark lowest price items
            if (lowestRate.HasValue)
            {
                foreach (var sp in compItem.SupplierPrices.Where(p => p.IsQuoted && p.Rate == lowestRate.Value))
                    sp.IsLowestPrice = true;
            }

            compItem.LowestRate = lowestRate ?? 0;
            result.Items.Add(compItem);
        }

        // Calculate total comparison summary
        result.LowestTotalAmount = result.Suppliers.Any()
            ? result.Suppliers.Min(s => s.GrandTotal)
            : 0;

        return result;
    }

    /// <summary>
    /// Gets a comparison for manually selected supplier quotations (not RFQ-linked).
    /// </summary>
    public async Task<SupplierQuotationComparisonDto> GetComparisonByIdsAsync(List<Guid> quotationIds)
    {
        if (quotationIds == null || quotationIds.Count < 2)
            throw new Volo.Abp.BusinessException("MyERP:04050")
                .WithData("reason", "At least 2 quotations required for comparison");

        var sqQueryable = await _sqRepository.GetQueryableAsync();
        var supplierQueryable = await _supplierRepository.GetQueryableAsync();

        var quotations = sqQueryable
            .Where(sq => quotationIds.Contains(sq.Id))
            .ToList();

        if (quotations.Count < 2)
            throw new Volo.Abp.BusinessException("MyERP:04050")
                .WithData("reason", "Fewer than 2 valid quotations found");

        var supplierIds = quotations.Select(sq => sq.SupplierId).Distinct().ToArray();
        var suppliers = supplierQueryable
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s.Name);

        var allItems = quotations
            .SelectMany(sq => sq.Items)
            .Select(i => new { i.ItemId, i.Description })
            .DistinctBy(x => x.ItemId)
            .OrderBy(x => x.Description)
            .ToList();

        var result = new SupplierQuotationComparisonDto
        {
            Suppliers = quotations.Select(sq => new ComparisonSupplierDto
            {
                SupplierId = sq.SupplierId,
                SupplierName = suppliers.GetValueOrDefault(sq.SupplierId) ?? sq.SupplierName ?? "Unknown",
                QuotationId = sq.Id,
                QuotationNumber = sq.QuotationNumber,
                Currency = sq.Currency,
                ValidTill = sq.ValidTill,
                GrandTotal = sq.GrandTotal,
                OrderStatus = sq.OrderStatus,
                Status = (int)sq.Status,
            }).ToList(),
            Items = new List<ComparisonItemDto>(),
        };

        foreach (var item in allItems)
        {
            var compItem = new ComparisonItemDto
            {
                ItemId = item.ItemId,
                ItemDescription = item.Description ?? "",
                SupplierPrices = new List<ComparisonPriceDto>(),
            };

            decimal? lowestRate = null;

            foreach (var sq in quotations)
            {
                var sqItem = sq.Items.FirstOrDefault(i => i.ItemId == item.ItemId);
                var price = new ComparisonPriceDto
                {
                    SupplierId = sq.SupplierId,
                    QuotationId = sq.Id,
                    Rate = sqItem?.UnitPrice ?? 0,
                    Quantity = sqItem?.Quantity ?? 0,
                    Amount = sqItem?.Amount ?? 0,
                    LeadTimeDays = sqItem?.LeadTimeDays,
                    IsQuoted = sqItem != null,
                };

                if (sqItem != null && sqItem.UnitPrice > 0)
                {
                    if (!lowestRate.HasValue || sqItem.UnitPrice < lowestRate.Value)
                        lowestRate = sqItem.UnitPrice;
                }

                compItem.SupplierPrices.Add(price);
            }

            if (lowestRate.HasValue)
            {
                foreach (var sp in compItem.SupplierPrices.Where(p => p.IsQuoted && p.Rate == lowestRate.Value))
                    sp.IsLowestPrice = true;
            }

            compItem.LowestRate = lowestRate ?? 0;
            result.Items.Add(compItem);
        }

        result.LowestTotalAmount = result.Suppliers.Any()
            ? result.Suppliers.Min(s => s.GrandTotal)
            : 0;

        return result;
    }
}
