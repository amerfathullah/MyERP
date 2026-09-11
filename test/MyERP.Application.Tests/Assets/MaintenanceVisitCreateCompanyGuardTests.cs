using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

/// <summary>
/// Regression coverage for the duplicate MaintenanceAppService.CreateVisitAsync (round-91 flagged
/// this as a duplicate of MyERP.Maintenance.MaintenanceVisitAppService.CreateAsync — both are live
/// public endpoints, so both need the fix): it never wired CompanyRestrictionValidationService for
/// the optional Customer/Items either. See [[project_myerp_migration_2026_09_10o]].
/// </summary>
public abstract class MaintenanceVisitCreateCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateVisitAsync_CustomerFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var maintenanceAppService = GetRequiredService<IMaintenanceAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MV2 Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MV2 Guard Other Co"), autoSave: true);

            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "MV2 Guard Customer"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                maintenanceAppService.CreateVisitAsync(new CreateMaintenanceVisitDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    VisitDate = DateTime.UtcNow,
                    MaintenanceType = "Scheduled",
                }));
        });
    }

    [Fact]
    public async Task CreateVisitAsync_ScheduleFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var scheduleRepository = GetRequiredService<IRepository<Maintenance.Entities.MaintenanceSchedule, Guid>>();
            var maintenanceAppService = GetRequiredService<IMaintenanceAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MV2 Sched Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MV2 Sched Other Co"), autoSave: true);

            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "MV2 Sched Customer"), autoSave: true);

            var otherSchedule = new Maintenance.Entities.MaintenanceSchedule(
                Guid.NewGuid(), otherCompany.Id, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), "Monthly");
            await scheduleRepository.InsertAsync(otherSchedule, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                maintenanceAppService.CreateVisitAsync(new CreateMaintenanceVisitDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    MaintenanceScheduleId = otherSchedule.Id,
                    VisitDate = DateTime.UtcNow,
                    MaintenanceType = "Scheduled",
                }));
        });
    }
}
