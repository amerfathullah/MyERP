using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

public class SubscriptionDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = null!;
    public string? PartyName { get; set; }
    public string? SubscriptionNumber { get; set; }
    public string BillingInterval { get; set; } = null!;
    public int BillingIntervalCount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? CurrentInvoiceStart { get; set; }
    public DateTime? CurrentInvoiceEnd { get; set; }
    public decimal TotalPerInterval { get; set; }
    public int Status { get; set; }
    public SubscriptionPlanDto[] Plans { get; set; } = [];
}

public class SubscriptionPlanDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}

public class CreateSubscriptionDto
{
    public Guid CompanyId { get; set; }
    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = "Customer";
    public string? PartyName { get; set; }
    public string BillingInterval { get; set; } = "Monthly";
    public int BillingIntervalCount { get; set; } = 1;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int TrialPeriodDays { get; set; }
    public CreateSubscriptionPlanDto[] Plans { get; set; } = [];
}

public class CreateSubscriptionPlanDto
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}

public class GeneratedInvoiceDto
{
    public Guid InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
}

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class SubscriptionAppService : ApplicationService
{
    private readonly IRepository<Subscription, Guid> _repository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly SubscriptionBillingEngine _billingEngine;

    public SubscriptionAppService(
        IRepository<Subscription, Guid> repository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        SubscriptionBillingEngine billingEngine)
    {
        _repository = repository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _billingEngine = billingEngine;
    }

    public async Task<PagedResultDto<SubscriptionDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(x => x.SubscriptionNumber != null && x.SubscriptionNumber.Contains(f));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<SubscriptionStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(s => s.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<SubscriptionDto>(totalCount, items.Select(x => ObjectMapper.Map<Subscription, SubscriptionDto>(x)).ToList());
    }

    public async Task<SubscriptionDto> GetAsync(Guid id)
    {
        var sub = (await _repository.WithDetailsAsync()).First(s => s.Id == id);
        return ObjectMapper.Map<Subscription, SubscriptionDto>(sub);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<SubscriptionDto> CreateAsync(CreateSubscriptionDto input)
    {
        var sub = new Subscription(GuidGenerator.Create(), input.CompanyId, input.PartyId,
            input.PartyType, input.StartDate, input.BillingInterval, CurrentTenant.Id)
        {
            PartyName = input.PartyName,
            BillingIntervalCount = input.BillingIntervalCount,
            EndDate = input.EndDate,
            TrialPeriodDays = input.TrialPeriodDays,
        };
        foreach (var p in input.Plans)
            sub.AddPlan(p.ItemId, p.Qty, p.Rate, p.ItemName);
        sub.AdvancePeriod(); // Set initial billing period
        await _repository.InsertAsync(sub);
        return ObjectMapper.Map<Subscription, SubscriptionDto>(sub);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Cancel)]
    public async Task<SubscriptionDto> CancelAsync(Guid id)
    {
        var sub = await _repository.GetAsync(id);
        sub.Cancel();
        await _repository.UpdateAsync(sub);
        return ObjectMapper.Map<Subscription, SubscriptionDto>(sub);
    }

    public async Task<SubscriptionDto> AdvancePeriodAsync(Guid id)
    {
        var sub = await _repository.GetAsync(id);
        sub.AdvancePeriod();
        await _repository.UpdateAsync(sub);
        return ObjectMapper.Map<Subscription, SubscriptionDto>(sub);
    }

    /// <summary>
    /// Generates a Sales Invoice for the current billing period.
    /// Delegates to SubscriptionBillingEngine for trial/proration/items logic.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<GeneratedInvoiceDto> GenerateInvoiceAsync(Guid id)
    {
        var sub = (await _repository.WithDetailsAsync()).First(s => s.Id == id);

        if (sub.Status != SubscriptionStatus.Active)
            throw new BusinessException(MyERPDomainErrorCodes.SubscriptionNotActive);

        if (!sub.Plans.Any())
            throw new BusinessException(MyERPDomainErrorCodes.SubscriptionHasNoPlans);

        // Delegate to engine for items (handles trial period + proration)
        var items = _billingEngine.BuildInvoiceItems(sub, DateTime.UtcNow.Date);

        // Generate invoice reference via engine
        var invoiceRef = _billingEngine.GenerateInvoiceReference(sub);

        var invoice = new SalesInvoice(
            GuidGenerator.Create(), sub.CompanyId, sub.PartyId, invoiceRef,
            sub.CurrentInvoiceStart ?? DateTime.UtcNow, CurrentTenant.Id);
        invoice.Notes = $"Subscription {sub.SubscriptionNumber} — " +
                        $"{sub.CurrentInvoiceStart:dd/MM/yyyy} to {sub.CurrentInvoiceEnd:dd/MM/yyyy}";

        foreach (var item in items)
            invoice.AddItem(item.ItemId, item.ItemName ?? "Subscription Item",
                item.Qty, item.Rate, 0m);

        await _salesInvoiceRepository.InsertAsync(invoice);

        // Advance period and check completion via engine
        _billingEngine.AdvancePeriodAndCheckCompletion(sub);
        await _repository.UpdateAsync(sub);

        return new GeneratedInvoiceDto
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            GrandTotal = invoice.GrandTotal,
            PeriodStart = invoice.IssueDate,
            PeriodEnd = sub.CurrentInvoiceEnd,
        };
    }
}

