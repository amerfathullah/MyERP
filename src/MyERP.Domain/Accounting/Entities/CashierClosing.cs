using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Cashier Closing aggregate root — tracks end-of-shift cashier totals, cash custody, returns, and outstanding POS invoices.
/// Maps to ERPNext accounts/doctype/cashier_closing.
/// </summary>
public class CashierClosing : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string ClosingNumber { get; set; } = null!;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = null!;
    public DateTime Date { get; set; }
    public TimeSpan FromTime { get; set; }
    public TimeSpan ToTime { get; set; }
    public decimal Expense { get; set; }
    public decimal Custody { get; set; }
    public decimal Returns { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal NetAmount { get; set; }
    public bool IsSubmitted { get; set; }

    public virtual ICollection<CashierClosingPayment> Payments { get; protected set; } = new Collection<CashierClosingPayment>();

    protected CashierClosing() { }

    public CashierClosing(
        Guid id,
        string closingNumber,
        Guid userId,
        string userName,
        DateTime date,
        TimeSpan fromTime,
        TimeSpan toTime,
        decimal expense = 0,
        decimal custody = 0,
        decimal returns = 0,
        decimal outstandingAmount = 0,
        Guid? tenantId = null)
        : base(id)
    {
        ClosingNumber = Check.NotNullOrWhiteSpace(closingNumber, nameof(closingNumber), CashierClosingConsts.MaxClosingNumberLength);
        UserId = userId;
        UserName = Check.NotNullOrWhiteSpace(userName, nameof(userName), CashierClosingConsts.MaxUserNameLength);
        Date = date.Date;
        FromTime = fromTime;
        ToTime = toTime;
        Expense = expense;
        Custody = custody;
        Returns = returns;
        OutstandingAmount = outstandingAmount;
        TenantId = tenantId;

        ValidateTimes();
        CalculateNetAmount();
    }

    public void ValidateTimes()
    {
        if (FromTime >= ToTime)
        {
            throw new BusinessException("MyERP:CashierClosing:FromTimeMustBeLessThanToTime", "From Time must be less than To Time.");
        }
    }

    public void AddPayment(Guid id, string modeOfPayment, decimal amount)
    {
        Payments.Add(new CashierClosingPayment(id, Id, modeOfPayment, amount));
        CalculateNetAmount();
    }

    public void ClearPayments()
    {
        Payments.Clear();
        CalculateNetAmount();
    }

    public void SetOutstandingAmount(decimal outstanding)
    {
        OutstandingAmount = outstanding;
        CalculateNetAmount();
    }

    public void CalculateNetAmount()
    {
        var totalPayments = Payments.Sum(p => p.Amount);
        // Formula per ERPNext cashier_closing.py:
        // net_amount = total_payments + outstanding_amount + expense - custody + returns
        NetAmount = totalPayments + OutstandingAmount + Expense - Custody + Returns;
    }

    public void Submit()
    {
        ValidateTimes();
        CalculateNetAmount();
        IsSubmitted = true;
    }
}
