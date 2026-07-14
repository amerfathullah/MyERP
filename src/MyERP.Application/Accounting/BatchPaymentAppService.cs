using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Batch Payment AppService — AP payment runs and AR batch receipts.
/// Exposes the domain BatchPaymentService with invoice outstanding validation.
/// </summary>
[Authorize(MyERPPermissions.PaymentEntries.Create)]
public class BatchPaymentAppService : ApplicationService
{
    private readonly BatchPaymentService _batchPaymentService;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepo;

    public BatchPaymentAppService(
        BatchPaymentService batchPaymentService,
        IRepository<SalesInvoice, Guid> salesInvoiceRepo,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepo)
    {
        _batchPaymentService = batchPaymentService;
        _salesInvoiceRepo = salesInvoiceRepo;
        _purchaseInvoiceRepo = purchaseInvoiceRepo;
    }

    /// <summary>
    /// Get outstanding invoices for a party (for batch payment selection UI).
    /// </summary>
    public async Task<List<BatchPaymentInvoiceDto>> GetOutstandingInvoicesAsync(
        GetOutstandingForBatchDto input)
    {
        var results = new List<BatchPaymentInvoiceDto>();

        if (input.PartyType == "Supplier")
        {
            var invoices = await _purchaseInvoiceRepo.GetListAsync(pi =>
                pi.CompanyId == input.CompanyId &&
                pi.SupplierId == input.PartyId &&
                pi.Status == DocumentStatus.Posted &&
                !pi.IsReturn);

            foreach (var pi in invoices.Where(i => i.OutstandingAmount > 0))
            {
                results.Add(new BatchPaymentInvoiceDto
                {
                    InvoiceId = pi.Id,
                    InvoiceNumber = pi.InvoiceNumber,
                    InvoiceType = "PurchaseInvoice",
                    PartyId = pi.SupplierId,
                    IssueDate = pi.IssueDate,
                    DueDate = pi.DueDate,
                    GrandTotal = pi.GrandTotal,
                    Outstanding = pi.OutstandingAmount,
                    CurrencyCode = pi.CurrencyCode
                });
            }
        }
        else // Customer
        {
            var invoices = await _salesInvoiceRepo.GetListAsync(si =>
                si.CompanyId == input.CompanyId &&
                si.CustomerId == input.PartyId &&
                si.Status == DocumentStatus.Posted &&
                !si.IsReturn);

            foreach (var si in invoices.Where(i => i.OutstandingAmount > 0))
            {
                results.Add(new BatchPaymentInvoiceDto
                {
                    InvoiceId = si.Id,
                    InvoiceNumber = si.InvoiceNumber,
                    InvoiceType = "SalesInvoice",
                    PartyId = si.CustomerId,
                    IssueDate = si.IssueDate,
                    DueDate = si.DueDate,
                    GrandTotal = si.GrandTotal,
                    Outstanding = si.OutstandingAmount,
                    CurrencyCode = si.CurrencyCode
                });
            }
        }

        return results.OrderBy(i => i.DueDate).ToList();
    }

    /// <summary>
    /// Create batch payment entries for selected invoices.
    /// </summary>
    public async Task<BatchPaymentResultDto> CreateBatchPaymentAsync(CreateBatchPaymentDto input)
    {
        var batchInput = new BatchPaymentInput
        {
            CompanyId = input.CompanyId,
            PaymentType = input.PaymentType,
            PartyType = input.PartyType,
            PaidFromAccountId = input.PaidFromAccountId,
            PaidToAccountId = input.PaidToAccountId,
            ModeOfPaymentId = input.ModeOfPaymentId,
            PostingDate = input.PostingDate ?? DateTime.Today,
            GroupByParty = input.GroupByParty,
            Items = input.Items.Select(i => new BatchPaymentItem
            {
                PartyId = i.PartyId,
                InvoiceId = i.InvoiceId,
                InvoiceType = i.InvoiceType,
                TotalAmount = i.TotalAmount,
                Outstanding = i.Outstanding,
                Amount = i.Amount,
                ExchangeRate = i.ExchangeRate
            }).ToList()
        };

        // Validate before processing
        var errors = _batchPaymentService.ValidateBatch(batchInput);
        if (errors.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems)
                .WithData("errors", string.Join("; ", errors));
        }

        var result = await _batchPaymentService.CreateBatchPaymentEntriesAsync(batchInput);

        return new BatchPaymentResultDto
        {
            SuccessCount = result.SuccessCount,
            ErrorCount = result.ErrorCount,
            TotalAmount = result.TotalAmount,
            Errors = result.Errors.Select(e => $"{e.PartyId}: {e.Message}").ToList(),
            CreatedPaymentEntryIds = result.CreatedEntries.Select(pe => pe.Id).ToList()
        };
    }
}

#region DTOs

public class GetOutstandingForBatchDto
{
    public Guid CompanyId { get; set; }
    public string PartyType { get; set; } = "Supplier";
    public Guid PartyId { get; set; }
}

public class BatchPaymentInvoiceDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string InvoiceType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal Outstanding { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
}

public class CreateBatchPaymentDto
{
    [Required]
    public Guid CompanyId { get; set; }

    public PaymentType PaymentType { get; set; } = PaymentType.Pay;
    public string PartyType { get; set; } = "Supplier";

    [Required]
    public Guid PaidFromAccountId { get; set; }

    [Required]
    public Guid PaidToAccountId { get; set; }

    public Guid? ModeOfPaymentId { get; set; }
    public DateTime? PostingDate { get; set; }
    public bool GroupByParty { get; set; } = true;

    [Required]
    public List<BatchPaymentItemDto> Items { get; set; } = new();
}

public class BatchPaymentItemDto
{
    public Guid PartyId { get; set; }
    public Guid InvoiceId { get; set; }
    public string InvoiceType { get; set; } = "PurchaseInvoice";
    public decimal TotalAmount { get; set; }
    public decimal Outstanding { get; set; }
    public decimal Amount { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
}

public class BatchPaymentResultDto
{
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<Guid> CreatedPaymentEntryIds { get; set; } = new();
}

#endregion
