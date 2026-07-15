using System;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.HumanResources.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.HumanResources.DomainServices;

/// <summary>
/// Domain service for Expense Claim business rules.
/// Handles reimbursement calculation, advance linkage validation, and payment creation.
/// Per DO-NOT: "Allow expense claim GL posting without verifying advance linkage (double-payment risk)"
/// </summary>
public class ExpenseClaimManager : DomainService
{
    private readonly IRepository<ExpenseClaim, Guid> _claimRepository;

    public ExpenseClaimManager(IRepository<ExpenseClaim, Guid> claimRepository)
    {
        _claimRepository = claimRepository;
    }

    /// <summary>
    /// Calculates the reimbursable amount for an expense claim.
    /// Reimbursable = TotalSanctioned - AdvanceAmount - AlreadyReimbursed.
    /// Returns 0 if nothing pending.
    /// </summary>
    public decimal CalculateReimbursableAmount(ExpenseClaim claim)
    {
        var pending = claim.TotalSanctionedAmount - claim.AdvanceAmount - claim.TotalAmountReimbursed;
        return Math.Max(0, pending);
    }

    /// <summary>
    /// Validates that the claim can be reimbursed.
    /// Must be Submitted and have pending reimbursable amount.
    /// </summary>
    public void ValidateForReimbursement(ExpenseClaim claim)
    {
        if (claim.Status != DocumentStatus.Submitted)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("currentStatus", claim.Status.ToString())
                .WithData("action", "Reimburse");
        }

        var reimbursable = CalculateReimbursableAmount(claim);
        if (reimbursable <= 0)
        {
            throw new BusinessException("MyERP:14003")
                .WithData("claimId", claim.Id)
                .WithData("totalSanctioned", claim.TotalSanctionedAmount)
                .WithData("advance", claim.AdvanceAmount)
                .WithData("alreadyReimbursed", claim.TotalAmountReimbursed);
        }
    }

    /// <summary>
    /// Records reimbursement after Payment Entry is created.
    /// Updates TotalAmountReimbursed on the claim.
    /// </summary>
    public async Task RecordReimbursementAsync(Guid claimId, decimal amount)
    {
        var claim = await _claimRepository.GetAsync(claimId);
        claim.TotalAmountReimbursed += amount;
        await _claimRepository.UpdateAsync(claim);
    }

    /// <summary>
    /// Validates advance payment linkage is consistent.
    /// If advance is linked, AdvanceAmount must not exceed the Payment Entry amount.
    /// Prevents double-payment when both advance and reimbursement are processed.
    /// </summary>
    public void ValidateAdvanceLinkage(ExpenseClaim claim, decimal? advancePaymentAmount)
    {
        if (claim.AdvancePaymentEntryId.HasValue && advancePaymentAmount.HasValue)
        {
            if (claim.AdvanceAmount > advancePaymentAmount.Value)
            {
                throw new BusinessException("MyERP:14005")
                    .WithData("advanceAmount", claim.AdvanceAmount)
                    .WithData("paymentAmount", advancePaymentAmount.Value);
            }
        }
    }
}
