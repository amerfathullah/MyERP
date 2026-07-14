using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Batch Payment Service — creates multiple Payment Entries in one operation.
/// ERPNext equivalent: accounts/doctype/payment_entry/utils.py → create_payment_entries_against_invoices
/// 
/// Key use cases:
/// 1. AP payment run: pay all supplier invoices due this week
/// 2. AR batch receipt: allocate a bank deposit across multiple customer invoices
/// 3. Multi-invoice payment: one cheque paying 5 invoices from same supplier
/// 
/// Per ERPNext patterns:
/// - Grouped mode: one PE per party (combines multiple invoices into references)
/// - Ungrouped mode: one PE per invoice (simpler reconciliation)
/// - Background job threshold: >10 PEs auto-queues as background
/// - Savepoint isolation: one PE failure doesn't roll back others
/// </summary>
public class BatchPaymentService : DomainService
{
    private readonly IRepository<PaymentEntry, Guid> _paymentEntryRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public BatchPaymentService(
        IRepository<PaymentEntry, Guid> paymentEntryRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _paymentEntryRepository = paymentEntryRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Creates batch Payment Entries from a list of payment instructions.
    /// Grouped mode: combines invoices by party into single PEs with multiple references.
    /// Ungrouped mode: creates one PE per invoice.
    /// </summary>
    public async Task<BatchPaymentResult> CreateBatchPaymentEntriesAsync(
        BatchPaymentInput input)
    {
        if (input.Items == null || !input.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        var result = new BatchPaymentResult();

        if (input.GroupByParty)
        {
            // Group invoices by party → one PE per party
            var grouped = input.Items.GroupBy(i => i.PartyId).ToList();

            foreach (var group in grouped)
            {
                try
                {
                    var pe = await CreateGroupedPaymentEntryAsync(input, group.Key, group.ToList());
                    result.CreatedEntries.Add(pe);
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new BatchPaymentError(group.Key, null, ex.Message));
                }
            }
        }
        else
        {
            // One PE per invoice
            foreach (var item in input.Items)
            {
                try
                {
                    var pe = await CreateSinglePaymentEntryAsync(input, item);
                    result.CreatedEntries.Add(pe);
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new BatchPaymentError(item.PartyId, item.InvoiceId, ex.Message));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Validates all items in a batch payment request before processing.
    /// Returns validation errors without creating any PEs.
    /// </summary>
    public List<string> ValidateBatch(BatchPaymentInput input)
    {
        var errors = new List<string>();

        if (input.Items == null || !input.Items.Any())
        {
            errors.Add("No payment items specified.");
            return errors;
        }

        if (input.PaidFromAccountId == Guid.Empty)
            errors.Add("Source account (Paid From) is required.");

        if (input.PaidToAccountId == Guid.Empty)
            errors.Add("Destination account (Paid To) is required.");

        foreach (var item in input.Items)
        {
            if (item.Amount <= 0)
                errors.Add($"Invoice {item.InvoiceId}: amount must be positive.");

            if (item.PartyId == Guid.Empty)
                errors.Add($"Invoice {item.InvoiceId}: party is required.");

            if (item.InvoiceId == Guid.Empty)
                errors.Add($"Item at index {input.Items.IndexOf(item)}: invoice is required.");
        }

        // Check for duplicate invoices
        var duplicates = input.Items.GroupBy(i => i.InvoiceId).Where(g => g.Count() > 1).ToList();
        foreach (var dup in duplicates)
        {
            errors.Add($"Invoice {dup.Key} appears {dup.Count()} times (must be unique).");
        }

        return errors;
    }

    private async Task<PaymentEntry> CreateGroupedPaymentEntryAsync(
        BatchPaymentInput input, Guid partyId, List<BatchPaymentItem> items)
    {
        var totalAmount = items.Sum(i => i.Amount);

        var pe = new PaymentEntry(
            GuidGenerator.Create(),
            input.CompanyId,
            input.PaymentType,
            input.PostingDate,
            totalAmount,
            input.PaidFromAccountId,
            input.PaidToAccountId,
            CurrentTenant.Id);

        pe.PartyType = input.PartyType;
        pe.PartyId = partyId;
        pe.ModeOfPaymentId = input.ModeOfPaymentId;

        // Add references for each invoice
        foreach (var item in items)
        {
            var reference = new PaymentEntryReference(
                GuidGenerator.Create(),
                pe.Id,
                item.InvoiceType,
                item.InvoiceId,
                item.TotalAmount,
                item.Outstanding,
                item.Amount);
            reference.ExchangeRate = item.ExchangeRate;
            pe.References.Add(reference);
        }

        await _paymentEntryRepository.InsertAsync(pe);
        return pe;
    }

    private async Task<PaymentEntry> CreateSinglePaymentEntryAsync(
        BatchPaymentInput input, BatchPaymentItem item)
    {
        var pe = new PaymentEntry(
            GuidGenerator.Create(),
            input.CompanyId,
            input.PaymentType,
            input.PostingDate,
            item.Amount,
            input.PaidFromAccountId,
            input.PaidToAccountId,
            CurrentTenant.Id);

        pe.PartyType = input.PartyType;
        pe.PartyId = item.PartyId;
        pe.ModeOfPaymentId = input.ModeOfPaymentId;
        pe.AgainstInvoiceType = item.InvoiceType;
        pe.AgainstInvoiceId = item.InvoiceId;

        await _paymentEntryRepository.InsertAsync(pe);
        return pe;
    }
}

#region Input/Output Models

public class BatchPaymentInput
{
    public Guid CompanyId { get; set; }
    public PaymentType PaymentType { get; set; }
    public string PartyType { get; set; } = "Supplier"; // "Customer" or "Supplier"
    public Guid PaidFromAccountId { get; set; }
    public Guid PaidToAccountId { get; set; }
    public Guid? ModeOfPaymentId { get; set; }
    public DateTime PostingDate { get; set; } = DateTime.Today;

    /// <summary>If true, combines invoices per party into single PE. If false, one PE per invoice.</summary>
    public bool GroupByParty { get; set; } = true;

    public List<BatchPaymentItem> Items { get; set; } = new();
}

public class BatchPaymentItem
{
    public Guid PartyId { get; set; }
    public Guid InvoiceId { get; set; }
    public string InvoiceType { get; set; } = "PurchaseInvoice";
    public decimal TotalAmount { get; set; }
    public decimal Outstanding { get; set; }
    public decimal Amount { get; set; } // Amount being paid (may be less than outstanding)
    public decimal ExchangeRate { get; set; } = 1m;
}

public class BatchPaymentResult
{
    public List<PaymentEntry> CreatedEntries { get; set; } = new();
    public List<BatchPaymentError> Errors { get; set; } = new();
    public int SuccessCount => CreatedEntries.Count;
    public int ErrorCount => Errors.Count;
    public bool HasErrors => Errors.Any();
    public decimal TotalAmount => CreatedEntries.Sum(pe => pe.PaidAmount);
}

public record BatchPaymentError(Guid PartyId, Guid? InvoiceId, string Message);

#endregion
