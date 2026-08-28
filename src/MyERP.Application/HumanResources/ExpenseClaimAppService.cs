using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using MyERP.HumanResources.DomainServices;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

[Authorize(MyERPPermissions.Employees.Default)]
public class ExpenseClaimAppService : ApplicationService, IExpenseClaimAppService
{
    private readonly IRepository<ExpenseClaim, Guid> _repository;
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public ExpenseClaimAppService(IRepository<ExpenseClaim, Guid> repository, IRepository<Employee, Guid> employeeRepository)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
    }

    public async Task<PagedResultDto<ExpenseClaimDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
             query = query.Where(x => x.EmployeeName != null && x.EmployeeName.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(e => e.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ExpenseClaimDto>(totalCount, items.Select(x => ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(x)).ToList());
    }

    public async Task<ExpenseClaimDto> GetAsync(Guid id)
    {
        var ec = (await _repository.WithDetailsAsync()).First(e => e.Id == id);
        return ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(ec);
    }

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<ExpenseClaimDto> CreateAsync(CreateExpenseClaimDto input)
    {
        if (input.Expenses == null || input.Expenses.Length == 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        if (input.Expenses.Any(e => e.Amount <= 0))
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "Amount");
        }

        var ec = new ExpenseClaim(GuidGenerator.Create(), input.CompanyId, input.EmployeeId,
            input.PostingDate, CurrentTenant.Id)
        { EmployeeName = input.EmployeeName, ExpenseType = input.ExpenseType };
        foreach (var e in input.Expenses)
            ec.AddExpense(e.ExpenseDate, e.Description, e.Amount);
        await _repository.InsertAsync(ec);
        return ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(ec);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ExpenseClaimDto> ApproveAsync(Guid id)
    {
        var ec = (await _repository.WithDetailsAsync()).First(e => e.Id == id);
        await ValidateActingUserIsApproverAsync(ec);
        ec.Approve();
        await _repository.UpdateAsync(ec);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ExpenseClaim", ec.Id,
            "Approved", ec.CompanyId,
            ec.EmployeeName ?? ec.Id.ToString(), "Draft", "Approved", CurrentUser.Id,
            $"Expense Claim for {ec.EmployeeName} ({ec.TotalClaimedAmount:C}) approved", CurrentTenant.Id));

        return ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(ec);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ExpenseClaimDto> SubmitAsync(Guid id)
    {
        var ec = await _repository.GetAsync(id);
        ec.Submit();
        await _repository.UpdateAsync(ec);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ExpenseClaim", ec.Id,
            "Submitted", ec.CompanyId,
            ec.EmployeeName ?? ec.Id.ToString(), "Draft", "Submitted", CurrentUser.Id,
            $"Expense Claim for {ec.EmployeeName} ({ec.TotalClaimedAmount:C}) submitted", CurrentTenant.Id));

        return ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(ec);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ExpenseClaimDto> RejectAsync(Guid id)
    {
        var ec = await _repository.GetAsync(id);
        await ValidateActingUserIsApproverAsync(ec);
        ec.Reject();
        await _repository.UpdateAsync(ec);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ExpenseClaim", ec.Id,
            "Rejected", ec.CompanyId,
            ec.EmployeeName ?? ec.Id.ToString(), "Draft", "Rejected", CurrentUser.Id,
            $"Expense Claim for {ec.EmployeeName} rejected", CurrentTenant.Id));

        return ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(ec);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ExpenseClaimDto> CancelAsync(Guid id)
    {
        var ec = await _repository.GetAsync(id);
        ec.Cancel();
        await _repository.UpdateAsync(ec);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ExpenseClaim", ec.Id,
            "Cancelled", ec.CompanyId,
            ec.EmployeeName ?? ec.Id.ToString(), "Submitted", "Cancelled", CurrentUser.Id,
            $"Expense Claim for {ec.EmployeeName} cancelled", CurrentTenant.Id));

        return ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(ec);
    }

    /// <summary>
    /// Creates a Payment Entry to reimburse an approved/submitted expense claim.
    /// Per ERPNext: validates advance linkage to prevent double-payment.
    /// Per DO-NOT: "Allow expense claim GL posting without verifying advance linkage"
    /// Uses ExpenseClaimManager domain service for validation per DDD.
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Create)]
    public async Task<Guid> ReimburseAsync(Guid id, Guid paidFromAccountId)
    {
        var ec = (await _repository.WithDetailsAsync()).First(e => e.Id == id);

        // Delegate validation to domain service
        var claimManager = LazyServiceProvider.LazyGetRequiredService<ExpenseClaimManager>();
        claimManager.ValidateForReimbursement(ec);

        // Per DO-NOT: "Allow expense claim GL posting without verifying advance linkage
        // (double-payment risk)" — this method's own doc comment already claimed this check ran,
        // but ValidateAdvanceLinkage had no caller anywhere until now.
        if (ec.AdvancePaymentEntryId.HasValue)
        {
            var advancePeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.PaymentEntry, Guid>>();
            var advancePe = await advancePeRepo.FindAsync(ec.AdvancePaymentEntryId.Value);
            claimManager.ValidateAdvanceLinkage(ec, advancePe?.PaidAmount);
        }

        // Calculate reimbursable via domain service (single source of truth)
        var reimbursableAmount = claimManager.CalculateReimbursableAmount(ec);

        // Create Payment Entry (company pays employee)
        var numberGenerator = LazyServiceProvider.LazyGetRequiredService<IDocumentNumberGenerator>();
        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.PaymentEntry, Guid>>();
        var paymentNumber = await numberGenerator.GenerateAsync("PaymentEntry", ec.CompanyId);

        var pe = new MyERP.Accounting.Entities.PaymentEntry(
            GuidGenerator.Create(),
            ec.CompanyId,
            MyERP.Accounting.PaymentType.Pay,
            DateTime.UtcNow.Date,
            reimbursableAmount,
            paidFromAccountId,
            ec.PayableAccountId ?? paidFromAccountId);

        pe.PaymentNumber = paymentNumber;
        pe.PartyType = "Employee";
        pe.PartyId = ec.EmployeeId;
        pe.Notes = $"Reimbursement for expense claim {ec.ExpenseType} (Posted: {ec.PostingDate:yyyy-MM-dd})";

        // Reference row: this is what ExpenseClaimPaymentStatusJob matches on (ReferenceType ==
        // "ExpenseClaim") to keep TotalAmountReimbursed in sync — without it the nightly job finds
        // zero matching references for this claim and resets TotalAmountReimbursed back to 0,
        // silently un-reimbursing it even though this (now-posted) Payment Entry still exists.
        pe.References.Add(new MyERP.Accounting.Entities.PaymentEntryReference(
            GuidGenerator.Create(), pe.Id, "ExpenseClaim", ec.Id,
            totalAmount: ec.TotalSanctionedAmount, outstandingAmount: reimbursableAmount,
            allocatedAmount: reimbursableAmount));

        await peRepo.InsertAsync(pe, autoSave: true);

        // Per DO-NOT / the class's own doc comment: reimbursement must actually post GL, not just
        // flip status. PaymentEntryAppService.PostAsync's GL block only fires for payments against
        // a specific invoice/order — a standalone employee reimbursement has neither, so calling it
        // would silently post nothing either. Build the accrual+payment JE directly instead: DR
        // each claim line's expense account (there's no earlier accrual JE anywhere in this
        // entity's lifecycle — Approve/Submit don't post GL — so expense recognition happens here,
        // at reimbursement, same as the cash-basis Payment-Entry-only flow ERPNext uses when no
        // separate Journal Entry accrual exists), CR the bank/cash account actually paid from.
        pe.Submit();
        pe.Post();
        await peRepo.UpdateAsync(pe, autoSave: true);

        var fyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.FiscalYear, Guid>>();
        var fyQuery = await fyRepo.GetQueryableAsync();
        var fy = fyQuery.FirstOrDefault(f => f.CompanyId == ec.CompanyId
            && f.StartDate <= pe.PostingDate && f.EndDate >= pe.PostingDate);
        var fiscalYearId = fy?.Id ?? ec.CompanyId;

        var je = new MyERP.Accounting.Entities.JournalEntry(
            GuidGenerator.Create(), ec.CompanyId, fiscalYearId, pe.PostingDate, ec.TenantId)
        {
            ReferenceType = "PaymentEntry",
            ReferenceId = pe.Id,
            Narration = $"Expense claim reimbursement for {ec.EmployeeName} ({paymentNumber})",
        };

        var claimedTotal = ec.Expenses.Sum(e => e.Amount);
        if (claimedTotal > 0)
        {
            // Last line absorbs the rounding remainder so the sum of debit lines is exactly
            // reimbursableAmount — per-line proportional rounding alone can leave the JE a cent
            // short/over and Validate() rejects anything but an exact debit/credit match.
            var expenses = ec.Expenses.ToList();
            decimal allocated = 0;
            for (int i = 0; i < expenses.Count; i++)
            {
                var expense = expenses[i];
                var share = i == expenses.Count - 1
                    ? reimbursableAmount - allocated
                    : Math.Round(expense.Amount / claimedTotal * reimbursableAmount, 2);
                allocated += share;
                if (share <= 0) continue;
                je.AddLine(
                    accountId: expense.ExpenseAccountId ?? ec.PayableAccountId ?? paidFromAccountId,
                    amount: share,
                    isDebit: true,
                    description: expense.Description);
            }
        }
        je.AddLine(paidFromAccountId, reimbursableAmount, isDebit: false, description: "Expense claim reimbursement");

        je.Post();
        var jeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.JournalEntry, Guid>>();
        await jeRepo.InsertAsync(je, autoSave: true);

        // Update expense claim with reimbursement amount
        ec.TotalAmountReimbursed += reimbursableAmount;
        await _repository.UpdateAsync(ec, autoSave: true);

        return pe.Id;
    }

    /// <summary>
    /// Per ERPNext expense_claim.py: only the claimant's reporting manager (or an HR-manager
    /// role) may approve/reject. Unlike LeaveApplication, ExpenseClaim never stored an approver
    /// field to begin with (its ApprovalStatusBy is dead/unused) — resolved dynamically from the
    /// claimant's ReportsToEmployeeId here instead, matching ERPNext's own get_approvers()
    /// approach even more directly than the stored-field pattern used for Leave. Same
    /// incremental-adoption behavior: a no-op until ReportsTo/UserId are both linked.
    /// </summary>
    private async Task ValidateActingUserIsApproverAsync(ExpenseClaim ec)
    {
        var claimant = await _employeeRepository.FindAsync(ec.EmployeeId);
        if (claimant?.ReportsToEmployeeId == null)
        {
            return;
        }

        var manager = await _employeeRepository.FindAsync(claimant.ReportsToEmployeeId.Value);
        if (manager?.UserId == null)
        {
            return;
        }

        if (manager.UserId != CurrentUser.Id)
        {
            throw new BusinessException("MyERP:HR:009", "Only the claimant's reporting manager may approve or reject this expense claim.");
        }
    }
}

