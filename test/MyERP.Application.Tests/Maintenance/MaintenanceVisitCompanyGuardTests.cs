using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Maintenance;

/// <summary>
/// Regression coverage for a real gap found via a CompanyRestrictionValidationService spot-check
/// sweep: MaintenanceVisitAppService.CreateAsync only ran purpose Items through
/// ItemTransactionValidationService (IsActive check only) and never wired
/// CompanyRestrictionValidationService for Customer/Items. Note: MyERP.Assets.MaintenanceAppService
/// has a parallel, duplicate CreateVisitAsync (round-91 found this duplication) with the identical
/// gap — fixed alongside this one, see [[project_myerp_migration_2026_09_10o]].
/// </summary>
public abstract class MaintenanceVisitCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_CustomerFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var visitAppService = GetRequiredService<IMaintenanceVisitAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MV Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MV Guard Other Co"), autoSave: true);

            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "MV Guard Customer"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                visitAppService.CreateAsync(new CreateMaintenanceVisitDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    VisitDate = DateTime.UtcNow,
                }));
        });
    }
}
