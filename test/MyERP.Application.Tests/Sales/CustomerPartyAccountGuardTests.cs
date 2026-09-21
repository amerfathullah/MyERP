using System;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class CustomerPartyAccountGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_PayableAccountFromOtherCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var owner = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Cust Acct Owner Co 1"), autoSave: true);
            var other = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Cust Acct Other Co 1"), autoSave: true);
            var foreign = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), other.Id, "2100-X1", "Foreign Receivable", AccountType.Asset), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                GetRequiredService<ICustomerAppService>().CreateAsync(new CreateUpdateCustomerDto
                {
                    CompanyId = owner.Id,
                    Name = "Cust Acct Customer 1",
                    IsActive = true,
                    DefaultReceivableAccountId = foreign.Id,
                }));
        });
    }
}
