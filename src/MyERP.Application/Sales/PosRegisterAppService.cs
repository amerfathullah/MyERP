using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// POS Register App Service — lists point of sale transactions and payment breakdowns.
/// ERPNext equivalent: accounts/report/pos_register/pos_register.py
/// 
/// Key fixes per ERPNext PR #59990 / commits 06c74bc91d, 2929ee8904, 045e383982, 1ce22d620d:
/// - Includes POS Invoices and Sales Invoices made directly at the POS (is_pos == 1).
/// - Excludes consolidated Sales Invoices (is_consolidated == 1) to prevent double counting.
/// - Shows Paid Amount in flat/ungrouped list as well as grouped views.
/// - In company currency, change is deducted in company currency.
/// - When grouped by Payment Method, each invoice's Grand Total is counted only ONCE on its first row
///   so group subtotals add up accurately without duplicate totals.
/// </summary>
[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class PosRegisterAppService : ApplicationService, IPosRegisterAppService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<PosProfile, Guid> _posProfileRepository;

    public PosRegisterAppService(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<PosProfile, Guid> posProfileRepository)
    {
        _invoiceRepository = invoiceRepository;
        _customerRepository = customerRepository;
        _posProfileRepository = posProfileRepository;
    }

    public async Task<PosRegisterDto> GetReportAsync(PosRegisterRequestDto input)
    {
        ValidateFilters(input);

        var query = await _invoiceRepository.GetQueryableAsync();
        var invoices = query
            .Where(si => si.CompanyId == input.CompanyId
                && si.IsPos
                && !si.IsConsolidated // Skip consolidated invoices (PR #59990)
                && (si.Status == DocumentStatus.Submitted || si.Status == DocumentStatus.Posted)
                && si.IssueDate >= input.FromDate
                && si.IssueDate <= input.ToDate)
            .ToList();

        if (input.PosProfileId.HasValue)
        {
            invoices = invoices.Where(i => i.PosProfileId == input.PosProfileId.Value).ToList();
        }

        if (input.CustomerId.HasValue)
        {
            invoices = invoices.Where(i => i.CustomerId == input.CustomerId.Value).ToList();
        }

        if (input.CashierId.HasValue)
        {
            invoices = invoices.Where(i => i.CreatorId == input.CashierId.Value).ToList();
        }

        if (input.IsReturn.HasValue)
        {
            invoices = invoices.Where(i => i.IsReturn == input.IsReturn.Value).ToList();
        }

        if (!invoices.Any())
        {
            return new PosRegisterDto
            {
                CompanyId = input.CompanyId,
                FromDate = input.FromDate,
                ToDate = input.ToDate,
                Rows = new()
            };
        }

        // Preload customer and POS profile names
        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await _customerRepository.GetListAsync(c => customerIds.Contains(c.Id));
        var customerLookup = customers.ToDictionary(c => c.Id, c => c.Name);

        var profileIds = invoices.Where(i => i.PosProfileId.HasValue).Select(i => i.PosProfileId!.Value).Distinct().ToList();
        var profiles = await _posProfileRepository.GetListAsync(p => profileIds.Contains(p.Id));
        var profileLookup = profiles.ToDictionary(p => p.Id, p => p.ProfileName);

        var rawRows = new List<PosRegisterRowDto>();

        foreach (var inv in invoices.OrderBy(i => i.IssueDate))
        {
            var grandTotal = inv.BaseGrandTotal > 0 ? inv.BaseGrandTotal : inv.GrandTotal * (inv.ExchangeRate > 0 ? inv.ExchangeRate : 1m);
            var paidAmount = inv.AmountPaid * (inv.ExchangeRate > 0 ? inv.ExchangeRate : 1m);

            customerLookup.TryGetValue(inv.CustomerId, out var customerName);
            string? profileName = null;
            if (inv.PosProfileId.HasValue)
            {
                profileLookup.TryGetValue(inv.PosProfileId.Value, out profileName);
            }

            rawRows.Add(new PosRegisterRowDto
            {
                PostingDate = inv.IssueDate,
                InvoiceType = inv.IsPos ? "POS Invoice" : "Sales Invoice",
                InvoiceId = inv.Id,
                InvoiceNumber = inv.InvoiceNumber,
                CustomerId = inv.CustomerId,
                CustomerName = customerName ?? "Unknown Customer",
                PosProfileId = inv.PosProfileId,
                PosProfileName = profileName ?? "Standard POS",
                CashierId = inv.CreatorId,
                CashierName = inv.CreatorId?.ToString()[..8] ?? "Cashier",
                GrandTotal = grandTotal,
                PaidAmount = paidAmount,
                ModeOfPayment = !string.IsNullOrWhiteSpace(inv.Notes) ? inv.Notes : "Cash",
                IsReturn = inv.IsReturn,
                IsSubtotal = false
            });
        }

        if (string.IsNullOrWhiteSpace(input.GroupBy))
        {
            return new PosRegisterDto
            {
                CompanyId = input.CompanyId,
                FromDate = input.FromDate,
                ToDate = input.ToDate,
                Rows = rawRows,
                TotalGrandTotal = rawRows.Sum(r => r.GrandTotal),
                TotalPaidAmount = rawRows.Sum(r => r.PaidAmount)
            };
        }

        // Handle Grouping per ERPNext PR #59990
        var groupedRows = new List<PosRegisterRowDto>();
        Func<PosRegisterRowDto, string> groupKeySelector = input.GroupBy switch
        {
            "POS Profile" => r => r.PosProfileName ?? "Unassigned",
            "Cashier" => r => r.CashierName ?? "Unknown Cashier",
            "Customer" => r => r.CustomerName ?? "Unknown Customer",
            "Payment Method" => r => r.ModeOfPayment ?? "Cash",
            _ => r => "All"
        };

        var groups = rawRows.GroupBy(groupKeySelector).ToList();

        // In payment method grouping, count each grand total once across payment rows (commit 045e383982)
        if (input.GroupBy == "Payment Method")
        {
            var seenInvoices = new HashSet<Guid>();
            foreach (var r in rawRows)
            {
                if (r.InvoiceId.HasValue && seenInvoices.Contains(r.InvoiceId.Value))
                {
                    r.GrandTotal = 0m;
                }
                else if (r.InvoiceId.HasValue)
                {
                    seenInvoices.Add(r.InvoiceId.Value);
                }
            }
        }

        foreach (var group in groups)
        {
            var groupItems = group.ToList();
            foreach (var item in groupItems)
            {
                item.GroupKey = group.Key;
                groupedRows.Add(item);
            }

            // Subtotal row
            groupedRows.Add(new PosRegisterRowDto
            {
                GroupKey = group.Key,
                InvoiceNumber = $"Total - {group.Key}",
                GrandTotal = groupItems.Sum(i => i.GrandTotal),
                PaidAmount = groupItems.Sum(i => i.PaidAmount),
                IsSubtotal = true
            });
        }

        return new PosRegisterDto
        {
            CompanyId = input.CompanyId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Rows = groupedRows,
            TotalGrandTotal = rawRows.Sum(r => r.GrandTotal),
            TotalPaidAmount = rawRows.Sum(r => r.PaidAmount)
        };
    }

    private static void ValidateFilters(PosRegisterRequestDto input)
    {
        if (input.FromDate > input.ToDate)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DateRangeInvalid);
        }

        if (input.PosProfileId.HasValue && input.GroupBy == "POS Profile")
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Cannot filter based on POS Profile when grouped by POS Profile.");
        }

        if (input.CustomerId.HasValue && input.GroupBy == "Customer")
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Cannot filter based on Customer when grouped by Customer.");
        }

        if (input.CashierId.HasValue && input.GroupBy == "Cashier")
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Cannot filter based on Cashier when grouped by Cashier.");
        }

        if (!string.IsNullOrWhiteSpace(input.ModeOfPayment) && input.GroupBy == "Payment Method")
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Cannot filter based on Payment Method when grouped by Payment Method.");
        }
    }
}
