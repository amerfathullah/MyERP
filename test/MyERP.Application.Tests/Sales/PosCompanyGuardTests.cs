using System;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Regression coverage for a real gap found via a CompanyRestrictionValidationService spot-check
/// sweep: PosAppService.CompleteSaleAsync never checked that Customer/Items belonged to the sale's
/// Company — a cashier could complete a POS sale against a cross-company Customer or Item with
/// zero guard firing, unlike SalesOrder/SalesInvoice/DeliveryNote which all wire this in.
/// </summary>
public abstract class PosCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CompleteSaleAsync_ItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var posOpeningRepository = GetRequiredService<IRepository<PosOpeningEntry, Guid>>();
            var posAppService = GetRequiredService<IPosAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "POS Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "POS Guard Other Co"), autoSave: true);

            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "POS Guard Customer"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "POS-GUARD-1", "POS Guard Item", ItemType.Goods), autoSave: true);

            await posOpeningRepository.InsertAsync(
                new PosOpeningEntry(Guid.NewGuid(), ownerCompany.Id, Guid.NewGuid(), Guid.NewGuid()), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                posAppService.CompleteSaleAsync(new CreatePosInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    Items =
                    {
                        new PosLineItemDto { ItemId = item.Id, Description = "Cross-co item", Quantity = 1, UnitPrice = 10m, TaxAmount = 0m },
                    },
                    AmountReceived = 10m,
                }));
        });
    }

    [Fact]
    public async Task CompleteSaleAsync_BlockedCustomer_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var posOpeningRepository = GetRequiredService<IRepository<PosOpeningEntry, Guid>>();
            var posAppService = GetRequiredService<IPosAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "POS Hold Co"), autoSave: true);
            await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY Test", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
                autoSave: true);

            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Blocked POS Customer")
            {
                OnHold = true,
                ReleaseDate = null
            }, autoSave: true);

            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "POS-HOLD-1", "POS Hold Item", ItemType.Goods), autoSave: true);

            await posOpeningRepository.InsertAsync(
                new PosOpeningEntry(Guid.NewGuid(), company.Id, Guid.NewGuid(), Guid.NewGuid()), autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                posAppService.CompleteSaleAsync(new CreatePosInvoiceDto
                {
                    CompanyId = company.Id,
                    CustomerId = customer.Id,
                    Items =
                    {
                        new PosLineItemDto { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 10m, TaxAmount = 0m },
                    },
                    AmountReceived = 10m,
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.CustomerBlocked);
        });
    }

    [Fact]
    public async Task CompleteSaleAsync_BackdatedSession_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var posOpeningRepository = GetRequiredService<IRepository<PosOpeningEntry, Guid>>();
            var posProfileRepository = GetRequiredService<IRepository<PosProfile, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var costCenterRepository = GetRequiredService<IRepository<CostCenter, Guid>>();
            var ruleRepository = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var posAppService = GetRequiredService<IPosAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "POS Backdate Co"), autoSave: true);
            var backdate = DateTime.UtcNow.Date.AddDays(-10);
            await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY Backdate", backdate.AddMonths(-1), backdate.AddMonths(11)),
                autoSave: true);

            var receivableAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1130-POSBD", "Test Receivable", AccountType.Asset), autoSave: true);
            var incomeAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4000-POSBD", "Test Revenue", AccountType.Revenue), autoSave: true);
            var costCenter = await costCenterRepository.InsertAsync(
                new CostCenter(Guid.NewGuid(), company.Id, "Test Cost Center BD"), autoSave: true);

            company.DefaultReceivableAccountId = receivableAccount.Id;
            company.DefaultIncomeAccountId = incomeAccount.Id;
            company.DefaultCostCenterId = costCenter.Id;
            await companyRepository.UpdateAsync(company, autoSave: true);

            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "SI DR Receivable", "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal) { SortOrder = 1 },
                autoSave: true);
            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "SI CR Revenue", "SalesInvoice", false, AccountSource.ItemIncome, AmountSource.NetTotal) { SortOrder = 2 },
                autoSave: true);
            await seriesRepository.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "POS Series", "POS", "POS-"), autoSave: true);

            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WH-BD1"), autoSave: true);
            var profile = await posProfileRepository.InsertAsync(new PosProfile(Guid.NewGuid(), company.Id, "Backdated Profile 1", warehouse.Id), autoSave: true);

            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Backdated Customer"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "POS-BACKDATE-1", "POS Backdate Item", ItemType.Goods), autoSave: true);

            await posOpeningRepository.InsertAsync(
                new PosOpeningEntry(Guid.NewGuid(), company.Id, profile.Id, Guid.NewGuid(),
                    openingDate: backdate, openingTime: backdate.AddHours(8)), autoSave: true);

            var result = await posAppService.CompleteSaleAsync(new CreatePosInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                PosProfileId = profile.Id,
                PostingDate = backdate,
                PostingTime = backdate.AddHours(10),
                Items =
                {
                    new PosLineItemDto { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 10m, TaxAmount = 0m },
                },
                AmountReceived = 10m,
            });

            result.ShouldNotBeNull();
            result.IssueDate.Date.ShouldBe(backdate);
        });
    }

    [Fact]
    public async Task CompleteSaleAsync_PostingDateTimeBeforeOpening_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var posOpeningRepository = GetRequiredService<IRepository<PosOpeningEntry, Guid>>();
            var posProfileRepository = GetRequiredService<IRepository<PosProfile, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var posAppService = GetRequiredService<IPosAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "POS Backdate Co 2"), autoSave: true);
            var backdate = DateTime.UtcNow.Date.AddDays(-10);
            await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY Backdate 2", backdate.AddMonths(-1), backdate.AddMonths(11)),
                autoSave: true);

            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WH-BD2"), autoSave: true);
            var profile = await posProfileRepository.InsertAsync(new PosProfile(Guid.NewGuid(), company.Id, "Backdated Profile 2", warehouse.Id), autoSave: true);

            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Backdated Customer 2"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "POS-BACKDATE-2", "POS Backdate Item 2", ItemType.Goods), autoSave: true);

            await posOpeningRepository.InsertAsync(
                new PosOpeningEntry(Guid.NewGuid(), company.Id, profile.Id, Guid.NewGuid(),
                    openingDate: backdate, openingTime: backdate.AddHours(10)), autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                posAppService.CompleteSaleAsync(new CreatePosInvoiceDto
                {
                    CompanyId = company.Id,
                    CustomerId = customer.Id,
                    PosProfileId = profile.Id,
                    PostingDate = backdate,
                    PostingTime = backdate.AddHours(8), // before 10:00 opening
                    Items =
                    {
                        new PosLineItemDto { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 10m, TaxAmount = 0m },
                    },
                    AmountReceived = 10m,
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PosPostingDateTimeBeforeOpening);
        });
    }
}
