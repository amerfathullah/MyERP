using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Sales.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Settings.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize]
public class PartyDashboardAppService : MyERPAppService, IPartyDashboardAppService
{
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly LoyaltyPointService _loyaltyPointService;

    public PartyDashboardAppService(
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<Customer, Guid> customerRepository,
        LoyaltyPointService loyaltyPointService)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _companyRepository = companyRepository;
        _customerRepository = customerRepository;
        _loyaltyPointService = loyaltyPointService;
    }

    public async Task<PartyDashboardDto> GetCustomerDashboardAsync(Guid customerId)
    {
        // 1. Get relevant sales invoices for the customer
        // The repository automatically applies CompanyRestrictionEventHandler (via ABP IDataFilter)
        // so the user only sees invoices for companies they have access to.
        var query = await _salesInvoiceRepository.GetQueryableAsync();
        var invoices = await AsyncExecuter.ToListAsync(
            query.Where(x => x.CustomerId == customerId && x.Status == DocumentStatus.Submitted)
        );

        var thisYear = DateTime.Today.Year;
        var ytdBilling = invoices.Where(x => x.IssueDate.Year == thisYear).Sum(x => x.BaseGrandTotal);
        var totalUnpaid = invoices.Sum(x => x.OutstandingAmount);

        // Resolve permitted companies this customer has transactions with
        var companyIds = invoices.Select(x => x.CompanyId).Distinct().ToList();
        var companies = await _companyRepository.GetListAsync(x => companyIds.Contains(x.Id));

        var customer = await _customerRepository.FindAsync(customerId);
        var loyaltyPoints = customer?.LoyaltyProgramId.HasValue == true
            ? await _loyaltyPointService.GetAvailablePointsAsync(customerId, customer.LoyaltyProgramId!.Value, DateTime.Today)
            : 0;

        return new PartyDashboardDto
        {
            YtdBilling = ytdBilling,
            TotalUnpaid = totalUnpaid,
            LoyaltyPoints = loyaltyPoints,
            Companies = companies.Select(c => new CompanyReferenceDto { Id = c.Id, Name = c.Name }).ToList()
        };
    }

    public async Task<PartyDashboardDto> GetSupplierDashboardAsync(Guid supplierId)
    {
        var query = await _purchaseInvoiceRepository.GetQueryableAsync();
        var invoices = await AsyncExecuter.ToListAsync(
            query.Where(x => x.SupplierId == supplierId && x.Status == DocumentStatus.Submitted)
        );

        var thisYear = DateTime.Today.Year;
        var ytdBilling = invoices.Where(x => x.IssueDate.Year == thisYear).Sum(x => x.BaseGrandTotal);
        var totalUnpaid = invoices.Sum(x => x.OutstandingAmount);

        var companyIds = invoices.Select(x => x.CompanyId).Distinct().ToList();
        var companies = await _companyRepository.GetListAsync(x => companyIds.Contains(x.Id));

        return new PartyDashboardDto
        {
            YtdBilling = ytdBilling,
            TotalUnpaid = totalUnpaid,
            LoyaltyPoints = 0, // Suppliers don't participate in the Loyalty Program (customer-only feature)
            Companies = companies.Select(c => new CompanyReferenceDto { Id = c.Id, Name = c.Name }).ToList()
        };
    }
}
