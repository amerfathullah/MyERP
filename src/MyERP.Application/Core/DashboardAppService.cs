using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Inventory.Entities;
using MyERP.EInvoice.Entities;
using MyERP.Workflow;
using MyERP.Workflow.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

[Authorize]
public class DashboardAppService : ApplicationService
{
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo;
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepo;
    private readonly IRepository<EInvoiceSubmission, Guid> _eInvoiceRepo;
    private readonly IRepository<ApprovalRequest, Guid> _approvalRepo;

    public DashboardAppService(
        IRepository<Customer, Guid> customerRepo,
        IRepository<Supplier, Guid> supplierRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<SalesInvoice, Guid> salesInvoiceRepo,
        IRepository<PurchaseOrder, Guid> purchaseOrderRepo,
        IRepository<EInvoiceSubmission, Guid> eInvoiceRepo,
        IRepository<ApprovalRequest, Guid> approvalRepo)
    {
        _customerRepo = customerRepo;
        _supplierRepo = supplierRepo;
        _itemRepo = itemRepo;
        _salesInvoiceRepo = salesInvoiceRepo;
        _purchaseOrderRepo = purchaseOrderRepo;
        _eInvoiceRepo = eInvoiceRepo;
        _approvalRepo = approvalRepo;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        return new DashboardSummaryDto
        {
            TotalCustomers = (int)await _customerRepo.GetCountAsync(),
            TotalSuppliers = (int)await _supplierRepo.GetCountAsync(),
            TotalItems = (int)await _itemRepo.GetCountAsync(),
            DraftInvoices = (int)await _salesInvoiceRepo.CountAsync(i => i.Status == DocumentStatus.Draft),
            OutstandingInvoices = (int)await _salesInvoiceRepo.CountAsync(i => i.Status == DocumentStatus.Posted && i.AmountPaid < i.GrandTotal),
            PendingPurchaseOrders = (int)await _purchaseOrderRepo.CountAsync(po => po.Status == DocumentStatus.Submitted),
            SubmittedEInvoices = (int)await _eInvoiceRepo.GetCountAsync(),
            PendingApprovals = (int)await _approvalRepo.CountAsync(a => a.Status == ApprovalStatus.Pending),
            MonthlyRevenue = (await _salesInvoiceRepo.GetListAsync(i => i.Status == DocumentStatus.Posted && i.IssueDate >= monthStart))
                .Sum(i => i.GrandTotal),
            MonthlyExpenses = 0, // TODO: sum from purchase invoices
        };
    }
}
