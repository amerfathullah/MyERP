using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseInvoices.Default)]
public class PurchaseRegisterAppService : ApplicationService, IPurchaseRegisterAppService
{
    private readonly IRepository<PurchaseInvoice, Guid> _invoiceRepository;

    public PurchaseRegisterAppService(IRepository<PurchaseInvoice, Guid> invoiceRepository)
        => _invoiceRepository = invoiceRepository;

    public async Task<RegisterReportDto<PurchaseRegisterLineDto>> GetReportAsync(RegisterFilterDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;

        var query = await _invoiceRepository.GetQueryableAsync();
        var invoices = query
            .Where(pi => pi.CompanyId == input.CompanyId
                      && pi.Status == DocumentStatus.Posted
                      && pi.IssueDate >= from
                      && pi.IssueDate <= to)
            .OrderByDescending(pi => pi.IssueDate)
            .ToList();

        return new RegisterReportDto<PurchaseRegisterLineDto>
        {
            Count = invoices.Count,
            Items = invoices.Select(pi => new PurchaseRegisterLineDto
            {
                InvoiceId = pi.Id,
                InvoiceNumber = pi.InvoiceNumber ?? "",
                PostingDate = pi.IssueDate,
                SupplierId = pi.SupplierId,
                NetTotal = pi.NetTotal,
                TaxAmount = pi.TaxAmount,
                GrandTotal = pi.GrandTotal,
                AmountPaid = pi.AmountPaid,
                Outstanding = pi.OutstandingAmount,
                IsReturn = pi.IsReturn,
            }).ToList(),
            TotalNet = invoices.Sum(pi => pi.NetTotal),
            TotalTax = invoices.Sum(pi => pi.TaxAmount),
            TotalGrand = invoices.Sum(pi => pi.GrandTotal),
        };
    }
}
