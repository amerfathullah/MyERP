using System;
using System.Linq;
using System.Threading.Tasks;
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
/// Regression coverage for a gap found comparing against ERPNext's leave_application.py: only the
/// designated leave_approver (or an HR-manager role) may approve/reject. MyERP's LeaveApplication
/// already had a LeaveApproverId field, but (a) the Angular create form never set it — always
/// null — and (b) ApproveAsync/RejectAsync only gated on the generic Employees.Edit permission,
/// never checked the acting user was actually the named approver. Root cause traced further:
/// Employee had no link to an ABP Identity user account at all, so "is CurrentUser this
/// employee?" couldn't even be resolved (see [[project_myerp_migration_2026_08_27e]]).
///
/// Fixed via Employee.UserId (links to IdentityUser) + Employee.ReportsToEmployeeId (added the
/// prior round) to auto-derive the default approver, matching ERPNext's own default-from-
/// reports_to behavior, with enforcement staying a no-op until an org actually links both fields
/// (incremental adoption — never silently locks out orgs that haven't set this up).
/// </summary>
public abstract class LeaveApproverEnforcementTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ApplyAsync_NoExplicitApprover_DefaultsFromReportsTo()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var leaveTypeRepository = GetRequiredService<IRepository<LeaveType, Guid>>();
            var leaveAppService = GetRequiredService<ILeaveAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Leave Approver Test Co 1"), autoSave: true);
            var manager = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-LMGR-1", "Manager"), autoSave: true);
            var applicant = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-LAPP-1", "Applicant") { ReportsToEmployeeId = manager.Id }, autoSave: true);
            var leaveType = await leaveTypeRepository.InsertAsync(
                new LeaveType(Guid.NewGuid(), "Annual Leave", 14m), autoSave: true);

            var result = await leaveAppService.ApplyAsync(new CreateLeaveApplicationDto
            {
                CompanyId = company.Id,
                EmployeeId = applicant.Id,
                LeaveTypeId = leaveType.Id,
                FromDate = DateTime.Today,
                ToDate = DateTime.Today,
                TotalLeaveDays = 1m,
            });

            result.LeaveApproverId.ShouldBe(manager.Id);
        });
    }

    [Fact]
    public async Task RejectAsync_ApproverHasLinkedUser_WrongActingUser_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var leaveTypeRepository = GetRequiredService<IRepository<LeaveType, Guid>>();
            var leaveRepository = GetRequiredService<IRepository<LeaveApplication, Guid>>();
            var leaveAppService = GetRequiredService<ILeaveAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Leave Approver Test Co 2"), autoSave: true);
            // A different, real user id — never the acting test user — so the mismatch is real.
            var someoneElsesUserId = Guid.NewGuid();
            var manager = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-LMGR-2", "Manager") { UserId = someoneElsesUserId }, autoSave: true);
            var applicant = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-LAPP-2", "Applicant"), autoSave: true);
            var leaveType = await leaveTypeRepository.InsertAsync(
                new LeaveType(Guid.NewGuid(), "Annual Leave", 14m), autoSave: true);

            var leave = new LeaveApplication(Guid.NewGuid(), company.Id, applicant.Id, leaveType.Id,
                DateTime.Today, DateTime.Today, 1m) { LeaveApproverId = manager.Id };
            await leaveRepository.InsertAsync(leave, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() => leaveAppService.RejectAsync(leave.Id));
            ex.Code.ShouldBe("MyERP:HR:007");
        });
    }

    [Fact]
    public async Task RejectAsync_ApproverHasNoLinkedUser_FallsBackToPermissionOnlyGate_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var leaveTypeRepository = GetRequiredService<IRepository<LeaveType, Guid>>();
            var leaveRepository = GetRequiredService<IRepository<LeaveApplication, Guid>>();
            var leaveAppService = GetRequiredService<ILeaveAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Leave Approver Test Co 3"), autoSave: true);
            var manager = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-LMGR-3", "Manager"), autoSave: true); // no UserId linked yet
            var applicant = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-LAPP-3", "Applicant"), autoSave: true);
            var leaveType = await leaveTypeRepository.InsertAsync(
                new LeaveType(Guid.NewGuid(), "Annual Leave", 14m), autoSave: true);

            var leave = new LeaveApplication(Guid.NewGuid(), company.Id, applicant.Id, leaveType.Id,
                DateTime.Today, DateTime.Today, 1m) { LeaveApproverId = manager.Id };
            await leaveRepository.InsertAsync(leave, autoSave: true);

            var result = await leaveAppService.RejectAsync(leave.Id);
            result.Status.ShouldBe(LeaveApplicationStatus.Rejected);
        });
    }

    [Fact]
    public async Task ApproveAsync_ApproverLinkedToActingUser_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var leaveTypeRepository = GetRequiredService<IRepository<LeaveType, Guid>>();
            var allocationRepository = GetRequiredService<IRepository<LeaveAllocation, Guid>>();
            var leaveRepository = GetRequiredService<IRepository<LeaveApplication, Guid>>();
            var leaveAppService = GetRequiredService<ILeaveAppService>();
            var currentUser = GetRequiredService<ICurrentUser>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Leave Approver Test Co 4"), autoSave: true);
            var manager = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-LMGR-4", "Manager") { UserId = currentUser.Id }, autoSave: true);
            var applicant = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-LAPP-4", "Applicant"), autoSave: true);
            var leaveType = await leaveTypeRepository.InsertAsync(
                new LeaveType(Guid.NewGuid(), "Annual Leave", 14m), autoSave: true);
            await allocationRepository.InsertAsync(
                new LeaveAllocation(Guid.NewGuid(), company.Id, applicant.Id, leaveType.Id,
                    DateTime.Today.AddMonths(-1), DateTime.Today.AddMonths(11), 14m), autoSave: true);

            var leave = new LeaveApplication(Guid.NewGuid(), company.Id, applicant.Id, leaveType.Id,
                DateTime.Today, DateTime.Today, 1m) { LeaveApproverId = manager.Id };
            await leaveRepository.InsertAsync(leave, autoSave: true);

            var result = await leaveAppService.ApproveAsync(leave.Id);
            result.Status.ShouldBe(LeaveApplicationStatus.Approved);
        });
    }

    [Fact]
    public async Task UpdateAsync_UserAlreadyLinkedToAnotherEmployee_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var employeeAppService = GetRequiredService<IEmployeeAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Leave Approver Test Co 5"), autoSave: true);
            var sharedUserId = Guid.NewGuid();
            var linked = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-LINKED-1", "Linked") { UserId = sharedUserId }, autoSave: true);
            var other = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-OTHER-1", "Other"), autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                employeeAppService.UpdateAsync(other.Id, new CreateUpdateEmployeeDto
                {
                    CompanyId = company.Id,
                    FirstName = "Other",
                    UserId = sharedUserId,
                }));
            ex.Code.ShouldBe("MyERP:HR:008");
        });
    }
}
