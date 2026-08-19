using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using MyERP.Accounting.Entities;
using MyERP.Accounting.DomainServices;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class DunningAppService : ApplicationService, IDunningAppService
{
    private readonly IRepository<Dunning, Guid> _repository;
    private readonly MyERP.Sales.DomainServices.DunningManager _dunningManager;

    public DunningAppService(
        IRepository<Dunning, Guid> repository,
        MyERP.Sales.DomainServices.DunningManager dunningManager)
    {
        _repository = repository;
        _dunningManager = dunningManager;
    }

    public async Task<PagedResultDto<DunningDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(x => x.CustomerName != null && x.CustomerName.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(d => d.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<DunningDto>(totalCount, items.Select(ObjectMapper.Map<Dunning, DunningDto>).ToList());
    }

    public async Task<DunningDto> GetAsync(Guid id)
    {
        var d = (await _repository.WithDetailsAsync()).First(x => x.Id == id);
        var dto = ObjectMapper.Map<Dunning, DunningDto>(d);

        // Resolve invoice numbers for overdue payment display
        if (d.OverduePayments.Any())
        {
            var invoiceIds = d.OverduePayments.Select(p => p.SalesInvoiceId).ToList();
            var invoiceRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>();
            var invoiceQuery = await invoiceRepo.GetQueryableAsync();
            var invoiceNumbers = invoiceQuery
                .Where(si => invoiceIds.Contains(si.Id))
                .Select(si => new { si.Id, si.InvoiceNumber })
                .ToDictionary(si => si.Id, si => si.InvoiceNumber);

            dto.OverduePayments = d.OverduePayments.Select(p => new DunningOverduePaymentDto
            {
                SalesInvoiceId = p.SalesInvoiceId,
                InvoiceNumber = invoiceNumbers.GetValueOrDefault(p.SalesInvoiceId),
                OutstandingAmount = p.OutstandingAmount,
                DueDate = p.DueDate,
                OverdueDays = p.OverdueDays,
            }).OrderByDescending(p => p.OverdueDays).ToList();
        }

        return dto;
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<DunningDto> CreateAsync(CreateDunningDto input)
    {
        // Determine correct dunning level from existing submitted dunnings
        var level = await _dunningManager.DetermineDunningLevelAsync(
            input.CustomerId, input.CompanyId, CurrentTenant.Id);

        // Default fee/interest rate from the Dunning Type when not explicitly overridden (per ERPNext fetch_from)
        var dunningFee = input.DunningFee;
        var interestRatePerAnnum = input.InterestRatePerAnnum;
        if (input.DunningTypeId.HasValue)
        {
            var typeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.DunningType, Guid>>();
            var dunningType = await typeRepo.FindAsync(input.DunningTypeId.Value);
            if (dunningType != null)
            {
                if (dunningFee == 0) dunningFee = dunningType.DunningFee;
                if (interestRatePerAnnum == 0) interestRatePerAnnum = dunningType.RateOfInterest;
            }
        }

        // Calculate interest if rate provided but amount not explicitly set
        var interestAmount = input.InterestAmount;
        if (interestAmount == 0 && interestRatePerAnnum > 0 && input.OverduePayments.Length > 0)
        {
            var overdueData = input.OverduePayments
                .Select(p => (p.OutstandingAmount, p.OverdueDays))
                .ToList();
            interestAmount = MyERP.Sales.DomainServices.DunningManager.CalculateInterest(
                interestRatePerAnnum, overdueData);
        }

        var d = new Dunning(GuidGenerator.Create(), input.CompanyId, input.CustomerId,
            input.PostingDate, level, CurrentTenant.Id)
        {
            CustomerName = input.CustomerName,
            DunningTypeId = input.DunningTypeId,
            DunningFee = dunningFee,
            InterestAmount = interestAmount,
        };
        foreach (var p in input.OverduePayments)
            d.AddOverduePayment(p.SalesInvoiceId, p.OutstandingAmount, p.DueDate, p.OverdueDays);
        await _repository.InsertAsync(d);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new DocumentActivityLog(GuidGenerator.Create(),
            "Dunning", d.Id, "Created", d.CompanyId,
            d.DunningLevel.ToString(), "Draft", "Draft",
            CurrentUser.Id,
            $"Dunning level {d.DunningLevel} created for customer {d.CustomerName}", d.TenantId));

        return ObjectMapper.Map<Dunning, DunningDto>(d);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<DunningDto> SubmitAsync(Guid id)
    {
        var d = (await _repository.WithDetailsAsync()).First(x => x.Id == id);

        // Validate level sequencing before submit
        await _dunningManager.ValidateLevelSequencingAsync(d.CustomerId, d.CompanyId, d.DunningLevel, d.TenantId);

        d.Submit();

        // Post GL entries for dunning fee + interest (DR Receivable, CR Income)
        if (d.GrandTotal > 0)
        {
            try
            {
                var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Company, Guid>>();
                var fyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<FiscalYear, Guid>>();
                var jeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<JournalEntry, Guid>>();
                var company = await companyRepo.GetAsync(d.CompanyId);
                var fy = (await fyRepo.GetQueryableAsync())
                    .Where(f => f.CompanyId == d.CompanyId && f.StartDate <= d.PostingDate && f.EndDate >= d.PostingDate)
                    .FirstOrDefault();

                // Prefer the Dunning Type's configured income account, falling back to the company default.
                Guid? incomeAccountId = company.DefaultIncomeAccountId;
                if (d.DunningTypeId.HasValue)
                {
                    var typeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.DunningType, Guid>>();
                    var dunningType = await typeRepo.FindAsync(d.DunningTypeId.Value);
                    if (dunningType?.IncomeAccountId != null)
                        incomeAccountId = dunningType.IncomeAccountId;
                }

                if (fy != null && company.DefaultReceivableAccountId.HasValue && incomeAccountId.HasValue)
                {
                    var je = new JournalEntry(GuidGenerator.Create(), d.CompanyId, fy.Id, d.PostingDate, d.TenantId);
                    je.ReferenceType = "Dunning";
                    je.ReferenceId = d.Id;
                    // DR Receivable (customer owes fee + interest)
                    je.AddLine(company.DefaultReceivableAccountId.Value, d.GrandTotal, true);
                    // CR Income (dunning fee + interest earned)
                    je.AddLine(incomeAccountId.Value, d.GrandTotal, false);
                    je.Post();
                    await jeRepo.InsertAsync(je);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to post GL entries for Dunning {DunningId}", d.Id);
            }
        }

        // Activity log
        try
        {
            var actRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DocumentActivityLog, Guid>>();
            await actRepo.InsertAsync(new DocumentActivityLog(GuidGenerator.Create(),
                "Dunning", d.Id, "Submitted", d.CompanyId,
                previousStatus: "Draft", newStatus: "Submitted",
                performedByUserId: CurrentUser.Id, tenantId: d.TenantId));
        }
        catch { }

        await _repository.UpdateAsync(d);
        return ObjectMapper.Map<Dunning, DunningDto>(d);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<DunningDto> ResolveAsync(Guid id)
    {
        var d = await _repository.GetAsync(id);
        d.Resolve();
        await _repository.UpdateAsync(d);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new DocumentActivityLog(GuidGenerator.Create(),
            "Dunning", d.Id, "Resolved", d.CompanyId,
            d.DunningLevel.ToString(), "Submitted", "Resolved",
            CurrentUser.Id,
            $"Dunning level {d.DunningLevel} resolved for customer {d.CustomerName}", d.TenantId));

        return ObjectMapper.Map<Dunning, DunningDto>(d);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Cancel)]
    public async Task<DunningDto> CancelAsync(Guid id)
    {
        var d = await _repository.GetAsync(id);
        d.Cancel();
        await _repository.UpdateAsync(d);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new DocumentActivityLog(GuidGenerator.Create(),
            "Dunning", d.Id, "Cancelled", d.CompanyId,
            d.DunningLevel.ToString(), "Submitted", "Cancelled",
            CurrentUser.Id,
            $"Dunning level {d.DunningLevel} cancelled for customer {d.CustomerName}", d.TenantId));

        return ObjectMapper.Map<Dunning, DunningDto>(d);
    }

    /// <summary>
    /// Send dunning notice email to customer with overdue invoice details.
    /// Per ERPNext dunning.py: get_dunning_letter_text renders per-language template.
    /// </summary>
    /// <summary>
    /// Auto-generate dunning documents for all customers with overdue invoices.
    /// Per ERPNext: scans submitted SIs where due_date < today and outstanding > 0,
    /// groups by customer, determines correct dunning level, calculates interest,
    /// creates one Dunning per customer with all their overdue invoices.
    /// Skips customers that already have a Draft dunning awaiting submission.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<AutoGenerateDunningsResultDto> AutoGenerateAsync(AutoGenerateDunningsDto input)
    {
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));

        var today = input.AsOfDate ?? DateTime.UtcNow.Date;
        var result = new AutoGenerateDunningsResultDto();

        // Get all submitted, non-return invoices with outstanding > 0 and due date past
        var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>();
        var siQuery = await siRepo.GetQueryableAsync();
        var overdueInvoices = siQuery
            .Where(si => si.CompanyId == input.CompanyId
                      && si.Status == DocumentStatus.Submitted
                      && !si.IsReturn
                      && si.DueDate.HasValue
                      && si.DueDate.Value < today
                      && (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0.01m)
            .Select(si => new
            {
                si.Id,
                si.CustomerId,
                DueDate = si.DueDate!.Value,
                Outstanding = si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance
            })
            .ToList();

        if (!overdueInvoices.Any())
        {
            return result;
        }

        // Group by customer
        var grouped = overdueInvoices.GroupBy(i => i.CustomerId).ToList();

        // Find customers that already have a draft dunning (skip those)
        var existingDraftQuery = await _repository.GetQueryableAsync();
        var customersWithDraftDunning = existingDraftQuery
            .Where(d => d.CompanyId == input.CompanyId && d.Status == DocumentStatus.Draft)
            .Select(d => d.CustomerId)
            .Distinct()
            .ToHashSet();

        foreach (var customerGroup in grouped)
        {
            var customerId = customerGroup.Key;

            // Skip if customer already has a pending draft dunning
            if (customersWithDraftDunning.Contains(customerId))
            {
                result.SkippedCount++;
                continue;
            }

            try
            {
                var level = await _dunningManager.DetermineDunningLevelAsync(
                    customerId, input.CompanyId, CurrentTenant.Id);

                // Calculate interest if rate provided
                var overdueData = customerGroup
                    .Select(i => (i.Outstanding, (int)(today - i.DueDate).TotalDays))
                    .ToList();
                var interestAmount = input.InterestRatePerAnnum > 0
                    ? Sales.DomainServices.DunningManager.CalculateInterest(input.InterestRatePerAnnum, overdueData.Select(d => (d.Outstanding, d.Item2)).ToList())
                    : 0m;

                var dunning = new Dunning(GuidGenerator.Create(), input.CompanyId, customerId,
                    today, level, CurrentTenant.Id)
                {
                    DunningFee = input.DunningFeePerCustomer,
                    InterestAmount = Math.Round(interestAmount, 2),
                };

                foreach (var inv in customerGroup)
                {
                    var overdueDays = (int)(today - inv.DueDate).TotalDays;
                    dunning.AddOverduePayment(inv.Id, inv.Outstanding, inv.DueDate, overdueDays);
                }

                await _repository.InsertAsync(dunning);
                result.CreatedCount++;
                result.TotalOverdueAmount += dunning.TotalOutstanding;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to auto-generate dunning for customer {CustomerId}", customerId);
                result.FailedCount++;
            }
        }

        result.CustomersScanned = grouped.Count;
        return result;
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task SendDunningEmailAsync(Guid id, SendDunningEmailDto input)
    {
        var d = (await _repository.WithDetailsAsync()).First(x => x.Id == id);
        if (d.Status != DocumentStatus.Submitted)
            throw new BusinessException("MyERP:01002")
                .WithData("reason", "Dunning must be submitted before sending email");

        var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Customer, Guid>>();
        var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Company, Guid>>();
        var customer = await customerRepo.GetAsync(d.CustomerId);
        var company = await companyRepo.GetAsync(d.CompanyId);

        var recipientEmail = input.RecipientEmail ?? customer.Email;
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new BusinessException("MyERP:09001")
                .WithData("reason", "No email address for customer. Provide a recipient email.");

        var invoiceDetails = d.OverduePayments
            .OrderByDescending(p => p.OverdueDays)
            .Select(p => $"  • Outstanding {p.OutstandingAmount:N2} (overdue {p.OverdueDays} days)")
            .ToList();

        var subject = $"Payment Reminder - Level {d.DunningLevel} - {company.Name}";
        var body = $"""
            Dear {customer.Name},

            This is a reminder that the following invoices are overdue:

            {string.Join("\n", invoiceDetails)}

            Total Outstanding: {d.TotalOutstanding:N2}
            Dunning Fee: {d.DunningFee:N2}
            Interest: {d.InterestAmount:N2}
            Grand Total Due: {d.GrandTotal:N2}

            Please arrange payment at your earliest convenience.

            Regards,
            {company.Name}
            """;

        var emailService = LazyServiceProvider.LazyGetRequiredService<DocumentEmailService>();
        var ccEmails = string.IsNullOrWhiteSpace(input.Cc)
            ? null
            : input.Cc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        await emailService.SendCustomEmailAsync(recipientEmail, subject, body, ccEmails: ccEmails);

        d.MarkEmailSent(recipientEmail);
        await _repository.UpdateAsync(d);
    }
}

