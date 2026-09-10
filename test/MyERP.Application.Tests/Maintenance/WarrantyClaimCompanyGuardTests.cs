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
/// Regression coverage for a real gap found via ERPNext validate() parity: WarrantyClaimAppService
/// was the one Maintenance/Support AppService that never wired CompanyRestrictionValidationService
/// in — every other cross-company-sensitive AppService swept this session (Quotation, RFQ, Pick
/// List, Stock Reconciliation, etc.) got this fix, but Warranty Claim's Customer/Item could belong
/// to a different Company entirely with no check ever firing.
/// </summary>
public abstract class WarrantyClaimCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_CustomerFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warrantyClaimAppService = GetRequiredService<IWarrantyClaimAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WC Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WC Guard Other Co"), autoSave: true);

            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "WC-ITEM-001", "Warranty Guard Item", ItemType.Goods), autoSave: true);
            var customer = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), otherCompany.Id, "Warranty Guard Cross-Co Customer"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                warrantyClaimAppService.CreateAsync(new CreateWarrantyClaimDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    ItemId = item.Id,
                    ComplaintDate = DateTime.UtcNow,
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SameCompanyCustomerAndItem_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warrantyClaimAppService = GetRequiredService<IWarrantyClaimAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WC Guard Happy Co"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "WC-ITEM-002", "Warranty Happy Item", ItemType.Goods), autoSave: true);
            var customer = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Warranty Happy Customer"), autoSave: true);

            var dto = await warrantyClaimAppService.CreateAsync(new CreateWarrantyClaimDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                ItemId = item.Id,
                ComplaintDate = DateTime.UtcNow,
            });

            dto.CustomerId.ShouldBe(customer.Id);
        });
    }
}
