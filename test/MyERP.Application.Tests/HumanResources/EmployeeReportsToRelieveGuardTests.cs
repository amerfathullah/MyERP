using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.HumanResources;

/// <summary>
/// Regression coverage for a gap found comparing against ERPNext's employee.py
/// validate_status(): an employee cannot be relieved (Resigned/Terminated) while other
/// active employees still report to them — MyERP's Employee entity had no manager-hierarchy
/// field at all (no ReportsTo), so this guard was structurally impossible, not just unwired.
/// </summary>
public abstract class EmployeeReportsToRelieveGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ChangeStatusAsync_ManagerWithActiveSubordinate_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var employeeAppService = GetRequiredService<IEmployeeAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Reports-To Guard Test Co 1"), autoSave: true);
            var manager = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-MGR-1", "Manager"), autoSave: true);
            var report = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-RPT-1", "Report") { ReportsToEmployeeId = manager.Id }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                employeeAppService.ChangeStatusAsync(manager.Id, new ChangeEmployeeStatusDto
                {
                    Status = EmploymentStatus.Terminated,
                }));
            ex.Code.ShouldBe("MyERP:HR:005");
        });
    }

    [Fact]
    public async Task ChangeStatusAsync_ManagerWithOnlyRelievedSubordinates_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var employeeAppService = GetRequiredService<IEmployeeAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Reports-To Guard Test Co 2"), autoSave: true);
            var manager = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-MGR-2", "Manager"), autoSave: true);
            var report = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-RPT-2", "Report")
                {
                    ReportsToEmployeeId = manager.Id,
                    Status = EmploymentStatus.Terminated,
                }, autoSave: true);

            var result = await employeeAppService.ChangeStatusAsync(manager.Id, new ChangeEmployeeStatusDto
            {
                Status = EmploymentStatus.Terminated,
            });

            result.Status.ShouldBe(EmploymentStatus.Terminated.ToString());
        });
    }

    [Fact]
    public async Task ChangeStatusAsync_NoSubordinates_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var employeeAppService = GetRequiredService<IEmployeeAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Reports-To Guard Test Co 3"), autoSave: true);
            var employee = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-SOLO-1", "Solo"), autoSave: true);

            var result = await employeeAppService.ChangeStatusAsync(employee.Id, new ChangeEmployeeStatusDto
            {
                Status = EmploymentStatus.Terminated,
            });

            result.Status.ShouldBe(EmploymentStatus.Terminated.ToString());
        });
    }

    [Fact]
    public async Task UpdateAsync_ReportsToSelf_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var employeeAppService = GetRequiredService<IEmployeeAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Reports-To Guard Test Co 4"), autoSave: true);
            var employee = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-SELF-1", "Self"), autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                employeeAppService.UpdateAsync(employee.Id, new CreateUpdateEmployeeDto
                {
                    CompanyId = company.Id,
                    FirstName = "Self",
                    ReportsToEmployeeId = employee.Id,
                }));
            ex.Code.ShouldBe("MyERP:HR:006");
        });
    }

    [Fact]
    public async Task ChangeStatusAsync_Resigned_DisablesLinkedSalesPersonWithoutOpenSales()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var salesPersonRepository = GetRequiredService<IRepository<SalesPerson, Guid>>();
            var employeeAppService = GetRequiredService<IEmployeeAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SP Disable Co"), autoSave: true);
            var employee = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-SP-1", "Alice"), autoSave: true);
            var sp = await salesPersonRepository.InsertAsync(
                new SalesPerson(Guid.NewGuid(), "Alice SP") { EmployeeId = employee.Id, IsEnabled = true }, autoSave: true);

            var result = await employeeAppService.ChangeStatusAsync(employee.Id, new ChangeEmployeeStatusDto
            {
                Status = EmploymentStatus.Resigned,
                DateOfResignation = DateTime.UtcNow.Date,
            });

            result.Warnings.ShouldBeEmpty();
            var updatedSp = await salesPersonRepository.GetAsync(sp.Id);
            updatedSp.IsEnabled.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task ChangeStatusAsync_Resigned_KeepsSalesPersonWithOpenSalesOrderEnabledAndWarns()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var salesPersonRepository = GetRequiredService<IRepository<SalesPerson, Guid>>();
            var salesOrderRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var salesTeamRepository = GetRequiredService<IRepository<SalesTeamEntry, Guid>>();
            var employeeAppService = GetRequiredService<IEmployeeAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SP Keep Co"), autoSave: true);
            var employee = await employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-SP-2", "Bob"), autoSave: true);
            var sp = await salesPersonRepository.InsertAsync(
                new SalesPerson(Guid.NewGuid(), "Bob SP") { EmployeeId = employee.Id, IsEnabled = true }, autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, Guid.NewGuid(), "SO-OPEN-1", DateTime.UtcNow.Date);
            so.AddItem(Guid.NewGuid(), "Widget", 1m, 100m, 0m);
            so.Submit();
            await salesOrderRepository.InsertAsync(so, autoSave: true);
            await salesTeamRepository.InsertAsync(
                new SalesTeamEntry(Guid.NewGuid(), sp.Id, "SalesOrder", so.Id, 100m, 100m, 5m), autoSave: true);

            var result = await employeeAppService.ChangeStatusAsync(employee.Id, new ChangeEmployeeStatusDto
            {
                Status = EmploymentStatus.Resigned,
                DateOfResignation = DateTime.UtcNow.Date,
            });

            result.Status.ShouldBe(EmploymentStatus.Resigned.ToString());
            result.Warnings.Count.ShouldBe(1);
            result.Warnings[0].ShouldContain("SO-OPEN-1");

            var updatedSp = await salesPersonRepository.GetAsync(sp.Id);
            updatedSp.IsEnabled.ShouldBeTrue();
        });
    }
}
