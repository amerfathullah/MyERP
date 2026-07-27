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

        var today = DateTime.UtcNow.Date;
        int siOverpaid = 0, piOverpaid = 0;
        int siOverdue = 0, piOverdue = 0;
        int siErrors = 0, piErrors = 0;

        // --- Sales Invoices ---
        var siQuery = await _siRepository.GetQueryableAsync();
        var postedSalesInvoices = siQuery
            .Where(si => si.CompanyId == args.CompanyId
                      && si.Status == DocumentStatus.Posted
                      && si.GrandTotal > 0)
            .ToList();

        foreach (var si in postedSalesInvoices)
        {
            try
            {
                // Overdue detection (warning only — the job is a safety net)
                if (si.DueDate.HasValue && si.DueDate.Value < today && si.OutstandingAmount > 0)
                {
                    _logger.LogWarning(
                        "Overdue invoice {InvoiceNumber}: due {DueDate:yyyy-MM-dd}, outstanding {Outstanding:N2}",
                        si.InvoiceNumber, si.DueDate.Value, si.OutstandingAmount);
                    siOverdue++;
                }

                // Overpayment fix: cap AmountPaid to GrandTotal
                if (si.AmountPaid > si.GrandTotal)
                {
                    _logger.LogWarning(
                        "Overpaid SI {InvoiceNumber}: AmountPaid={Paid:N2} > GrandTotal={Total:N2}. Capping.",
                        si.InvoiceNumber, si.AmountPaid, si.GrandTotal);
                    si.AmountPaid = si.GrandTotal;
                    await _siRepository.UpdateAsync(si);
                    siOverpaid++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InvoiceStatusUpdateJob: Error processing SI {InvoiceNumber}", si.InvoiceNumber);
                siErrors++;
            }
        }

        // --- Purchase Invoices ---
        var piQuery = await _piRepository.GetQueryableAsync();
        var postedPurchaseInvoices = piQuery
            .Where(pi => pi.CompanyId == args.CompanyId
                      && pi.Status == DocumentStatus.Posted
                      && pi.GrandTotal > 0)
            .ToList();

        foreach (var pi in postedPurchaseInvoices)
        {
            try
            {
                if (pi.DueDate.HasValue && pi.DueDate.Value < today && pi.OutstandingAmount > 0)
                {
                    _logger.LogWarning(
                        "Overdue invoice {InvoiceNumber}: due {DueDate:yyyy-MM-dd}, outstanding {Outstanding:N2}",
                        pi.InvoiceNumber, pi.DueDate.Value, pi.OutstandingAmount);
                    piOverdue++;
                }

                if (pi.AmountPaid > pi.GrandTotal)
                {
                    _logger.LogWarning(
                        "Overpaid PI {InvoiceNumber}: AmountPaid={Paid:N2} > GrandTotal={Total:N2}. Capping.",
                        pi.InvoiceNumber, pi.AmountPaid, pi.GrandTotal);
                    pi.AmountPaid = pi.GrandTotal;
                    await _piRepository.UpdateAsync(pi);
                    piOverpaid++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InvoiceStatusUpdateJob: Error processing PI {InvoiceNumber}", pi.InvoiceNumber);
                piErrors++;
            }
        }

        // Summary
        if (siOverpaid > 0 || piOverpaid > 0)
        {
            _logger.LogWarning(
                "InvoiceStatusUpdateJob: Fixed overpayment on {SiCount} SI + {PiCount} PI. This indicates a bug in payment allocation.",
                siOverpaid, piOverpaid);
        }

        if (siOverdue > 0 || piOverdue > 0)
        {
            _logger.LogWarning(
                "InvoiceStatusUpdateJob: {SiOverdue} SI + {PiOverdue} PI overdue.",
                siOverdue, piOverdue);
        }

        if (siErrors > 0 || piErrors > 0)
        {
            _logger.LogError(
                "InvoiceStatusUpdateJob: {SiErrors} SI + {PiErrors} PI failed processing.",
                siErrors, piErrors);
        }

        if (siOverpaid == 0 && piOverpaid == 0 && siOverdue == 0 && piOverdue == 0 && siErrors == 0 && piErrors == 0)
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
