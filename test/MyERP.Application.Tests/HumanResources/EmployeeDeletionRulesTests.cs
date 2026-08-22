using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.HumanResources;

/// <summary>
/// Regression coverage for a gap found while surveying HumanResources DomainServices for unwired
/// methods: EmployeeLifecycleManager.CheckDeletionRulesAsync had zero callers anywhere — its own
/// class doc comment states "Cannot delete with linked transactions (leave, salary, attendance)",
/// but EmployeeAppService.DeleteAsync deleted unconditionally.
/// </summary>
public abstract class EmployeeDeletionRulesTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task DeleteAsync_WithLinkedAttendance_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var attendanceRepository = GetRequiredService<IRepository<Attendance, Guid>>();
            var employeeAppService = GetRequiredService<IEmployeeAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Employee Delete Test Co 1"), autoSave: true);
            var employee = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-DEL-1", "Test"), autoSave: true);
            await attendanceRepository.InsertAsync(
                new Attendance(Guid.NewGuid(), company.Id, employee.Id, DateTime.Today, AttendanceStatus.Present), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() => employeeAppService.DeleteAsync(employee.Id));
        });
    }

    [Fact]
    public async Task DeleteAsync_WithLinkedLeaveApplication_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var leaveRepository = GetRequiredService<IRepository<LeaveApplication, Guid>>();
            var employeeAppService = GetRequiredService<IEmployeeAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Employee Delete Test Co 2"), autoSave: true);
            var employee = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-DEL-2", "Test"), autoSave: true);
            await leaveRepository.InsertAsync(
                new LeaveApplication(Guid.NewGuid(), company.Id, employee.Id, Guid.NewGuid(),
                    DateTime.Today, DateTime.Today.AddDays(1), 2m), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() => employeeAppService.DeleteAsync(employee.Id));
        });
    }

    [Fact]
    public async Task DeleteAsync_NoLinkedRecords_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var employeeAppService = GetRequiredService<IEmployeeAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Employee Delete Test Co 3"), autoSave: true);
            var employee = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-DEL-3", "Test"), autoSave: true);

            await employeeAppService.DeleteAsync(employee.Id);

            (await employeeRepository.FindAsync(employee.Id)).ShouldBeNull();
        });
    }
}
