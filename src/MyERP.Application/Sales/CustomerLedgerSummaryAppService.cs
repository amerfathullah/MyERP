using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class CustomerLedgerSummaryAppService : ApplicationService, ICustomerLedgerSummaryAppService
{
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepo;
    private readonly IRepository<PaymentEntry, Guid> _paymentRepo;
    private readonly IRepository<JournalEntry, Guid> _journalRepo;
    private readonly IRepository<Company, Guid> _companyRepo;

    public CustomerLedgerSummaryAppService(
        IRepository<Customer, Guid> customerRepo,
        IRepository<SalesInvoice, Guid> invoiceRepo,
        IRepository<PaymentEntry, Guid> paymentRepo,
        IRepository<JournalEntry, Guid> journalRepo,
        IRepository<Company, Guid> companyRepo)
    {
        _customerRepo = customerRepo;
        _invoiceRepo = invoiceRepo;
        _paymentRepo = paymentRepo;
        _journalRepo = journalRepo;
        _companyRepo = companyRepo;
    }

    public async Task<CustomerLedgerSummaryReportDto> GetReportAsync(CustomerLedgerSummaryRequestDto input)
    {
        var company = await _companyRepo.FindAsync(input.CompanyId);
        var companyCurrency = company?.CurrencyCode ?? "MYR";

        var today = DateTime.Today;
        var fromDate = input.FromDate?.Date ?? new DateTime(today.Year, today.Month, 1).AddMonths(-1);
        var toDate = input.ToDate?.Date.AddDays(1).AddTicks(-1) ?? today.AddDays(1).AddTicks(-1);

        // 1. Load Customers
        var custQuery = await _customerRepo.GetQueryableAsync();
        custQuery = custQuery.Where(c => c.CompanyId == input.CompanyId);

        if (input.CustomerId.HasValue)
        {
            custQuery = custQuery.Where(c => c.Id == input.CustomerId.Value);
        }
        if (input.CustomerGroupId.HasValue)
        {
            custQuery = custQuery.Where(c => c.CustomerGroupId == input.CustomerGroupId.Value);
        }
        if (input.TerritoryId.HasValue)
        {
            custQuery = custQuery.Where(c => c.TerritoryId == input.TerritoryId.Value);
        }

        var customers = custQuery.ToList();
        var customerIds = customers.Select(c => c.Id).ToHashSet();

        if (customerIds.Count == 0)
        {
            return new CustomerLedgerSummaryReportDto
            {
                CompanyCurrency = companyCurrency,
                FromDate = fromDate,
                ToDate = toDate,
                Rows = new(),
                TotalRow = new() { CustomerName = "Total", Currency = companyCurrency }
            };
        }

        // 2. Territory and Customer Group lookup
        var groupDict = new Dictionary<Guid, string>();
        var territoryDict = new Dictionary<Guid, string>();

        var groupIds = customers.Where(c => c.CustomerGroupId.HasValue).Select(c => c.CustomerGroupId!.Value).Distinct().ToList();
        if (groupIds.Count > 0)
        {
            var groupRepo = LazyServiceProvider.LazyGetService<IRepository<CustomerGroup, Guid>>();
            if (groupRepo != null)
            {
                var gQuery = await groupRepo.GetQueryableAsync();
                groupDict = gQuery.Where(g => groupIds.Contains(g.Id)).ToDictionary(g => g.Id, g => g.Name);
            }
        }

        var territoryIds = customers.Where(c => c.TerritoryId.HasValue).Select(c => c.TerritoryId!.Value).Distinct().ToList();
        if (territoryIds.Count > 0)
        {
            var territoryRepo = LazyServiceProvider.LazyGetService<IRepository<Territory, Guid>>();
            if (territoryRepo != null)
            {
                var tQuery = await territoryRepo.GetQueryableAsync();
                territoryDict = tQuery.Where(t => territoryIds.Contains(t.Id)).ToDictionary(t => t.Id, t => t.Name);
            }
        }

        // 3. Load Invoices up to toDate
        var invQuery = await _invoiceRepo.GetQueryableAsync();
        var invoices = invQuery
            .Where(i => i.CompanyId == input.CompanyId
                        && i.Status != DocumentStatus.Draft
                        && i.Status != DocumentStatus.Cancelled
                        && customerIds.Contains(i.CustomerId)
                        && i.IssueDate <= toDate)
            .ToList();

        // 4. Load Payments up to toDate
        var payQuery = await _paymentRepo.GetQueryableAsync();
        var payments = payQuery
            .Where(p => p.CompanyId == input.CompanyId
                        && p.PartyType == "Customer"
                        && p.PartyId.HasValue
                        && customerIds.Contains(p.PartyId.Value)
                        && p.Status != DocumentStatus.Draft
                        && p.Status != DocumentStatus.Cancelled
                        && p.PostingDate <= toDate)
            .ToList();

        // 5. Load Manual Journal Entry lines up to toDate
        var jeQuery = await _journalRepo.WithDetailsAsync(j => j.Lines);
        var journals = jeQuery
            .Where(j => j.CompanyId == input.CompanyId
                        && j.Status == DocumentStatus.Posted
                        && (j.ReferenceType == null || j.ReferenceType == "JournalEntry")
                        && j.PostingDate <= toDate)
            .ToList();

        var flatJeLines = journals
            .SelectMany(j => j.Lines.Select(l => new { j.PostingDate, Line = l }))
            .Where(x => x.Line.PartyType == "Customer" && x.Line.PartyId.HasValue && customerIds.Contains(x.Line.PartyId.Value))
            .ToList();

        // 6. Aggregate by Customer
        var rows = new List<CustomerLedgerSummaryRowDto>();

        foreach (var customer in customers)
        {
            var cId = customer.Id;

            // Invoices: opening vs in-period
            var priorInvoices = invoices.Where(i => i.CustomerId == cId && i.IssueDate < fromDate).ToList();
            var periodInvoices = invoices.Where(i => i.CustomerId == cId && i.IssueDate >= fromDate && i.IssueDate <= toDate).ToList();

            decimal openingBalance = 0m;
            foreach (var inv in priorInvoices)
            {
                var amt = Math.Abs(inv.BaseGrandTotal != 0 ? inv.BaseGrandTotal : inv.GrandTotal);
                openingBalance += inv.IsReturn ? -amt : amt;
            }

            decimal invoicedAmount = 0m;
            decimal returnAmount = 0m;
            foreach (var inv in periodInvoices)
            {
                var amt = Math.Abs(inv.BaseGrandTotal != 0 ? inv.BaseGrandTotal : inv.GrandTotal);
                if (inv.IsReturn)
                {
                    returnAmount += amt;
                }
                else
                {
                    invoicedAmount += amt;
                }
            }

            // Payments: opening vs in-period
            var priorPayments = payments.Where(p => p.PartyId == cId && p.PostingDate < fromDate).ToList();
            var periodPayments = payments.Where(p => p.PartyId == cId && p.PostingDate >= fromDate && p.PostingDate <= toDate).ToList();

            foreach (var pay in priorPayments)
            {
                var rate = pay.ExchangeRate > 0 ? pay.ExchangeRate : 1m;
                var amt = pay.PaidAmount * rate;
                // Receive reduces customer debit balance; Pay (refund) increases customer debit balance
                openingBalance += pay.PaymentType == PaymentType.Receive ? -amt : amt;
            }

            decimal paidAmount = 0m;
            foreach (var pay in periodPayments)
            {
                var rate = pay.ExchangeRate > 0 ? pay.ExchangeRate : 1m;
                var amt = pay.PaidAmount * rate;
                if (pay.PaymentType == PaymentType.Receive)
                {
                    paidAmount += amt;
                }
                else
                {
                    paidAmount -= amt;
                }
            }

            // Manual Journal entries
            var priorJeLines = flatJeLines.Where(x => x.Line.PartyId == cId && x.PostingDate < fromDate).ToList();
            var periodJeLines = flatJeLines.Where(x => x.Line.PartyId == cId && x.PostingDate >= fromDate && x.PostingDate <= toDate).ToList();

            foreach (var jl in priorJeLines)
            {
                openingBalance += jl.Line.IsDebit ? jl.Line.Amount : -jl.Line.Amount;
            }

            decimal adjustmentAmount = 0m;
            foreach (var jl in periodJeLines)
            {
                adjustmentAmount += jl.Line.IsDebit ? jl.Line.Amount : -jl.Line.Amount;
            }

            var closingBalance = openingBalance + invoicedAmount - paidAmount - returnAmount + adjustmentAmount;

            // Only include customers with activity or balance (matching ERPNext behavior)
            if (openingBalance == 0m && invoicedAmount == 0m && paidAmount == 0m && returnAmount == 0m && adjustmentAmount == 0m && closingBalance == 0m)
            {
                continue;
            }

            string? groupName = null;
            if (customer.CustomerGroupId.HasValue) groupDict.TryGetValue(customer.CustomerGroupId.Value, out groupName);

            string? territoryName = null;
            if (customer.TerritoryId.HasValue) territoryDict.TryGetValue(customer.TerritoryId.Value, out territoryName);

            var drOrCr = closingBalance > 0.005m ? "Dr" : (closingBalance < -0.005m ? "Cr" : string.Empty);

            rows.Add(new CustomerLedgerSummaryRowDto
            {
                CustomerId = customer.Id,
                CustomerCode = customer.CustomerCode ?? customer.Name,
                CustomerName = customer.Name,
                CustomerGroupName = groupName,
                TerritoryName = territoryName,
                OpeningBalance = Math.Round(openingBalance, 2),
                InvoicedAmount = Math.Round(invoicedAmount, 2),
                PaidAmount = Math.Round(paidAmount, 2),
                ReturnAmount = Math.Round(returnAmount, 2),
                AdjustmentAmount = Math.Round(adjustmentAmount, 2),
                ClosingBalance = Math.Round(closingBalance, 2),
                DrOrCr = drOrCr,
                Currency = companyCurrency,
            });
        }

        rows = rows.OrderBy(r => r.CustomerName).ToList();

        // Total Row
        var totalRow = new CustomerLedgerSummaryRowDto
        {
            CustomerCode = "Total",
            CustomerName = "Total",
            OpeningBalance = rows.Sum(r => r.OpeningBalance),
            InvoicedAmount = rows.Sum(r => r.InvoicedAmount),
            PaidAmount = rows.Sum(r => r.PaidAmount),
            ReturnAmount = rows.Sum(r => r.ReturnAmount),
            AdjustmentAmount = rows.Sum(r => r.AdjustmentAmount),
            ClosingBalance = rows.Sum(r => r.ClosingBalance),
            Currency = companyCurrency,
        };
        totalRow.DrOrCr = totalRow.ClosingBalance > 0.005m ? "Dr" : (totalRow.ClosingBalance < -0.005m ? "Cr" : string.Empty);

        return new CustomerLedgerSummaryReportDto
        {
            CompanyCurrency = companyCurrency,
            FromDate = fromDate,
            ToDate = toDate,
            Rows = rows,
            TotalRow = totalRow,
        };
    }
}
