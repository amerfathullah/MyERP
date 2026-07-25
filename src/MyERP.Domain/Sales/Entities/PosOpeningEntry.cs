using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// POS Opening Entry — records the start of a POS cashier shift.
/// Per ERPNext accounts/doctype/pos_opening_entry:
/// - One open entry per POS Profile + one per user (enforced).
/// - Records opening balance per payment mode (initial float/cash).
/// - Must be closed via POS Closing Entry before opening a new one.
/// - Cannot cancel while unconsolidated invoices exist.
/// Per DO-NOT: "Allow multiple POS sessions for same profile or same user"
/// </summary>
public class PosOpeningEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PosProfileId { get; set; }
    public Guid UserId { get; set; }

    public DateTime OpeningDate { get; set; }
    public DateTime OpeningTime { get; set; }

    public PosOpeningStatus Status { get; private set; } = PosOpeningStatus.Open;

    /// <summary>Linked closing entry ID when shift is closed.</summary>
    public Guid? PosClosingEntryId { get; set; }

    private readonly List<PosOpeningPayment> _payments = new();
    public IReadOnlyList<PosOpeningPayment> Payments => _payments.AsReadOnly();

    protected PosOpeningEntry() { }

    public PosOpeningEntry(Guid id, Guid companyId, Guid posProfileId, Guid userId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        PosProfileId = posProfileId;
        UserId = userId;
        OpeningDate = DateTime.UtcNow.Date;
        OpeningTime = DateTime.UtcNow;
        TenantId = tenantId;
    }

    /// <summary>
    /// Records the initial cash/payment float for this payment mode.
    /// Called during shift opening to register starting balances.
    /// </summary>
    public void AddOpeningBalance(Guid modeOfPaymentId, string modeName, decimal openingAmount)
    {
        if (Status != PosOpeningStatus.Open)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _payments.Add(new PosOpeningPayment(Guid.NewGuid(), Id, modeOfPaymentId, modeName, openingAmount));
    }

    /// <summary>
    /// Closes this opening entry (links to a POS Closing Entry).
    /// Per ERPNext: POS Opening cannot be cancelled while open — must close first.
    /// </summary>
    public void Close(Guid closingEntryId)
    {
        if (Status != PosOpeningStatus.Open)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        PosClosingEntryId = closingEntryId;
        Status = PosOpeningStatus.Closed;
    }

    /// <summary>
    /// Cancels this opening entry. Only allowed when already Closed.
    /// Per DO-NOT: "Cancel POS Opening Entry while unconsolidated invoices exist"
    /// </summary>
    public void Cancel()
    {
        if (Status != PosOpeningStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("reason", "Must close POS session before cancelling");

        Status = PosOpeningStatus.Cancelled;
    }

    /// <summary>Total opening float across all payment modes.</summary>
    public decimal TotalOpeningAmount => _payments.Sum(p => p.OpeningAmount);
}

public class PosOpeningPayment : CreationAuditedEntity<Guid>
{
    public Guid PosOpeningEntryId { get; set; }
    public Guid ModeOfPaymentId { get; set; }
    public string ModeName { get; set; } = null!;
    public decimal OpeningAmount { get; set; }

    protected PosOpeningPayment() { }

    public PosOpeningPayment(Guid id, Guid openingEntryId, Guid modeOfPaymentId, string modeName, decimal openingAmount)
        : base(id)
    {
        PosOpeningEntryId = openingEntryId;
        ModeOfPaymentId = modeOfPaymentId;
        ModeName = modeName;
        OpeningAmount = openingAmount;
    }
}

public enum PosOpeningStatus
{
    Open = 0,
    Closed = 1,
    Cancelled = 2,
}
