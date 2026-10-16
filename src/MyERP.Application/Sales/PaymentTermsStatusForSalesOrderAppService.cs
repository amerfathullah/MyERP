using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize]
public class PaymentTermsStatusForSalesOrderAppService : ApplicationService, IPaymentTermsStatusForSalesOrderAppService
{
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo;
    private readonly IRepository<PaymentTermsTemplate, Guid> _paymentTermsRepo;

    public PaymentTermsStatusForSalesOrderAppService(
        IRepository<SalesOrder, Guid> salesOrderRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<SalesInvoice, Guid> salesInvoiceRepo,
        IRepository<PaymentTermsTemplate, Guid> paymentTermsRepo)
    {
        _salesOrderRepo = salesOrderRepo;
        _customerRepo = customerRepo;
        _salesInvoiceRepo = salesInvoiceRepo;
        _paymentTermsRepo = paymentTermsRepo;
    }

    public async Task<PaymentTermsStatusForSalesOrderReportDto> GetReportAsync(PaymentTermsStatusForSalesOrderRequestDto input)
    {
        var soQuery = await _salesOrderRepo.WithDetailsAsync(x => x.Items);
        var orders = soQuery
            .Where(x => x.CompanyId == input.CompanyId && x.Status != DocumentStatus.Draft && x.Status != DocumentStatus.Cancelled)
            .ToList();

        if (input.CustomerId.HasValue)
        {
            orders = orders.Where(x => x.CustomerId == input.CustomerId.Value).ToList();
        }

        if (input.SalesOrderId.HasValue)
        {
            orders = orders.Where(x => x.Id == input.SalesOrderId.Value).ToList();
        }

        if (input.FromDate.HasValue)
        {
            orders = orders.Where(x => x.OrderDate.Date >= input.FromDate.Value.Date).ToList();
        }

        if (input.ToDate.HasValue)
        {
            orders = orders.Where(x => x.OrderDate.Date <= input.ToDate.Value.Date).ToList();
        }

        orders = orders.OrderBy(x => x.OrderNumber).ToList();

        if (orders.Count == 0)
        {
            return new PaymentTermsStatusForSalesOrderReportDto
            {
                CompanyId = input.CompanyId,
                Rows = new List<PaymentTermsStatusForSalesOrderRowDto>()
            };
        }

        var customerIds = orders.Select(x => x.CustomerId).Distinct().ToList();
        var customers = (await _customerRepo.GetListAsync(c => customerIds.Contains(c.Id)))
            .ToDictionary(c => c.Id);

        var templateIds = customers.Values
            .Where(c => c.DefaultPaymentTermsTemplateId.HasValue)
            .Select(c => c.DefaultPaymentTermsTemplateId!.Value)
            .Distinct()
            .ToList();

        var templateQuery = await _paymentTermsRepo.WithDetailsAsync(t => t.Terms);
        var templates = templateQuery
            .Where(t => templateIds.Contains(t.Id))
            .ToDictionary(t => t.Id);

        var allSoItemIds = orders.SelectMany(x => x.Items).Select(i => i.Id).ToHashSet();

        // Invoices referencing these sales orders
        var invoiceQuery = await _salesInvoiceRepo.WithDetailsAsync(i => i.Items);
        var submittedInvoices = invoiceQuery
            .Where(i => i.CompanyId == input.CompanyId && i.Status != DocumentStatus.Draft && i.Status != DocumentStatus.Cancelled)
            .ToList();

        // Map (Invoice, SalesOrder) -> net amount
        var invoiceOrderRows = new List<InvoiceOrderLine>();
        foreach (var inv in submittedInvoices)
        {
            var matchingLines = inv.Items.Where(it => it.SalesOrderItemId.HasValue && allSoItemIds.Contains(it.SalesOrderItemId.Value)).ToList();
            if (matchingLines.Count == 0) continue;

            var groups = matchingLines
                .GroupBy(it => orders.First(o => o.Items.Any(oi => oi.Id == it.SalesOrderItemId!.Value)).Id)
                .ToList();

            var totalNet = inv.Items.Sum(it => it.LineTotal);
            if (totalNet <= 0) totalNet = 1m;

            var grandTotal = inv.GrandTotal;
            var allocatedSoFar = 0m;

            for (var idx = 0; idx < groups.Count; idx++)
            {
                var grp = groups[idx];
                var orderId = grp.Key;
                var orderNet = grp.Sum(it => it.LineTotal);
                decimal orderShare;

                if (idx == groups.Count - 1)
                {
                    // Last group absorbs residual
                    orderShare = grandTotal - allocatedSoFar;
                }
                else
                {
                    orderShare = Math.Round(grandTotal * (orderNet / totalNet), 2);
                    allocatedSoFar += orderShare;
                }

                invoiceOrderRows.Add(new InvoiceOrderLine
                {
                    InvoiceId = inv.Id,
                    InvoiceNumber = inv.InvoiceNumber,
                    PostingDate = inv.IssueDate,
                    SalesOrderId = orderId,
                    AllocatedAmount = orderShare
                });
            }
        }

        var today = DateTime.UtcNow.Date;
        var allRows = new List<PaymentTermsStatusForSalesOrderRowDto>();

        foreach (var so in orders)
        {
            customers.TryGetValue(so.CustomerId, out var cust);
            PaymentTermsTemplate? template = null;
            if (cust?.DefaultPaymentTermsTemplateId.HasValue == true)
            {
                templates.TryGetValue(cust.DefaultPaymentTermsTemplateId.Value, out template);
            }

            List<PaymentScheduleLine> schedule;
            if (template != null && template.Terms.Count > 0)
            {
                schedule = template.GenerateSchedule(so.OrderDate, so.GrandTotal);
            }
            else
            {
                schedule = new List<PaymentScheduleLine>
                {
                    new()
                    {
                        DueDate = so.DeliveryDate ?? so.OrderDate,
                        InvoicePortion = 100m,
                        PaymentAmount = so.GrandTotal,
                        Description = "Full Payment"
                    }
                };
            }

            var soAllocations = invoiceOrderRows
                .Where(x => x.SalesOrderId == so.Id)
                .OrderBy(x => x.PostingDate)
                .ThenBy(x => x.InvoiceNumber)
                .Select(x => new InvoiceAllocationTracker
                {
                    InvoiceNumber = x.InvoiceNumber,
                    RemainingAmount = x.AllocatedAmount
                })
                .ToList();

            foreach (var term in schedule.OrderBy(t => t.DueDate))
            {
                var termNeeded = term.PaymentAmount;
                var paidAmount = 0m;
                var linkedInvoices = new List<string>();

                foreach (var alloc in soAllocations)
                {
                    if (alloc.RemainingAmount <= 0) continue;
                    if (termNeeded <= 0) break;

                    var take = Math.Min(termNeeded, alloc.RemainingAmount);
                    alloc.RemainingAmount -= take;
                    termNeeded -= take;
                    paidAmount += take;
                    if (!linkedInvoices.Contains(alloc.InvoiceNumber))
                    {
                        linkedInvoices.Add(alloc.InvoiceNumber);
                    }
                }

                string status;
                if (paidAmount >= term.PaymentAmount && term.PaymentAmount > 0)
                {
                    status = "Completed";
                }
                else if (paidAmount > 0)
                {
                    status = "Partly Paid";
                }
                else if (term.DueDate.Date < today)
                {
                    status = "Overdue";
                }
                else
                {
                    status = "Unpaid";
                }

                allRows.Add(new PaymentTermsStatusForSalesOrderRowDto
                {
                    SalesOrderId = so.Id,
                    SalesOrderNumber = so.OrderNumber,
                    CustomerId = so.CustomerId,
                    CustomerName = cust?.Name ?? "Unknown",
                    OrderDate = so.OrderDate,
                    PaymentTermName = term.Description ?? $"Term ({term.InvoicePortion:0.#}%)",
                    Description = term.Description,
                    DueDate = term.DueDate,
                    InvoicePortion = term.InvoicePortion,
                    PaymentAmount = term.PaymentAmount,
                    PaidAmount = paidAmount,
                    Invoices = string.Join(", ", linkedInvoices),
                    Status = status,
                    Currency = so.CurrencyCode ?? "MYR"
                });
            }
        }

        // Filter on Status
        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            allRows = allRows
                .Where(r => string.Equals(r.Status, input.Status, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Filter for immediate upcoming term
        if (input.OnlyImmediateUpcomingTerm == true)
        {
            var seen = new HashSet<Guid>();
            var filtered = new List<PaymentTermsStatusForSalesOrderRowDto>();
            foreach (var row in allRows)
            {
                if (!seen.Contains(row.SalesOrderId) && row.DueDate.Date >= today)
                {
                    filtered.Add(row);
                    seen.Add(row.SalesOrderId);
                }
            }
            allRows = filtered;
        }

        var totalPaymentAmount = allRows.Sum(r => r.PaymentAmount);
        var totalPaidAmount = allRows.Sum(r => r.PaidAmount);

        return new PaymentTermsStatusForSalesOrderReportDto
        {
            CompanyId = input.CompanyId,
            Rows = allRows,
            TotalSalesOrders = allRows.Select(r => r.SalesOrderId).Distinct().Count(),
            TotalPaymentTerms = allRows.Count,
            TotalPaymentAmount = totalPaymentAmount,
            TotalPaidAmount = totalPaidAmount,
            TotalOutstandingAmount = totalPaymentAmount - totalPaidAmount,
            CompletedTermsCount = allRows.Count(r => r.Status == "Completed"),
            OverdueTermsCount = allRows.Count(r => r.Status == "Overdue")
        };
    }

    private class InvoiceOrderLine
    {
        public Guid InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = null!;
        public DateTime PostingDate { get; set; }
        public Guid SalesOrderId { get; set; }
        public decimal AllocatedAmount { get; set; }
    }

    private class InvoiceAllocationTracker
    {
        public string InvoiceNumber { get; set; } = null!;
        public decimal RemainingAmount { get; set; }
    }
}
