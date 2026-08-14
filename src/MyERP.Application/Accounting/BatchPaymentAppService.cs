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
public class BatchPaymentAppService : ApplicationService, IBatchPaymentAppService
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
    /// Validate and partition selected invoice IDs into payable vs excluded (with reasons).
    /// Per ERPNext PR #57703: returns only payable invoices, excludes debit notes, internal transfers, and already-paid.
    /// </summary>
    public async Task<PayableInvoicePartitionDto> GetPayableInvoicesAsync(
        ValidatePayableInvoicesDto input)
    {
        if (input.InvoiceIds == null || !input.InvoiceIds.Any())
            return new PayableInvoicePartitionDto();

        var queryable = await _purchaseInvoiceRepo.GetQueryableAsync();
        var invoices = queryable
            .Where(pi => input.InvoiceIds.Contains(pi.Id) && pi.Status == DocumentStatus.Posted)
            .Select(pi => new
            {
                pi.Id,
                pi.InvoiceNumber,
                pi.SupplierId,
                pi.CreditToAccountId,
                pi.OutstandingAmount,
                pi.ExchangeRate,
                pi.IsReturn,
                pi.CurrencyCode,
                pi.GrandTotal
            })
            .ToList();

        // Resolve which suppliers are internal (represent another company)
        var supplierIds = invoices.Select(i => i.SupplierId).Distinct().ToList();
        var supplierQueryable = await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>()
            .GetQueryableAsync();
        var internalSupplierIds = supplierQueryable
            .Where(s => supplierIds.Contains(s.Id) && s.RepresentsCompanyId != null)
            .Select(s => s.Id)
            .ToHashSet();

        var payable = new List<PayableInvoiceInfoDto>();
        var excluded = new List<ExcludedInvoiceDto>();

        foreach (var pi in invoices)
        {
            if (pi.IsReturn)
                excluded.Add(new ExcludedInvoiceDto { InvoiceId = pi.Id, InvoiceNumber = pi.InvoiceNumber, Reason = "Debit Note" });
            else if (internalSupplierIds.Contains(pi.SupplierId))
                excluded.Add(new ExcludedInvoiceDto { InvoiceId = pi.Id, InvoiceNumber = pi.InvoiceNumber, Reason = "Internal Transfer" });
            else if (pi.OutstandingAmount <= 0)
                excluded.Add(new ExcludedInvoiceDto { InvoiceId = pi.Id, InvoiceNumber = pi.InvoiceNumber, Reason = "Already Paid" });
            else
                payable.Add(new PayableInvoiceInfoDto
                {
                    InvoiceId = pi.Id,
                    InvoiceNumber = pi.InvoiceNumber,
                    SupplierId = pi.SupplierId,
                    PartyAccountId = pi.CreditToAccountId,
                    Outstanding = pi.OutstandingAmount * (pi.ExchangeRate > 0 ? pi.ExchangeRate : 1m),
                    CurrencyCode = pi.CurrencyCode
                });
        }

        // IDs not found (cancelled/deleted since report loaded)
        var foundIds = invoices.Select(i => i.Id).ToHashSet();
        foreach (var id in input.InvoiceIds.Where(id => !foundIds.Contains(id)))
            excluded.Add(new ExcludedInvoiceDto { InvoiceId = id, Reason = "Not available" });

        return new PayableInvoicePartitionDto
        {
            Payable = payable,
            Excluded = excluded,
            TotalPayable = payable.Sum(p => p.Outstanding),
            PaymentEntryCount = payable.GroupBy(p => (p.SupplierId, p.PartyAccountId)).Count()
        };
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

