using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Proforma Invoice — enables partial/progressive invoicing before delivery (v16 feature).
/// Created exclusively from Sales Order via make_proforma_invoice path (in_create = true).
/// Per ERPNext PR #57263: selling/doctype/proforma_invoice.
///
/// Key rules:
/// - Only created against SUBMITTED Sales Orders
/// - Over-proforma is a WARNING (not block) per gotcha #2450
/// - Cancelled proformas cannot be emailed per gotcha #2452
/// - PDF generated from in-memory SO clone (never saved) per gotcha #2435
/// - Gated by Selling Settings.enable_proforma_invoice per gotcha #2454
/// </summary>
public class ProformaInvoice : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string ProformaNumber { get; set; } = null!;
    public DateTime ProformaDate { get; set; }

    /// <summary>Linked Sales Order (mandatory — sole creation path).</summary>
    public Guid SalesOrderId { get; set; }

    /// <summary>Customer from the Sales Order.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Quantity or Amount basis for line items.</summary>
    public ProformaInvoiceBasis BasedOn { get; set; } = ProformaInvoiceBasis.Quantity;

    /// <summary>When Amount-based: hides qty/rate on printed proforma.</summary>
    public bool HideItemQty { get; set; }

    public string? CurrencyCode { get; set; }
    public decimal GrandTotal { get; private set; }
    public decimal TotalQty { get; private set; }

    public ProformaInvoiceStatus Status { get; private set; } = ProformaInvoiceStatus.Draft;

    /// <summary>URL to the attached PDF file (generated on submit).</summary>
    public string? ProformaPdfUrl { get; set; }

    /// <summary>When the proforma was emailed to the customer.</summary>
    public DateTime? SentOn { get; set; }

    /// <summary>Comma-separated list of email recipients.</summary>
    public string? EmailedTo { get; set; }

    public ICollection<ProformaInvoiceItem> Items { get; private set; } = new List<ProformaInvoiceItem>();

    protected ProformaInvoice() { }

    public ProformaInvoice(
        Guid id,
        Guid companyId,
        Guid salesOrderId,
        Guid customerId,
        DateTime proformaDate,
        ProformaInvoiceBasis basedOn = ProformaInvoiceBasis.Quantity,
        string? currencyCode = null)
        : base(id)
    {
        Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        Check.NotDefaultOrNull<Guid>(salesOrderId, nameof(salesOrderId));
        Check.NotDefaultOrNull<Guid>(customerId, nameof(customerId));

        CompanyId = companyId;
        SalesOrderId = salesOrderId;
        CustomerId = customerId;
        ProformaDate = proformaDate;
        BasedOn = basedOn;
        CurrencyCode = currencyCode;
    }

    /// <summary>
    /// Add an item line. Only allowed when Draft.
    /// For Quantity basis: rate comes from SO, amount = qty × rate.
    /// For Amount basis: both qty and amount are user-entered, rate = amount / qty.
    /// </summary>
    public void AddItem(
        Guid salesOrderItemId,
        Guid itemId,
        string itemCode,
        string itemName,
        decimal quantity,
        decimal rate,
        string? uom = null)
    {
        if (Status != ProformaInvoiceStatus.Draft)
            throw new BusinessException("MyERP:01001")
                .WithData("status", Status.ToString());

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        var item = new ProformaInvoiceItem(
            Guid.NewGuid(),
            Id,
            salesOrderItemId,
            itemId,
            itemCode,
            itemName,
            quantity,
            rate,
            uom);

        Items.Add(item);
        RecalculateTotals();
    }

    /// <summary>Submits the proforma invoice (Draft → Issued).</summary>
    public void Submit()
    {
        if (Status != ProformaInvoiceStatus.Draft)
            throw new BusinessException("MyERP:01001")
                .WithData("status", Status.ToString());

        if (!Items.Any())
            throw new BusinessException("MyERP:01007");

        Status = ProformaInvoiceStatus.Issued;
    }

    /// <summary>Cancels the proforma invoice (Issued → Cancelled).</summary>
    public void Cancel()
    {
        if (Status != ProformaInvoiceStatus.Issued)
            throw new BusinessException("MyERP:01001")
                .WithData("status", Status.ToString());

        Status = ProformaInvoiceStatus.Cancelled;
    }

    /// <summary>Records that the proforma was emailed to recipients.</summary>
    public void MarkEmailed(string recipients)
    {
        if (Status == ProformaInvoiceStatus.Cancelled)
            throw new BusinessException("MyERP:01001")
                .WithData("status", Status.ToString());

        SentOn = DateTime.UtcNow;
        EmailedTo = recipients;
    }

    private void RecalculateTotals()
    {
        TotalQty = Items.Sum(i => i.Quantity);
        GrandTotal = Items.Sum(i => i.Amount);
    }
}
