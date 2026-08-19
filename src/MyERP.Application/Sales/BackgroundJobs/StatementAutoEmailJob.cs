using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales.BackgroundJobs;

/// <summary>
/// Background job that automatically emails monthly statements of accounts to customers with outstanding balances.
/// Per ERPNext: process_statement_of_accounts.send_auto_email (daily/monthly scheduler).
/// </summary>
public class StatementAutoEmailJob : AsyncBackgroundJob<StatementAutoEmailJobArgs>, ITransientDependency
{
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly DocumentEmailService _emailService;
    private readonly ILogger<StatementAutoEmailJob> _logger;

    public StatementAutoEmailJob(
        IRepository<Customer, Guid> customerRepository,
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<Company, Guid> companyRepository,
        DocumentEmailService emailService,
        ILogger<StatementAutoEmailJob> logger)
    {
        _customerRepository = customerRepository;
        _invoiceRepository = invoiceRepository;
        _companyRepository = companyRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public override async Task ExecuteAsync(StatementAutoEmailJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("StatementAutoEmailJob: Processing automated customer statements for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var company = await _companyRepository.FindAsync(args.CompanyId);
        if (company == null) return;

        var invQuery = await _invoiceRepository.GetQueryableAsync();
        var openInvoices = invQuery
            .Where(i => i.CompanyId == args.CompanyId &&
                        i.Status == DocumentStatus.Posted &&
                        i.OutstandingAmount > 0)
            .ToList();

        var customerGroups = openInvoices.GroupBy(i => i.CustomerId).ToList();
        var sentCount = 0;

        foreach (var group in customerGroups)
        {
            var customer = await _customerRepository.FindAsync(group.Key);
            if (customer == null || string.IsNullOrWhiteSpace(customer.Email))
                continue;

            var totalOutstanding = group.Sum(i => i.OutstandingAmount);
            if (totalOutstanding <= 0)
                continue;

            try
            {
                var input = new SendDocumentEmailInput
                {
                    RecipientEmail = customer.Email,
                    AttachPdf = false,
                    Variables = new Dictionary<string, string>
                    {
                        ["customer_name"] = customer.Name,
                        ["company_name"] = company.Name,
                        ["total_outstanding"] = totalOutstanding.ToString("N2"),
                        ["invoice_count"] = group.Count().ToString(),
                        ["as_of_date"] = asOfDate.ToString("yyyy-MM-dd"),
                    }
                };

                await _emailService.SendSalesInvoiceEmailAsync(input);
                sentCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StatementAutoEmailJob: Failed to send statement email to customer {CustomerId} ({CustomerEmail})",
                    customer.Id, customer.Email);
            }
        }

        _logger.LogInformation("StatementAutoEmailJob: Sent {SentCount} customer statement emails for company {CompanyId}",
            sentCount, args.CompanyId);
    }
}

public class StatementAutoEmailJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
