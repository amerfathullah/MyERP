using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

public class SalesRegisterLineDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Outstanding { get; set; }
    public bool IsReturn { get; set; }
}

public class RegisterReportDto<T>
{
    public List<T> Items { get; set; } = new();
    public decimal TotalNet { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalGrand { get; set; }
    public int Count { get; set; }
}

public class RegisterFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class SalesRegisterAppService : ApplicationService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;

    public SalesRegisterAppService(IRepository<SalesInvoice, Guid> invoiceRepository)
        => _invoiceRepository = invoiceRepository;

    public async Task<RegisterReportDto<SalesRegisterLineDto>> GetReportAsync(RegisterFilterDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;

        var query = await _invoiceRepository.GetQueryableAsync();
        var invoices = query
            .Where(si => si.CompanyId == input.CompanyId
                      && si.Status == DocumentStatus.Posted
                      && si.IssueDate >= from
                      && si.IssueDate <= to)
            .OrderByDescending(si => si.IssueDate)
            .ToList();

        var result = new RegisterReportDto<SalesRegisterLineDto>
        {
            Count = invoices.Count,
            Items = invoices.Select(si => new SalesRegisterLineDto
            {
                InvoiceId = si.Id,
                InvoiceNumber = si.InvoiceNumber ?? "",
                PostingDate = si.IssueDate,
                CustomerId = si.CustomerId,
                NetTotal = si.NetTotal,
                TaxAmount = si.TaxAmount,
                GrandTotal = si.GrandTotal,
                AmountPaid = si.AmountPaid,
                Outstanding = si.OutstandingAmount,
                IsReturn = si.IsReturn,
            }).ToList(),
            TotalNet = invoices.Sum(si => si.NetTotal),
            TotalTax = invoices.Sum(si => si.TaxAmount),
            TotalGrand = invoices.Sum(si => si.GrandTotal),
        };

        return result;
    }
}
