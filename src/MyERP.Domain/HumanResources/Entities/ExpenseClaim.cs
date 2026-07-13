using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Expense Claim — employee reimbursement request with expense detail lines.
/// Cannot post GL without verifying advance linkage (double-payment risk).
/// Maps to ERPNext hr/doctype/expense_claim.
/// </summary>
public class ExpenseClaim : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }

    public DateTime PostingDate { get; set; }
    public Guid? ApprovalStatusBy { get; set; }

    /// <summary>Expense type: Travel, Food, Accommodation, etc.</summary>
    public string? ExpenseType { get; set; }

    /// <summary>Payable account for GL posting.</summary>
    public Guid? PayableAccountId { get; set; }

    /// <summary>Linked advance Payment Entry (if employee took advance).</summary>
    public Guid? AdvancePaymentEntryId { get; set; }
    public decimal AdvanceAmount { get; set; }

    public decimal TotalClaimedAmount { get; private set; }
    public decimal TotalSanctionedAmount { get; set; }
    public decimal TotalAmountReimbursed { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    private readonly List<ExpenseClaimDetail> _expenses = new();
    public IReadOnlyList<ExpenseClaimDetail> Expenses => _expenses.AsReadOnly();

    protected ExpenseClaim() { }

    public ExpenseClaim(Guid id, Guid companyId, Guid employeeId,
        DateTime postingDate, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        EmployeeId = employeeId;
        PostingDate = postingDate;
        TenantId = tenantId;
    }

    public void AddExpense(DateTime expenseDate, string description, decimal amount,
        Guid? expenseAccountId = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _expenses.Add(new ExpenseClaimDetail(Guid.NewGuid(), Id, expenseDate,
            description, amount, expenseAccountId));
        TotalClaimedAmount = _expenses.Sum(e => e.Amount);
    }

    public void Approve()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_expenses.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        TotalSanctionedAmount = TotalClaimedAmount;
        Status = DocumentStatus.Approved;
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Approved)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    public void Reject()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Rejected;
    }

    public void Cancel()
    {
        if (Status is not (DocumentStatus.Submitted or DocumentStatus.Approved))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }
}

public class ExpenseClaimDetail : FullAuditedEntity<Guid>
{
    public Guid ExpenseClaimId { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }
    public Guid? ExpenseAccountId { get; set; }

    protected ExpenseClaimDetail() { }

    public ExpenseClaimDetail(Guid id, Guid claimId, DateTime expenseDate,
        string description, decimal amount, Guid? expenseAccountId) : base(id)
    {
        ExpenseClaimId = claimId;
        ExpenseDate = expenseDate;
        Description = description;
        Amount = amount;
        ExpenseAccountId = expenseAccountId;
    }
}
