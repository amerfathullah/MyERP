using System;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

public abstract class SupplierPartyAccountGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_PayableAccountFromOtherCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var owner = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Supp Acct Owner Co 1"), autoSave: true);
            var other = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Supp Acct Other Co 1"), autoSave: true);
            var foreign = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), other.Id, "2100-X1", "Foreign Payable", AccountType.Liability), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                GetRequiredService<ISupplierAppService>().CreateAsync(new CreateUpdateSupplierDto
                {
                    CompanyId = owner.Id,
                    Name = "Supp Acct Supplier 1",
                    IsActive = true,
                    DefaultPayableAccountId = foreign.Id,
                }));
        });
    }
}
