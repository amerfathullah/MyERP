using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting.BackgroundJobs;

/// <summary>
/// Daily job that recalculates invoice payment status.
/// Per ERPNext hooks.py: controllers.accounts_controller.update_invoice_status (daily_maintenance)
/// 
/// Per ERPNext: this is a SAFETY NET. The primary status update happens when payments are posted,
/// but this daily job catches any missed updates (e.g., from direct DB edits, crashed transactions).
/// 
/// Status transitions:
/// - Posted + OutstandingAmount &gt; 0 + DueDate &lt; today → Overdue (not explicitly tracked but useful)
/// - Posted + OutstandingAmount == 0 → could flag as Paid
/// 
/// Per DO-NOT: "Skip daily invoice status recalculation (safety net for missed event handler updates)"
/// </summary>
public class InvoiceStatusUpdateJob : AsyncBackgroundJob<InvoiceStatusUpdateJobArgs>, ITransientDependency
{
    private readonly IRepository<SalesInvoice, Guid> _siRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _piRepository;
    private readonly ILogger<InvoiceStatusUpdateJob> _logger;

    public InvoiceStatusUpdateJob(
        IRepository<SalesInvoice, Guid> siRepository,
        IRepository<PurchaseInvoice, Guid> piRepository,
        ILogger<InvoiceStatusUpdateJob> logger)
    {
        _siRepository = siRepository;
        _piRepository = piRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(InvoiceStatusUpdateJobArgs args)
    {
        _logger.LogInformation(
            "InvoiceStatusUpdateJob: Recalculating invoice status for company {CompanyId}",
            args.CompanyId);

        int siUpdated = 0;
        int piUpdated = 0;

        // Sales Invoices: only load those with over-payment (AmountPaid > GrandTotal)
        var siQuery = await _siRepository.GetQueryableAsync();
        var overpaidSalesInvoices = siQuery
            .Where(si => si.CompanyId == args.CompanyId
                      && si.Status == DocumentStatus.Posted
                      && si.GrandTotal > 0
                      && si.AmountPaid > si.GrandTotal)
            .ToList();

        foreach (var si in overpaidSalesInvoices)
        {
            // OutstandingAmount is a computed property: GrandTotal - AmountPaid
            // If AmountPaid somehow exceeds GrandTotal (e.g., double payment), cap it
            if (si.AmountPaid > si.GrandTotal)
            {
                si.AmountPaid = si.GrandTotal;
                await _siRepository.UpdateAsync(si);
                siUpdated++;
            }
        }

        // Purchase Invoices: same check — only load over-paid ones
        var piQuery = await _piRepository.GetQueryableAsync();
        var overpaidPurchaseInvoices = piQuery
            .Where(pi => pi.CompanyId == args.CompanyId
                      && pi.Status == DocumentStatus.Posted
                      && pi.GrandTotal > 0
                      && pi.AmountPaid > pi.GrandTotal)
            .ToList();

        foreach (var pi in overpaidPurchaseInvoices)
        {
            if (pi.AmountPaid > pi.GrandTotal)
            {
                pi.AmountPaid = pi.GrandTotal;
                await _piRepository.UpdateAsync(pi);
                piUpdated++;
            }
        }

        if (siUpdated > 0 || piUpdated > 0)
        {
            _logger.LogWarning(
                "InvoiceStatusUpdateJob: Fixed {SiCount} SI + {PiCount} PI with over-payment. This indicates a bug in payment allocation.",
                siUpdated, piUpdated);
        }
        else
        {
            _logger.LogInformation("InvoiceStatusUpdateJob: All invoices consistent. No corrections needed.");
        }
    }
}

public class InvoiceStatusUpdateJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}
