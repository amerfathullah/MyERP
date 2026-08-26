using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Bank Account Balance — point-in-time balance snapshot for a <see cref="BankAccount"/>,
/// used for balance-over-time reporting and reconciliation reference. Company is derived
/// from the bank account, matching ERPNext's read-only fetch_from field.
/// Maps to ERPNext accounts/doctype/bank_account_balance.
/// </summary>
public class BankAccountBalance : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid BankAccountId { get; set; }
    public DateTime Date { get; set; }
    public decimal Balance { get; set; }

    protected BankAccountBalance() { }

    public BankAccountBalance(Guid id, Guid bankAccountId, DateTime date, decimal balance, Guid? tenantId = null)
        : base(id)
    {
        BankAccountId = bankAccountId;
        Date = date;
        Balance = balance;
        TenantId = tenantId;
    }
}
