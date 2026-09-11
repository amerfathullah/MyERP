using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Projects;

public abstract class TimesheetCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_EmployeeFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepo = GetRequiredService<IRepository<Employee, Guid>>();
            var timesheetAppService = GetRequiredService<ITimesheetAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "TS Co Owner 1"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "TS Co Other 1"), autoSave: true);

            var otherEmployee = await employeeRepo.InsertAsync(new Employee(Guid.NewGuid(), otherCompany.Id, "EMP-OTHER-1", "Other Emp"), autoSave: true);

            var now = DateTime.UtcNow;
            await Should.ThrowAsync<BusinessException>(() =>
                timesheetAppService.CreateAsync(new CreateTimesheetDto
                {
                    CompanyId = ownerCompany.Id,
                    EmployeeId = otherEmployee.Id,
                    StartDate = now.Date,
                    EndDate = now.Date.AddDays(1),
                    Details = new List<CreateTimesheetDetailDto>
                    {
                        new()
                        {
                            ActivityType = "Execution",
                            FromTime = now,
                            ToTime = now.AddHours(2),
                            Hours = 2
                        }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_ProjectFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepo = GetRequiredService<IRepository<Employee, Guid>>();
            var projectRepo = GetRequiredService<IRepository<Project, Guid>>();
            var timesheetAppService = GetRequiredService<ITimesheetAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "TS Co Owner 2"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "TS Co Other 2"), autoSave: true);

            var ownerEmployee = await employeeRepo.InsertAsync(new Employee(Guid.NewGuid(), ownerCompany.Id, "EMP-OWNER-2", "Owner Emp"), autoSave: true);
            var otherProject = await projectRepo.InsertAsync(new Project(Guid.NewGuid(), otherCompany.Id, "PROJ-OTHER-2", "Other Proj"), autoSave: true);

            var now = DateTime.UtcNow;
            await Should.ThrowAsync<BusinessException>(() =>
                timesheetAppService.CreateAsync(new CreateTimesheetDto
                {
                    CompanyId = ownerCompany.Id,
                    EmployeeId = ownerEmployee.Id,
                    StartDate = now.Date,
                    EndDate = now.Date.AddDays(1),
                    Details = new List<CreateTimesheetDetailDto>
                    {
                        new()
                        {
                            ActivityType = "Execution",
                            FromTime = now,
                            ToTime = now.AddHours(2),
                            Hours = 2,
                            ProjectId = otherProject.Id
                        }
                    }
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_EmployeeFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepo = GetRequiredService<IRepository<Employee, Guid>>();
            var timesheetAppService = GetRequiredService<ITimesheetAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "TS Co Owner 3"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "TS Co Other 3"), autoSave: true);

            var ownerEmployee = await employeeRepo.InsertAsync(new Employee(Guid.NewGuid(), ownerCompany.Id, "EMP-OWNER-3", "Owner Emp 3"), autoSave: true);
            var otherEmployee = await employeeRepo.InsertAsync(new Employee(Guid.NewGuid(), otherCompany.Id, "EMP-OTHER-3", "Other Emp 3"), autoSave: true);

            var now = DateTime.UtcNow;
            var ts = await timesheetAppService.CreateAsync(new CreateTimesheetDto
            {
                CompanyId = ownerCompany.Id,
                EmployeeId = ownerEmployee.Id,
                StartDate = now.Date,
                EndDate = now.Date.AddDays(1),
                Details = new List<CreateTimesheetDetailDto>
                {
                    new()
                    {
                        ActivityType = "Planning",
                        FromTime = now,
                        ToTime = now.AddHours(1),
                        Hours = 1
                    }
                }
            });

            await Should.ThrowAsync<BusinessException>(() =>
                timesheetAppService.UpdateAsync(ts.Id, new CreateTimesheetDto
                {
                    CompanyId = ownerCompany.Id,
                    EmployeeId = otherEmployee.Id,
                    StartDate = now.Date,
                    EndDate = now.Date.AddDays(1),
                    Details = new List<CreateTimesheetDetailDto>
                    {
                        new()
                        {
                            ActivityType = "Planning",
                            FromTime = now,
                            ToTime = now.AddHours(1),
                            Hours = 1
                        }
                    }
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_ProjectFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepo = GetRequiredService<IRepository<Employee, Guid>>();
            var projectRepo = GetRequiredService<IRepository<Project, Guid>>();
            var timesheetAppService = GetRequiredService<ITimesheetAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "TS Co Owner 4"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "TS Co Other 4"), autoSave: true);

            var ownerEmployee = await employeeRepo.InsertAsync(new Employee(Guid.NewGuid(), ownerCompany.Id, "EMP-OWNER-4", "Owner Emp 4"), autoSave: true);
            var otherProject = await projectRepo.InsertAsync(new Project(Guid.NewGuid(), otherCompany.Id, "PROJ-OTHER-4", "Other Proj 4"), autoSave: true);

            var now = DateTime.UtcNow;
            var ts = await timesheetAppService.CreateAsync(new CreateTimesheetDto
            {
                CompanyId = ownerCompany.Id,
                EmployeeId = ownerEmployee.Id,
                StartDate = now.Date,
                EndDate = now.Date.AddDays(1),
                Details = new List<CreateTimesheetDetailDto>
                {
                    new()
                    {
                        ActivityType = "Testing",
                        FromTime = now,
                        ToTime = now.AddHours(1),
                        Hours = 1
                    }
                }
            });

            await Should.ThrowAsync<BusinessException>(() =>
                timesheetAppService.UpdateAsync(ts.Id, new CreateTimesheetDto
                {
                    CompanyId = ownerCompany.Id,
                    EmployeeId = ownerEmployee.Id,
                    StartDate = now.Date,
                    EndDate = now.Date.AddDays(1),
                    Details = new List<CreateTimesheetDetailDto>
                    {
                        new()
                        {
                            ActivityType = "Testing",
                            FromTime = now,
                            ToTime = now.AddHours(1),
                            Hours = 1,
                            ProjectId = otherProject.Id
                        }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateInvoiceFromTimesheetsAsync_CustomerFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var timesheetAppService = GetRequiredService<ITimesheetAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "TS Co Owner 5"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "TS Co Other 5"), autoSave: true);

            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "TS Cust Other 5"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                timesheetAppService.CreateInvoiceFromTimesheetsAsync(new CreateTimesheetInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = otherCustomer.Id
                }));
        });
    }

    [Fact]
    public async Task CreateInvoiceFromTimesheetsAsync_ProjectFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var projectRepo = GetRequiredService<IRepository<Project, Guid>>();
            var timesheetAppService = GetRequiredService<ITimesheetAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "TS Co Owner 6"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "TS Co Other 6"), autoSave: true);

            var ownerCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "TS Cust Owner 6"), autoSave: true);
            var otherProject = await projectRepo.InsertAsync(new Project(Guid.NewGuid(), otherCompany.Id, "PROJ-OTHER-6", "Other Proj 6"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                timesheetAppService.CreateInvoiceFromTimesheetsAsync(new CreateTimesheetInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = ownerCustomer.Id,
                    ProjectId = otherProject.Id
                }));
        });
    }
}
