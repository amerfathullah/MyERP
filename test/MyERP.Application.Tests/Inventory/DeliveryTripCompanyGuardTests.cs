using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for a real gap found via a CompanyRestrictionValidationService spot-check
/// sweep: DeliveryTripAppService.CreateAsync accepted a stop's optional CustomerId with no check
/// that the customer belonged to the trip's Company.
/// </summary>
public abstract class DeliveryTripCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_StopCustomerFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var deliveryTripAppService = GetRequiredService<IDeliveryTripAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "DT Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "DT Guard Other Co"), autoSave: true);

            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "DT Guard Customer"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                deliveryTripAppService.CreateAsync(new CreateUpdateDeliveryTripDto
                {
                    CompanyId = ownerCompany.Id,
                    TripNumber = "DT-GUARD-001",
                    Driver = "Test Driver",
                    Vehicle = "Test Vehicle",
                    DepartureTime = DateTime.UtcNow,
                    DeliveryStops = new List<CreateUpdateDeliveryStopDto>
                    {
                        new() { CustomerId = customer.Id, Address = "123 Cross-Co Lane" }
                    }
                }));
        });
    }
}
