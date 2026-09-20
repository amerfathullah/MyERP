using System;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class AccountFilteringAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAccountAppService _accountAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Account, Guid> _accountRepository;

    protected AccountFilteringAppServiceTests()
    {
        _accountAppService = GetRequiredService<IAccountAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _accountRepository = GetRequiredService<IRepository<Account, Guid>>();
    }

    [Fact]
    public async Task GetListAsync_FilterByCompanyAndSubType_ReturnsMatchingAccountsOnly()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company1 = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Co A Filter"), autoSave: true);
            var company2 = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Co B Filter"), autoSave: true);

            var stockAccount1 = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company1.Id, "1310", "Stock In Hand Co1", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.Stock,
                    IsGroup = false
                }, autoSave: true);

            var bankAccount1 = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company1.Id, "1210", "Bank Co1", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.BankAccount,
                    IsGroup = false
                }, autoSave: true);

            var stockAccount2 = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company2.Id, "1310-2", "Stock In Hand Co2", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.Stock,
                    IsGroup = false
                }, autoSave: true);

            // Filter by company1 and stock subtype
            var result = await _accountAppService.GetListAsync(new GetAccountListDto
            {
                CompanyId = company1.Id,
                AccountSubType = AccountSubType.Stock,
                IsGroup = false
            });

            result.TotalCount.ShouldBe(1);
            result.Items[0].Id.ShouldBe(stockAccount1.Id);
            result.Items[0].AccountName.ShouldBe("Stock In Hand Co1");

            // Filter by company2
            var resultCo2 = await _accountAppService.GetListAsync(new GetAccountListDto
            {
                CompanyId = company2.Id
            });

            resultCo2.TotalCount.ShouldBe(1);
            resultCo2.Items[0].Id.ShouldBe(stockAccount2.Id);
        });
    }
}
