using System;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for ERPNext PR #59191 / commit db6e089109:
/// Validates that warehouse accounts belong to the selected company.
/// </summary>
public abstract class WarehouseAccountValidationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateWarehouse_AccountFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var warehouseAppService = GetRequiredService<IWarehouseAppService>();

            var company1 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "WH Co 1"), autoSave: true);
            var company2 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "WH Co 2"), autoSave: true);

            var account2 = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company2.Id, "1310", "Stock In Hand - Co2", AccountType.Asset) { AccountSubType = AccountSubType.Stock }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                warehouseAppService.CreateAsync(new CreateUpdateWarehouseDto
                {
                    CompanyId = company1.Id,
                    Name = "Test WH Cross Co",
                    DefaultAccountId = account2.Id
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task UpdateWarehouse_AccountFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var warehouseAppService = GetRequiredService<IWarehouseAppService>();

            var company1 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "WH Co 1 Upd"), autoSave: true);
            var company2 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "WH Co 2 Upd"), autoSave: true);

            var created = await warehouseAppService.CreateAsync(new CreateUpdateWarehouseDto
            {
                CompanyId = company1.Id,
                Name = "Test WH Initial",
            });

            var account2 = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company2.Id, "1310", "Stock In Hand - Co2 Upd", AccountType.Asset) { AccountSubType = AccountSubType.Stock }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                warehouseAppService.UpdateAsync(created.Id, new CreateUpdateWarehouseDto
                {
                    CompanyId = company1.Id,
                    Name = "Test WH Updated",
                    DefaultAccountId = account2.Id
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task SaveWarehouseAccount_AccountFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Entities.Warehouse, Guid>>();
            var whAccountAppService = GetRequiredService<IWarehouseAccountAppService>();

            var company1 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "WH Acc Co 1"), autoSave: true);
            var company2 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "WH Acc Co 2"), autoSave: true);

            var wh = await warehouseRepo.InsertAsync(new Entities.Warehouse(Guid.NewGuid(), company1.Id, "WH 1"), autoSave: true);
            var account2 = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company2.Id, "1310", "Stock In Hand - Cross", AccountType.Asset) { AccountSubType = AccountSubType.Stock }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                whAccountAppService.SaveAsync(new CreateWarehouseAccountDto
                {
                    WarehouseId = wh.Id,
                    CompanyId = company1.Id,
                    AccountId = account2.Id
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task CreateWarehouse_MatchingCompanyAccount_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var warehouseAppService = GetRequiredService<IWarehouseAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "WH Co Match"), autoSave: true);
            var account = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1310", "Stock In Hand - Match", AccountType.Asset) { AccountSubType = AccountSubType.Stock }, autoSave: true);

            var result = await warehouseAppService.CreateAsync(new CreateUpdateWarehouseDto
            {
                CompanyId = company.Id,
                Name = "Test WH Matching Account",
                DefaultAccountId = account.Id
            });

            result.ShouldNotBeNull();
            result.DefaultAccountId.ShouldBe(account.Id);
        });
    }
}
