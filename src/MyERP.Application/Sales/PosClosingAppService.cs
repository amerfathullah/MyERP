using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// POS Closing Entry AppService — manages POS shift closure and reconciliation.
/// Per ERPNext: closing aggregates POS invoices per cashier shift, calculates variance,
/// and triggers consolidation into a single Sales Invoice for GL posting.
/// </summary>
[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class PosClosingAppService : ApplicationService
{
    private readonly IRepository<PosClosingEntry, Guid> _repository;
    private readonly PosConsolidationService _consolidationService;
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;

    public PosClosingAppService(
        IRepository<PosClosingEntry, Guid> repository,
        PosConsolidationService consolidationService,
        IRepository<SalesInvoice, Guid> invoiceRepository)
    {
        _repository = repository;
        _consolidationService = consolidationService;
        _invoiceRepository = invoiceRepository;
    }

    public async Task<PosClosingDto> GetAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        return ObjectMapper.Map<PosClosingEntry, PosClosingDto>(entry);
    }

    public async Task<PagedResultDto<PosClosingDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<PosClosingStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var count = query.Count();
        var list = query.OrderByDescending(x => x.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<PosClosingDto>(count, list.Select(x => ObjectMapper.Map<PosClosingEntry, PosClosingDto>(x)).ToList());
    }

    /// <summary>
    /// Creates a POS Closing Entry for the current shift.
    /// Links POS invoices and payment mode balances.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<PosClosingDto> CreateAsync(CreatePosClosingDto input)
    {
        var entry = new PosClosingEntry(
            GuidGenerator.Create(), input.CompanyId, input.PosProfileId,
            input.PosOpeningEntryId, input.UserId, CurrentTenant.Id);

        foreach (var inv in input.Invoices)
            entry.AddInvoice(inv.PosInvoiceId, inv.InvoiceNumber, inv.GrandTotal);

        foreach (var pay in input.Payments)
            entry.AddPayment(pay.ModeOfPaymentId, pay.ModeName, pay.ExpectedAmount, pay.ClosingAmount);

        await _repository.InsertAsync(entry, autoSave: true);
        return ObjectMapper.Map<PosClosingEntry, PosClosingDto>(entry);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<PosClosingDto> SubmitAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Submit();

        // POS Consolidation: merge individual POS invoices into consolidated SI for GL posting
        // Per ERPNext pos_invoice_merge_log: creates single SI per dimension group
        if (entry.Invoices.Any())
        {
            var invoiceIds = entry.Invoices.Select(i => i.PosInvoiceId).ToList();

            // Use first invoice's customer as the consolidation customer
            var firstInvoice = await _invoiceRepository.FindAsync(entry.Invoices.First().PosInvoiceId);
            var customerId = firstInvoice?.CustomerId ?? Guid.Empty;

            var results = await _consolidationService.ConsolidateAsync(
                invoiceIds, entry.CompanyId, customerId, entry.PostingDate);

            // Create the consolidated Sales Invoice from the first result
            if (results.Any())
            {
                var primary = results.First();
                var consolidatedSi = new SalesInvoice(
                    GuidGenerator.Create(), primary.CompanyId, primary.CustomerId,
                    "MYR", primary.PostingDate, CurrentTenant.Id);

                foreach (var item in primary.Items)
                {
                    consolidatedSi.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, 0m);
                }

                consolidatedSi.Submit();
                consolidatedSi.Post();
                await _invoiceRepository.InsertAsync(consolidatedSi, autoSave: true);

                entry.ConsolidatedSalesInvoiceId = consolidatedSi.Id;
            }
        }

        await _repository.UpdateAsync(entry, autoSave: true);
        return ObjectMapper.Map<PosClosingEntry, PosClosingDto>(entry);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<PosClosingDto> CancelAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Cancel();
        await _repository.UpdateAsync(entry, autoSave: true);
        return ObjectMapper.Map<PosClosingEntry, PosClosingDto>(entry);
    }
}

public class PosClosingDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PosProfileId { get; set; }
    public DateTime PostingDate { get; set; }
    public string Status { get; set; } = null!;
    public decimal GrandTotal { get; set; }
    public decimal NetTotal { get; set; }
    public decimal TotalDifference { get; set; }
    public Guid? ConsolidatedSalesInvoiceId { get; set; }
    public List<PosClosingPaymentDto> Payments { get; set; } = new();
    public List<PosClosingInvoiceDto> Invoices { get; set; } = new();
}

public class PosClosingPaymentDto
{
    public string ModeName { get; set; } = null!;
    public decimal ExpectedAmount { get; set; }
    public decimal ClosingAmount { get; set; }
    public decimal Difference { get; set; }
}

public class PosClosingInvoiceDto
{
    public Guid PosInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public decimal GrandTotal { get; set; }
}

public class CreatePosClosingDto
{
    public Guid CompanyId { get; set; }
    public Guid PosProfileId { get; set; }
    public Guid PosOpeningEntryId { get; set; }
    public Guid UserId { get; set; }
    public List<CreatePosClosingInvoiceDto> Invoices { get; set; } = new();
    public List<CreatePosClosingPaymentDto> Payments { get; set; } = new();
}

public class CreatePosClosingInvoiceDto
{
    public Guid PosInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public decimal GrandTotal { get; set; }
}

public class CreatePosClosingPaymentDto
{
    public Guid ModeOfPaymentId { get; set; }
    public string ModeName { get; set; } = null!;
    public decimal ExpectedAmount { get; set; }
    public decimal ClosingAmount { get; set; }
}
