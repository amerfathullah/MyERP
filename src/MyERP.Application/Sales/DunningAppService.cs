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

public class DunningDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public DateTime PostingDate { get; set; }
    public int DunningLevel { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal DunningFee { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public int Status { get; set; }
    public int OverduePaymentCount { get; set; }
    public DateTime? EmailSentAt { get; set; }
    public string? EmailSentTo { get; set; }

    /// <summary>Detailed overdue invoice entries (per ERPNext dunning_overdue_payment child table).</summary>
    public List<DunningOverduePaymentDto> OverduePayments { get; set; } = new();
}

/// <summary>Per-invoice detail in a Dunning (shows which invoices are overdue and by how much).</summary>
public class DunningOverduePaymentDto
{
    public Guid SalesInvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal OutstandingAmount { get; set; }
    public DateTime DueDate { get; set; }
    public int OverdueDays { get; set; }
}

public class CreateDunningDto
{
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal DunningFee { get; set; }
    public decimal InterestAmount { get; set; }
    /// <summary>Per-annum interest rate. If set and InterestAmount is 0, interest is auto-calculated.</summary>
    public decimal InterestRatePerAnnum { get; set; }
    public CreateDunningOverdueDto[] OverduePayments { get; set; } = [];
}

public class CreateDunningOverdueDto
{
    public Guid SalesInvoiceId { get; set; }
    public decimal OutstandingAmount { get; set; }
    public DateTime DueDate { get; set; }
    public int OverdueDays { get; set; }
}

public class SendDunningEmailDto
{
    public string? RecipientEmail { get; set; }
    public string? Cc { get; set; }
}

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class DunningAppService : ApplicationService
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

        // Calculate interest if rate provided but amount not explicitly set
        var interestAmount = input.InterestAmount;
        if (interestAmount == 0 && input.InterestRatePerAnnum > 0 && input.OverduePayments.Length > 0)
        {
            var overdueData = input.OverduePayments
                .Select(p => (p.OutstandingAmount, p.OverdueDays))
                .ToList();
            interestAmount = MyERP.Sales.DomainServices.DunningManager.CalculateInterest(
                input.InterestRatePerAnnum, overdueData);
        }

        var d = new Dunning(GuidGenerator.Create(), input.CompanyId, input.CustomerId,
            input.PostingDate, level, CurrentTenant.Id)
        { CustomerName = input.CustomerName, DunningFee = input.DunningFee, InterestAmount = interestAmount };
        foreach (var p in input.OverduePayments)
            d.AddOverduePayment(p.SalesInvoiceId, p.OutstandingAmount, p.DueDate, p.OverdueDays);
        await _repository.InsertAsync(d);
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

                if (fy != null && company.DefaultReceivableAccountId.HasValue && company.DefaultIncomeAccountId.HasValue)
                {
                    var je = new JournalEntry(GuidGenerator.Create(), d.CompanyId, fy.Id, d.PostingDate, d.TenantId);
                    je.ReferenceType = "Dunning";
                    je.ReferenceId = d.Id;
                    // DR Receivable (customer owes fee + interest)
                    je.AddLine(company.DefaultReceivableAccountId.Value, d.GrandTotal, true);
                    // CR Income (dunning fee + interest earned)
                    je.AddLine(company.DefaultIncomeAccountId.Value, d.GrandTotal, false);
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
        return ObjectMapper.Map<Dunning, DunningDto>(d);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Cancel)]
    public async Task<DunningDto> CancelAsync(Guid id)
    {
        var d = await _repository.GetAsync(id);
        d.Cancel();
        await _repository.UpdateAsync(d);
        return ObjectMapper.Map<Dunning, DunningDto>(d);
    }

    /// <summary>
    /// Send dunning notice email to customer with overdue invoice details.
    /// Per ERPNext dunning.py: get_dunning_letter_text renders per-language template.
    /// </summary>
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

