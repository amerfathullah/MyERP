using System;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 11 upstream ERPNext parity (PR #64936):
/// 1) Customer/Supplier billing currency and default account currency validation (ca02eb09c5, 9ecfac159f).
/// 2) PartyDetailsAppService currency and party account currency resolution.
/// </summary>
public abstract class UpstreamBatch11IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CustomerAppService_CreateAsync_PartyAccountCurrencyMatchesBillingCurrency_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var customerAppService = GetRequiredService<ICustomerAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Cust Currency Co")
            {
                CurrencyCode = "MYR"
            }, autoSave: true);

            var usdAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1130-USD", "USD Debtors", AccountType.Asset)
            {
                Currency = "USD",
                IsGroup = false,
                IsActive = true
            }, autoSave: true);

            var customer = await customerAppService.CreateAsync(new CreateUpdateCustomerDto
            {
                CompanyId = company.Id,
                Name = "Global Corp USD",
                DefaultCurrency = "USD",
                DefaultReceivableAccountId = usdAccount.Id,
                IsActive = true
            });

            customer.ShouldNotBeNull();
            customer.DefaultCurrency.ShouldBe("USD");
            customer.DefaultReceivableAccountId.ShouldBe(usdAccount.Id);
        });
    }

    [Fact]
    public async Task CustomerAppService_CreateAsync_PartyAccountCurrencyMismatch_ThrowsPartyAccountCurrencyMismatch()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var customerAppService = GetRequiredService<ICustomerAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Cust Currency Co 2")
            {
                CurrencyCode = "MYR"
            }, autoSave: true);

            // Account is EUR, but customer billing currency is USD and company is MYR
            var eurAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1130-EUR", "EUR Debtors", AccountType.Asset)
            {
                Currency = "EUR",
                IsGroup = false,
                IsActive = true
            }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                customerAppService.CreateAsync(new CreateUpdateCustomerDto
                {
                    CompanyId = company.Id,
                    Name = "Global Corp USD Bad Acct",
                    DefaultCurrency = "USD",
                    DefaultReceivableAccountId = eurAccount.Id,
                    IsActive = true
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PartyAccountCurrencyMismatch);
        });
    }

    [Fact]
    public async Task SupplierAppService_CreateAsync_PartyAccountCurrencyMatchesBillingCurrency_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var supplierAppService = GetRequiredService<ISupplierAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Supp Currency Co")
            {
                CurrencyCode = "MYR"
            }, autoSave: true);

            var usdAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "2100-USD", "USD Creditors", AccountType.Liability)
            {
                Currency = "USD",
                IsGroup = false,
                IsActive = true
            }, autoSave: true);

            var supplier = await supplierAppService.CreateAsync(new CreateUpdateSupplierDto
            {
                CompanyId = company.Id,
                Name = "Overseas Supplier USD",
                DefaultCurrency = "USD",
                DefaultPayableAccountId = usdAccount.Id,
                IsActive = true
            });

            supplier.ShouldNotBeNull();
            supplier.DefaultCurrency.ShouldBe("USD");
            supplier.DefaultPayableAccountId.ShouldBe(usdAccount.Id);
        });
    }

    [Fact]
    public async Task SupplierAppService_CreateAsync_PartyAccountCurrencyMismatch_ThrowsPartyAccountCurrencyMismatch()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var supplierAppService = GetRequiredService<ISupplierAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Supp Currency Co 2")
            {
                CurrencyCode = "MYR"
            }, autoSave: true);

            // Account is EUR, but supplier billing currency is USD and company is MYR
            var eurAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "2100-EUR", "EUR Creditors", AccountType.Liability)
            {
                Currency = "EUR",
                IsGroup = false,
                IsActive = true
            }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                supplierAppService.CreateAsync(new CreateUpdateSupplierDto
                {
                    CompanyId = company.Id,
                    Name = "Overseas Supplier USD Bad Acct",
                    DefaultCurrency = "USD",
                    DefaultPayableAccountId = eurAccount.Id,
                    IsActive = true
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PartyAccountCurrencyMismatch);
        });
    }

    [Fact]
    public async Task PartyDetailsAppService_ResolvesPartyCurrencyAndAccountCurrencyCorrectly()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var partyDetailsAppService = GetRequiredService<IPartyDetailsAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PartyDetails Co")
            {
                CurrencyCode = "MYR"
            }, autoSave: true);

            var usdReceivable = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1130-US", "USD Receivables", AccountType.Asset)
            {
                Currency = "USD"
            }, autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Customer FX")
            {
                DefaultCurrency = "USD",
                DefaultReceivableAccountId = usdReceivable.Id
            }, autoSave: true);

            var custDetails = await partyDetailsAppService.GetCustomerDetailsAsync(new GetPartyDetailsInput
            {
                PartyId = customer.Id,
                CompanyId = company.Id
            });

            custDetails.PartyCurrency.ShouldBe("USD");
            custDetails.PartyAccountCurrency.ShouldBe("USD");
            custDetails.CompanyCurrency.ShouldBe("MYR");

            var usdPayable = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "2100-US", "USD Payables", AccountType.Liability)
            {
                Currency = "USD"
            }, autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Supplier FX")
            {
                DefaultCurrency = "USD",
                DefaultPayableAccountId = usdPayable.Id
            }, autoSave: true);

            var suppDetails = await partyDetailsAppService.GetSupplierDetailsAsync(new GetPartyDetailsInput
            {
                PartyId = supplier.Id,
                CompanyId = company.Id
            });

            suppDetails.PartyCurrency.ShouldBe("USD");
            suppDetails.PartyAccountCurrency.ShouldBe("USD");
            suppDetails.CompanyCurrency.ShouldBe("MYR");
        });
    }
}
