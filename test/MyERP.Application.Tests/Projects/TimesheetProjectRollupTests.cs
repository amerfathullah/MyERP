using System;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Projects;

/// <summary>
/// Regression coverage for round-96's fix: Project.TotalCostingAmount/TotalBillingAmount/
/// TotalBilledAmount were declared and bound in the Project Detail page's KPI cards, but written
/// nowhere outside EF scaffolding — no MyERP equivalent of ERPNext's project.py::update_costing().
/// TimesheetAppService.SubmitAsync/CancelAsync/CreateInvoiceFromTimesheetsAsync now roll each
/// timesheet detail's costing/billing/billed amount up onto its linked Project.
/// </summary>
public abstract class TimesheetProjectRollupTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubmitAsync_RollsCostingAndBillingUpToProject()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepository = GetRequiredService<IRepository<Project, Guid>>();
            var timesheetRepository = GetRequiredService<IRepository<Timesheet, Guid>>();
            var timesheetAppService = GetRequiredService<ITimesheetAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Timesheet Rollup Co"), autoSave: true);
            var project = await projectRepository.InsertAsync(
                new Project(Guid.NewGuid(), company.Id, "PROJ-001", "Rollup Test Project"), autoSave: true);

            var ts = new Timesheet(Guid.NewGuid(), company.Id, Guid.NewGuid(), DateTime.Today, DateTime.Today) { EmployeeName = "Employee" };
            ts.AddDetail(new TimesheetDetail(Guid.NewGuid(), ts.Id, "Development",
                DateTime.Today, DateTime.Today.AddHours(8), 8m)
            {
                ProjectId = project.Id, IsBillable = true, BillingRate = 100m, CostingRate = 50m,
            });
            ts.AddDetail(new TimesheetDetail(Guid.NewGuid(), ts.Id, "Internal Meeting",
                DateTime.Today, DateTime.Today.AddHours(2), 2m)
            {
                ProjectId = project.Id, IsBillable = false, CostingRate = 50m,
            });
            await timesheetRepository.InsertAsync(ts, autoSave: true);

            await timesheetAppService.SubmitAsync(ts.Id);

            var updatedProject = await projectRepository.GetAsync(project.Id);
            updatedProject.TotalCostingAmount.ShouldBe(500m); // (8+2)h × 50 costing rate
            updatedProject.TotalBillingAmount.ShouldBe(800m); // only the billable 8h × 100

            // CancelAsync must reverse the rollup it applied on Submit.
            await timesheetAppService.CancelAsync(ts.Id);
            var afterCancel = await projectRepository.GetAsync(project.Id);
            afterCancel.TotalCostingAmount.ShouldBe(0m);
            afterCancel.TotalBillingAmount.ShouldBe(0m);
        });
    }

    [Fact]
    public async Task CreateInvoiceFromTimesheetsAsync_RollsBilledAmountUpToProject()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepository = GetRequiredService<IRepository<Project, Guid>>();
            var timesheetRepository = GetRequiredService<IRepository<Timesheet, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var timesheetAppService = GetRequiredService<ITimesheetAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Timesheet Billing Co"), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SI Series", "SalesInvoice", "TSSI-"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Test Customer"), autoSave: true);
            var project = await projectRepository.InsertAsync(
                new Project(Guid.NewGuid(), company.Id, "PROJ-002", "Billing Test Project"), autoSave: true);

            var ts = new Timesheet(Guid.NewGuid(), company.Id, Guid.NewGuid(), DateTime.Today, DateTime.Today) { EmployeeName = "Employee" };
            ts.AddDetail(new TimesheetDetail(Guid.NewGuid(), ts.Id, "Consulting",
                DateTime.Today, DateTime.Today.AddHours(5), 5m)
            {
                ProjectId = project.Id, IsBillable = true, BillingRate = 120m, CostingRate = 60m,
            });
            await timesheetRepository.InsertAsync(ts, autoSave: true);
            await timesheetAppService.SubmitAsync(ts.Id);

            var result = await timesheetAppService.CreateInvoiceFromTimesheetsAsync(new CreateTimesheetInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                ProjectId = project.Id,
            });

            result.InvoiceId.ShouldNotBe(Guid.Empty);

            var updatedProject = await projectRepository.GetAsync(project.Id);
            updatedProject.TotalBilledAmount.ShouldBe(600m); // 5h × 120
        });
    }
}
