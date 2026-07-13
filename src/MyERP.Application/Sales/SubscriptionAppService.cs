using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using MyERP.Permissions;
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

    public SubscriptionAppService(
        IRepository<Subscription, Guid> repository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository)
    {
        _repository = repository;
        _salesInvoiceRepository = salesInvoiceRepository;
    }

    public async Task<PagedResultDto<SubscriptionDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderByDescending(s => s.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<SubscriptionDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<SubscriptionDto> GetAsync(Guid id)
    {
        var sub = (await _repository.WithDetailsAsync()).First(s => s.Id == id);
        return MapToDto(sub);
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
        return MapToDto(sub);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Cancel)]
    public async Task<SubscriptionDto> CancelAsync(Guid id)
    {
        var sub = await _repository.GetAsync(id);
        sub.Cancel();
        await _repository.UpdateAsync(sub);
        return MapToDto(sub);
    }

    public async Task<SubscriptionDto> AdvancePeriodAsync(Guid id)
    {
        var sub = await _repository.GetAsync(id);
        sub.AdvancePeriod();
        await _repository.UpdateAsync(sub);
        return MapToDto(sub);
    }

    /// <summary>
    /// Generates a Sales Invoice for the current billing period.
    /// Advances the subscription period after creation.
    /// Per ERPNext: supports catch-up billing for missed periods.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<GeneratedInvoiceDto> GenerateInvoiceAsync(Guid id)
    {
        var sub = (await _repository.WithDetailsAsync()).First(s => s.Id == id);

        if (sub.Status != SubscriptionStatus.Active)
            throw new BusinessException(MyERPDomainErrorCodes.SubscriptionNotActive);

        if (!sub.Plans.Any())
            throw new BusinessException(MyERPDomainErrorCodes.SubscriptionHasNoPlans);

        // Determine if trial period (100% discount)
        var isInTrial = sub.TrialEndDate.HasValue && DateTime.UtcNow.Date <= sub.TrialEndDate.Value.Date;

        // Create invoice via SalesInvoiceAppService
        var invoiceItems = sub.Plans.Select(p => new Sales.CreateSalesInvoiceItemDto
        {
            ItemId = p.ItemId,
            Description = p.ItemName ?? "Subscription Item",
            Quantity = p.Qty,
            UnitPrice = isInTrial ? 0m : p.Rate,
        }).ToList();

        var invoiceNumber = $"SUB-{sub.SubscriptionNumber}-{sub.CurrentInvoiceStart:yyyyMMdd}";
        var invoice = new SalesInvoice(
            GuidGenerator.Create(), sub.CompanyId, sub.PartyId, invoiceNumber,
            sub.CurrentInvoiceStart ?? DateTime.UtcNow, CurrentTenant.Id);
        invoice.Notes = $"Subscription {sub.SubscriptionNumber} — " +
                        $"{sub.CurrentInvoiceStart:dd/MM/yyyy} to {sub.CurrentInvoiceEnd:dd/MM/yyyy}";

        foreach (var item in invoiceItems)
            invoice.AddItem(item.ItemId, item.Description,
                item.Quantity, item.UnitPrice, 0m);

        await _salesInvoiceRepository.InsertAsync(invoice);

        // Advance to next period
        sub.AdvancePeriod();

        // Check if subscription completed (past end date)
        if (sub.EndDate.HasValue && sub.CurrentInvoiceStart > sub.EndDate.Value)
            sub.Cancel();

        await _repository.UpdateAsync(sub);

        var previousStart = sub.CurrentInvoiceStart;
        var previousEnd = sub.CurrentInvoiceEnd;

        return new GeneratedInvoiceDto
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            GrandTotal = invoice.GrandTotal,
            PeriodStart = invoice.IssueDate,
            PeriodEnd = previousEnd,
        };
    }

    private static int GetIntervalMonths(Subscription sub) => sub.BillingInterval switch
    {
        "Monthly" => 1 * sub.BillingIntervalCount,
        "Quarterly" => 3 * sub.BillingIntervalCount,
        "Half-Yearly" => 6 * sub.BillingIntervalCount,
        "Yearly" => 12 * sub.BillingIntervalCount,
        _ => 1
    };

    private static SubscriptionDto MapToDto(Subscription s) => new()
    {
        Id = s.Id, CompanyId = s.CompanyId, PartyId = s.PartyId,
        PartyType = s.PartyType, PartyName = s.PartyName,
        SubscriptionNumber = s.SubscriptionNumber, BillingInterval = s.BillingInterval,
        BillingIntervalCount = s.BillingIntervalCount, StartDate = s.StartDate,
        EndDate = s.EndDate, CurrentInvoiceStart = s.CurrentInvoiceStart,
        CurrentInvoiceEnd = s.CurrentInvoiceEnd, TotalPerInterval = s.TotalPerInterval,
        Status = (int)s.Status,
        Plans = s.Plans.Select(p => new SubscriptionPlanDto
        {
            Id = p.Id, ItemId = p.ItemId, ItemName = p.ItemName, Qty = p.Qty, Rate = p.Rate,
        }).ToArray(),
    };
}
