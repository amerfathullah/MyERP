using System;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.Users;
using Xunit;

namespace MyERP.HumanResources;

/// <summary>
/// Regression coverage extending [[project_myerp_migration_2026_08_27t]]'s Employee.UserId/
/// ReportsToEmployeeId infrastructure to ExpenseClaim: per ERPNext expense_claim.py, only the
/// claimant's reporting manager may approve/reject. Unlike LeaveApplication, ExpenseClaim never
/// had a stored approver field (ApprovalStatusBy is dead/unused) — resolved dynamically from
/// ReportsToEmployeeId instead of storing a derived value, matching ERPNext's own
/// get_approvers() approach.
/// </summary>
public abstract class ExpenseClaimApproverEnforcementTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task RejectAsync_ManagerHasLinkedUser_WrongActingUser_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var claimRepository = GetRequiredService<IRepository<ExpenseClaim, Guid>>();
            var expenseClaimAppService = GetRequiredService<IExpenseClaimAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Expense Approver Test Co 1"), autoSave: true);
            var someoneElsesUserId = Guid.NewGuid();
            var manager = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-EMGR-1", "Manager") { UserId = someoneElsesUserId }, autoSave: true);
            var claimant = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-ECLM-1", "Claimant") { ReportsToEmployeeId = manager.Id }, autoSave: true);

            var claim = new ExpenseClaim(Guid.NewGuid(), company.Id, claimant.Id, DateTime.Today);
            claim.AddExpense(DateTime.Today, "Taxi", 50m);
            await claimRepository.InsertAsync(claim, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() => expenseClaimAppService.RejectAsync(claim.Id));
            ex.Code.ShouldBe("MyERP:HR:009");
        });
    }

    [Fact]
    public async Task RejectAsync_ClaimantHasNoManager_FallsBackToPermissionOnlyGate_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var claimRepository = GetRequiredService<IRepository<ExpenseClaim, Guid>>();
            var expenseClaimAppService = GetRequiredService<IExpenseClaimAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Expense Approver Test Co 2"), autoSave: true);
            var claimant = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-ECLM-2", "Claimant"), autoSave: true); // no manager

            var claim = new ExpenseClaim(Guid.NewGuid(), company.Id, claimant.Id, DateTime.Today);
            claim.AddExpense(DateTime.Today, "Taxi", 50m);
            await claimRepository.InsertAsync(claim, autoSave: true);

            var result = await expenseClaimAppService.RejectAsync(claim.Id);
            result.Status.ShouldBe((int)DocumentStatus.Rejected);
        });
    }

    [Fact]
    public async Task ApproveAsync_ManagerLinkedToActingUser_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var claimRepository = GetRequiredService<IRepository<ExpenseClaim, Guid>>();
            var expenseClaimAppService = GetRequiredService<IExpenseClaimAppService>();
            var currentUser = GetRequiredService<ICurrentUser>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Expense Approver Test Co 3"), autoSave: true);
            var manager = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-EMGR-3", "Manager") { UserId = currentUser.Id }, autoSave: true);
            var claimant = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-ECLM-3", "Claimant") { ReportsToEmployeeId = manager.Id }, autoSave: true);

            var claim = new ExpenseClaim(Guid.NewGuid(), company.Id, claimant.Id, DateTime.Today);
            claim.AddExpense(DateTime.Today, "Taxi", 50m);
            await claimRepository.InsertAsync(claim, autoSave: true);

            var result = await expenseClaimAppService.ApproveAsync(claim.Id);
            result.Status.ShouldBe((int)DocumentStatus.Approved);
        });
    }
}
