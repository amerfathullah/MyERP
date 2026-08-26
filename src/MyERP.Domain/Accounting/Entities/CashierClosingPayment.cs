using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Accounting.Entities;

public class CashierClosingPayment : CreationAuditedEntity<Guid>
{
    public Guid CashierClosingId { get; set; }
    public string ModeOfPayment { get; set; } = null!;
    public decimal Amount { get; set; }

    protected CashierClosingPayment() { }

    public CashierClosingPayment(Guid id, Guid cashierClosingId, string modeOfPayment, decimal amount)
        : base(id)
    {
        CashierClosingId = cashierClosingId;
        ModeOfPayment = Check.NotNullOrWhiteSpace(modeOfPayment, nameof(modeOfPayment), CashierClosingConsts.MaxModeOfPaymentLength);
        Amount = amount;
    }
}
