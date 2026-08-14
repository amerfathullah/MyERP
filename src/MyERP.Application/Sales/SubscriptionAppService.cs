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

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class SubscriptionAppService : ApplicationService, ISubscriptionAppService
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
        {
            var costCenterId = await ResolvePlanCostCenterAsync(p.ItemId, input.CompanyId, input.PartyType);
            sub.AddPlan(p.ItemId, p.Qty, p.Rate, p.ItemName, costCenterId);
        }

        // Per PR #57615: fill subscription-level cost center from first plan with a CC
        if (!sub.CostCenterId.HasValue)
        {
            var firstCc = sub.Plans.FirstOrDefault(pl => pl.CostCenterId.HasValue)?.CostCenterId;
            if (firstCc.HasValue) sub.CostCenterId = firstCc;
        }

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
        invoice.CostCenterId = sub.CostCenterId;
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

    /// <summary>
    /// Generates catch-up invoices for all elapsed billing periods.
    /// Per ERPNext Subscription 7-state lifecycle: on after_insert, generates invoices for ALL
    /// elapsed periods via while-loop (with 3 breaks: end date, max periods, current date).
    /// Called by background job or manual trigger when subscription has gaps.
    /// Per DO-NOT: "Implement subscription without catch-up invoice generation for past periods"
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<List<GeneratedInvoiceDto>> GenerateCatchUpInvoicesAsync(Guid id)
    {
        var sub = (await _repository.WithDetailsAsync()).First(s => s.Id == id);

        if (sub.Status != SubscriptionStatus.Active)
            throw new BusinessException(MyERPDomainErrorCodes.SubscriptionNotActive);

        if (!sub.Plans.Any())
            throw new BusinessException(MyERPDomainErrorCodes.SubscriptionHasNoPlans);

        var results = new List<GeneratedInvoiceDto>();
        var today = DateTime.UtcNow.Date;
        var maxCatchUp = 24; // Safety: max 24 periods to prevent runaway

        // Generate invoices while current period end is in the past
        while (sub.CurrentInvoiceEnd.HasValue && sub.CurrentInvoiceEnd.Value < today
            && sub.Status == SubscriptionStatus.Active && results.Count < maxCatchUp)
        {
            // Check end date boundary
            if (sub.EndDate.HasValue && sub.CurrentInvoiceStart.HasValue
                && sub.CurrentInvoiceStart.Value > sub.EndDate.Value)
                break;

            var items = _billingEngine.BuildInvoiceItems(sub, sub.CurrentInvoiceStart ?? today);
            if (!items.Any()) break;

            var invoiceRef = _billingEngine.GenerateInvoiceReference(sub);
            var invoice = new SalesInvoice(
                GuidGenerator.Create(), sub.CompanyId, sub.PartyId, invoiceRef,
                sub.CurrentInvoiceStart ?? today, CurrentTenant.Id);
            invoice.CostCenterId = sub.CostCenterId;
            invoice.Notes = $"Subscription {sub.SubscriptionNumber} (catch-up) — " +
                            $"{sub.CurrentInvoiceStart:dd/MM/yyyy} to {sub.CurrentInvoiceEnd:dd/MM/yyyy}";

            foreach (var item in items)
                invoice.AddItem(item.ItemId, item.ItemName ?? "Subscription Item",
                    item.Qty, item.Rate, 0m);

            await _salesInvoiceRepository.InsertAsync(invoice);

            results.Add(new GeneratedInvoiceDto
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                GrandTotal = invoice.GrandTotal,
                PeriodStart = sub.CurrentInvoiceStart,
                PeriodEnd = sub.CurrentInvoiceEnd,
            });

            // Advance to next period
            _billingEngine.AdvancePeriodAndCheckCompletion(sub);
        }

        if (results.Any())
            await _repository.UpdateAsync(sub);

        return results;
    }

    /// <summary>
    /// Resolves a plan's accounting dimensions (cost center) with fallback to item defaults.
    /// Per ERPNext PR #57615: plan → item defaults (selling CC for Customer, buying CC for Supplier).
    /// </summary>
    public async Task<PlanDimensionsDto> GetPlanDimensionsAsync(Guid itemId, Guid companyId, string? partyType = null)
    {
        var costCenterId = await ResolvePlanCostCenterAsync(itemId, companyId, partyType);
        return new PlanDimensionsDto { CostCenterId = costCenterId };
    }

    private async Task<Guid?> ResolvePlanCostCenterAsync(Guid itemId, Guid companyId, string? partyType)
    {
        var itemDefaultRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.ItemDefault, Guid>>();
        var itemDefault = (await itemDefaultRepo.GetQueryableAsync())
            .FirstOrDefault(d => d.ItemId == itemId && d.CompanyId == companyId);

        if (itemDefault == null) return null;

        // Per PR #57615: Supplier uses buying cost center, Customer uses selling; fallback to other
        if (partyType == "Supplier")
            return itemDefault.BuyingCostCenterId ?? itemDefault.SellingCostCenterId;

        return itemDefault.SellingCostCenterId ?? itemDefault.BuyingCostCenterId;
    }
}

