using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Projects;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: timesheet.py
/// validate_overlap_for()/get_overlap_for() blocks a time log that overlaps with another time log
/// for the *same employee* on a *different*, non-cancelled Timesheet. TimesheetAppService only ever
/// checked overlap *within* one document (gotcha #2801) — the entity's own doc-comment claims
/// "supports overlap validation," but the cross-document half was never implemented, so an employee
/// could log the same hours twice across two timesheets, double-counting billable/costing amounts
/// and the Project rollup this session wired up in round-96.
/// </summary>
public abstract class TimesheetCrossDocumentOverlapTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_OverlapsWithAnotherTimesheetSameEmployee_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var timesheetAppService = GetRequiredService<ITimesheetAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Timesheet Overlap Co"), autoSave: true);
            var employee = await employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-TS-1", "Overlap Employee"), autoSave: true);

            var day = DateTime.Today;
            await timesheetAppService.CreateAsync(new CreateTimesheetDto
            {
                CompanyId = company.Id,
                EmployeeId = employee.Id,
                StartDate = day,
                EndDate = day,
                Details = new List<CreateTimesheetDetailDto>
                {
                    new() { ActivityType = "Development", FromTime = day.AddHours(9), ToTime = day.AddHours(11), Hours = 2m }
                }
            });

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                timesheetAppService.CreateAsync(new CreateTimesheetDto
                {
                    CompanyId = company.Id,
                    EmployeeId = employee.Id,
                    StartDate = day,
                    EndDate = day,
                    Details = new List<CreateTimesheetDetailDto>
                    {
                        new() { ActivityType = "Consulting", FromTime = day.AddHours(10), ToTime = day.AddHours(12), Hours = 2m }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SameTimeRangeDifferentEmployee_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var timesheetAppService = GetRequiredService<ITimesheetAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Timesheet No Overlap Co"), autoSave: true);
            var employee1 = await employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-TS-2", "No Overlap Employee 1"), autoSave: true);
            var employee2 = await employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-TS-3", "No Overlap Employee 2"), autoSave: true);

            var day = DateTime.Today;
            await timesheetAppService.CreateAsync(new CreateTimesheetDto
            {
                CompanyId = company.Id,
                EmployeeId = employee1.Id,
                StartDate = day,
                EndDate = day,
                Details = new List<CreateTimesheetDetailDto>
                {
                    new() { ActivityType = "Development", FromTime = day.AddHours(9), ToTime = day.AddHours(11), Hours = 2m }
                }
            });

            var dto = await timesheetAppService.CreateAsync(new CreateTimesheetDto
            {
                CompanyId = company.Id,
                EmployeeId = employee2.Id,
                StartDate = day,
                EndDate = day,
                Details = new List<CreateTimesheetDetailDto>
                {
                    new() { ActivityType = "Development", FromTime = day.AddHours(9), ToTime = day.AddHours(11), Hours = 2m }
                }
            });

            dto.Id.ShouldNotBe(Guid.Empty);
        });
    }

    [Fact]
    public async Task UpdateAsync_SameDocumentUnchangedTimes_DoesNotFalsePositive()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var timesheetAppService = GetRequiredService<ITimesheetAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Timesheet Update Co"), autoSave: true);
            var employee = await employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-TS-4", "Update Employee"), autoSave: true);

            var day = DateTime.Today;
            var created = await timesheetAppService.CreateAsync(new CreateTimesheetDto
            {
                CompanyId = company.Id,
                EmployeeId = employee.Id,
                StartDate = day,
                EndDate = day,
                Note = "v1",
                Details = new List<CreateTimesheetDetailDto>
                {
                    new() { ActivityType = "Development", FromTime = day.AddHours(9), ToTime = day.AddHours(11), Hours = 2m }
                }
            });

            // Re-saving the exact same time range on the same document must not trip the
            // cross-document check against itself now that it's excluded by id.
            var updated = await timesheetAppService.UpdateAsync(created.Id, new CreateTimesheetDto
            {
                CompanyId = company.Id,
                EmployeeId = employee.Id,
                StartDate = day,
                EndDate = day,
                Note = "v2",
                Details = new List<CreateTimesheetDetailDto>
                {
                    new() { ActivityType = "Development", FromTime = day.AddHours(9), ToTime = day.AddHours(11), Hours = 2m }
                }
            });

            updated.Id.ShouldBe(created.Id);
        });
    }
}
