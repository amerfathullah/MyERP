using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// POS Closing Entry — aggregates POS Invoices for a cashier shift.
/// Maps to ERPNext accounts/doctype/pos_closing_entry.
/// Flow: POS Opening → POS Invoices (during shift) → POS Closing → Consolidated SI.
/// Per DO-NOT: posting_date always overridden to now(), cannot backdate.
/// </summary>
public class PosClosingEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid PosProfileId { get; set; }
    public Guid PosOpeningEntryId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Always set to now() — per DO-NOT: cannot backdate POS Closing.</summary>
    public DateTime PostingDate { get; set; }
    public DateTime PostingTime { get; set; }

    /// <summary>Net total across all POS invoices in this shift.</summary>
    public decimal GrandTotal { get; set; }
    /// <summary>Total quantity of items sold.</summary>
    public decimal NetTotal { get; set; }
    /// <summary>Total taxes collected.</summary>
    public decimal TotalTaxes { get; set; }

    public PosClosingStatus Status { get; private set; } = PosClosingStatus.Draft;

    /// <summary>Consolidated Sales Invoice ID created on submit.</summary>
    public Guid? ConsolidatedSalesInvoiceId { get; set; }

    private readonly List<PosClosingPayment> _payments = new();
    public IReadOnlyList<PosClosingPayment> Payments => _payments.AsReadOnly();

    private readonly List<PosClosingInvoice> _invoices = new();
    public IReadOnlyList<PosClosingInvoice> Invoices => _invoices.AsReadOnly();

    protected PosClosingEntry() { }

    public PosClosingEntry(Guid id, Guid companyId, Guid posProfileId, Guid posOpeningEntryId,
        Guid userId, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        PosProfileId = posProfileId;
        PosOpeningEntryId = posOpeningEntryId;
        UserId = userId;
        // Per ERPNext: posting_date/time always overridden to now
        PostingDate = DateTime.UtcNow.Date;
        PostingTime = DateTime.UtcNow;
        TenantId = tenantId;
    }

    /// <summary>
    /// Adds a payment mode with expected and actual amounts for reconciliation.
    /// Difference = ExpectedAmount - ClosingAmount (variance).
    /// </summary>
    public void AddPayment(Guid modeOfPaymentId, string modeName, decimal expectedAmount, decimal closingAmount)
    {
        if (Status != PosClosingStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _payments.Add(new PosClosingPayment(Guid.NewGuid(), Id, modeOfPaymentId, modeName, expectedAmount, closingAmount));
    }

    /// <summary>
    /// Links a POS Invoice to this closing entry.
    /// </summary>
    public void AddInvoice(Guid posInvoiceId, string invoiceNumber, decimal grandTotal)
    {
        if (Status != PosClosingStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _invoices.Add(new PosClosingInvoice(Guid.NewGuid(), Id, posInvoiceId, invoiceNumber, grandTotal));
    }

    public void Submit()
    {
        if (Status != PosClosingStatus.Draft || !_invoices.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Recalculate totals from linked invoices
        GrandTotal = _invoices.Sum(i => i.GrandTotal);
        Status = PosClosingStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != PosClosingStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = PosClosingStatus.Cancelled;
    }

    /// <summary>Total variance across all payment modes.</summary>
    public decimal TotalDifference => _payments.Sum(p => p.Difference);
}

public class PosClosingPayment : CreationAuditedEntity<Guid>
{
    public Guid PosClosingEntryId { get; set; }
    public Guid ModeOfPaymentId { get; set; }
    public string ModeName { get; set; } = null!;
    /// <summary>System-calculated expected amount from POS invoices.</summary>
    public decimal ExpectedAmount { get; set; }
    /// <summary>Actual counted amount entered by cashier.</summary>
    public decimal ClosingAmount { get; set; }
    /// <summary>Expected - Actual. Positive = short, Negative = overage.</summary>
    public decimal Difference => ExpectedAmount - ClosingAmount;

    protected PosClosingPayment() { }
    public PosClosingPayment(Guid id, Guid closingEntryId, Guid modeOfPaymentId, string modeName,
        decimal expectedAmount, decimal closingAmount) : base(id)
    {
        PosClosingEntryId = closingEntryId;
        ModeOfPaymentId = modeOfPaymentId;
        ModeName = modeName;
        ExpectedAmount = expectedAmount;
        ClosingAmount = closingAmount;
    }
}

public class PosClosingInvoice : CreationAuditedEntity<Guid>
{
    public Guid PosClosingEntryId { get; set; }
    public Guid PosInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public decimal GrandTotal { get; set; }

    protected PosClosingInvoice() { }
    public PosClosingInvoice(Guid id, Guid closingEntryId, Guid posInvoiceId, string invoiceNumber, decimal grandTotal) : base(id)
    {
        PosClosingEntryId = closingEntryId;
        PosInvoiceId = posInvoiceId;
        InvoiceNumber = invoiceNumber;
        GrandTotal = grandTotal;
    }
}

public enum PosClosingStatus
{
    Draft = 0,
    Submitted = 1,
    Cancelled = 2,
}
