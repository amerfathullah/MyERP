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

namespace MyERP.Sales;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: every other
/// Selling/Purchasing document wires CompanyRestrictionValidationService into CreateAsync;
/// BlanketOrderAppService.CreateAsync never did — a cross-company Customer/Supplier or Item could
/// be locked into a Blanket Order agreement despite the same reference being blocked on the Sales
/// Order/Purchase Order drawing from it.
/// </summary>
public abstract class BlanketOrderCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_SellingCustomerFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var blanketOrderAppService = GetRequiredService<IBlanketOrderAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "BO Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "BO Guard Other Co"), autoSave: true);

            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "BO-ITEM-001", "BO Guard Item", ItemType.Goods), autoSave: true);
            var customer = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), otherCompany.Id, "BO Guard Cross-Co Customer"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                blanketOrderAppService.CreateAsync(new CreateBlanketOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    OrderType = "Selling",
                    PartyId = customer.Id,
                    FromDate = DateTime.Today,
                    ToDate = DateTime.Today.AddMonths(6),
                    Items =
                    [
                        new CreateBlanketOrderItemDto { ItemId = item.Id, Qty = 100m, Rate = 10m }
                    ]
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_BuyingSupplierFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var blanketOrderAppService = GetRequiredService<IBlanketOrderAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "BO Guard Buying Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "BO Guard Buying Other Co"), autoSave: true);

            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "BO-ITEM-002", "BO Guard Item 2", ItemType.Goods), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(
                new MyERP.Purchasing.Entities.Supplier(Guid.NewGuid(), otherCompany.Id, "BO Guard Cross-Co Supplier"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                blanketOrderAppService.CreateAsync(new CreateBlanketOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    OrderType = "Buying",
                    PartyId = supplier.Id,
                    FromDate = DateTime.Today,
                    ToDate = DateTime.Today.AddMonths(6),
                    Items =
                    [
                        new CreateBlanketOrderItemDto { ItemId = item.Id, Qty = 50m, Rate = 20m }
                    ]
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SameCompanySellingParty_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var blanketOrderAppService = GetRequiredService<IBlanketOrderAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "BO Guard Happy Co"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BO-ITEM-003", "BO Guard Item 3", ItemType.Goods), autoSave: true);
            var customer = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "BO Guard Same-Co Customer"), autoSave: true);

            var dto = await blanketOrderAppService.CreateAsync(new CreateBlanketOrderDto
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = customer.Id,
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddMonths(6),
                Items =
                [
                    new CreateBlanketOrderItemDto { ItemId = item.Id, Qty = 100m, Rate = 10m }
                ]
            });

            dto.Id.ShouldNotBe(Guid.Empty);
        });
    }

    [Fact]
    public async Task CreateAsync_FromDateAfterToDate_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(new Company(Guid.NewGuid(), "BO Dates Co"), autoSave: true);
            var customer = await GetRequiredService<IRepository<Customer, Guid>>().InsertAsync(new Customer(Guid.NewGuid(), company.Id, "BO Dates Customer"), autoSave: true);
            var item = await GetRequiredService<IRepository<Item, Guid>>().InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BO-ITEM-DATES", "BO Dates Item", ItemType.Goods), autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                GetRequiredService<IBlanketOrderAppService>().CreateAsync(new CreateBlanketOrderDto
                {
                    CompanyId = company.Id,
                    OrderType = "Selling",
                    PartyId = customer.Id,
                    FromDate = DateTime.Today.AddMonths(6),
                    ToDate = DateTime.Today,
                    Items = [new CreateBlanketOrderItemDto { ItemId = item.Id, Qty = 1m, Rate = 1m }]
                }));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task CreateAsync_TwoOrdersInSameSecond_GetDistinctNumbers()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(new Company(Guid.NewGuid(), "BO Number Co"), autoSave: true);
            var customer = await GetRequiredService<IRepository<Customer, Guid>>().InsertAsync(new Customer(Guid.NewGuid(), company.Id, "BO Number Customer"), autoSave: true);
            var item = await GetRequiredService<IRepository<Item, Guid>>().InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BO-ITEM-NUM", "BO Number Item", ItemType.Goods), autoSave: true);
            var app = GetRequiredService<IBlanketOrderAppService>();
            CreateBlanketOrderDto Dto() => new()
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = customer.Id,
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddMonths(1),
                Items = [new CreateBlanketOrderItemDto { ItemId = item.Id, Qty = 1m, Rate = 1m }]
            };

            var first = await app.CreateAsync(Dto());
            var second = await app.CreateAsync(Dto());

            first.OrderNumber.ShouldNotBe(second.OrderNumber);
        });
    }

    [Fact]
    public async Task CreateAsync_DefaultsCompanyCurrency_AndResolvesExchangeRateAndBaseRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = new Company(Guid.NewGuid(), "BO FX Co") { CurrencyCode = "MYR" };
            await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(company, autoSave: true);
            var customer = await GetRequiredService<IRepository<Customer, Guid>>().InsertAsync(new Customer(Guid.NewGuid(), company.Id, "BO FX Customer"), autoSave: true);
            var item = await GetRequiredService<IRepository<Item, Guid>>().InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BO-ITEM-FX", "BO FX Item", ItemType.Goods), autoSave: true);
            await GetRequiredService<IRepository<MyERP.Accounting.Entities.CurrencyExchange, Guid>>().InsertAsync(
                new MyERP.Accounting.Entities.CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 4.5m, DateTime.Today), autoSave: true);

            var app = GetRequiredService<IBlanketOrderAppService>();

            var defaultDto = await app.CreateAsync(new CreateBlanketOrderDto
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = customer.Id,
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddMonths(1),
                Items = [new CreateBlanketOrderItemDto { ItemId = item.Id, Qty = 10m, Rate = 100m }]
            });
            defaultDto.Currency.ShouldBe("MYR");
            defaultDto.ExchangeRate.ShouldBe(1m);
            defaultDto.Items[0].BaseRate.ShouldBe(100m);

            var fxDto = await app.CreateAsync(new CreateBlanketOrderDto
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = customer.Id,
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddMonths(1),
                Currency = "usd",
                Items = [new CreateBlanketOrderItemDto { ItemId = item.Id, Qty = 10m, Rate = 100m }]
            });
            fxDto.Currency.ShouldBe("USD");
            fxDto.ExchangeRate.ShouldBe(4.5m);
            fxDto.Items[0].BaseRate.ShouldBe(450m);
        });
    }

    [Fact]
    public async Task GetListAsync_FilterMatchesPartyName()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(new Company(Guid.NewGuid(), "BO Filter Co"), autoSave: true);
            var customer = await GetRequiredService<IRepository<Customer, Guid>>().InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Alpha Aero"), autoSave: true);
            var item = await GetRequiredService<IRepository<Item, Guid>>().InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BO-ITEM-FLT", "BO Filter Item", ItemType.Goods), autoSave: true);

            var app = GetRequiredService<IBlanketOrderAppService>();
            await app.CreateAsync(new CreateBlanketOrderDto
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = customer.Id,
                PartyName = "Alpha Aero",
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddMonths(1),
                Items = [new CreateBlanketOrderItemDto { ItemId = item.Id, Qty = 5m, Rate = 10m }]
            });

            var result = await app.GetListAsync(new MyERP.Shared.CompanyFilteredPagedRequestDto
            {
                CompanyId = company.Id,
                Filter = "Alpha",
                MaxResultCount = 10
            });
            result.TotalCount.ShouldBe(1);
            result.Items[0].PartyName.ShouldBe("Alpha Aero");
        });
    }

    [Fact]
    public async Task GetActiveForPartyAsync_ScopesByParty_ResolvesLeadAndProspect_AndNeverLeaksOtherCustomer()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(
                new Company(Guid.NewGuid(), "BO Party Filter Co") { CurrencyCode = "MYR" }, autoSave: true);
            var custRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var leadRepo = GetRequiredService<IRepository<MyERP.CRM.Entities.Lead, Guid>>();
            var prospectRepo = GetRequiredService<IRepository<MyERP.CRM.Entities.Prospect, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var boApp = GetRequiredService<IBlanketOrderAppService>();
            var itemDetailsApp = GetRequiredService<IItemDetailsAppService>();

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BO-PF-1", "Party Item", ItemType.Goods), autoSave: true);

            var lead = new MyERP.CRM.Entities.Lead(Guid.NewGuid(), company.Id, "LD-BO-1", "Lead Co");
            var unconvertedProspect = new MyERP.CRM.Entities.Prospect(Guid.NewGuid(), company.Id, "Unconverted Prospect");
            var convertedProspect = new MyERP.CRM.Entities.Prospect(Guid.NewGuid(), company.Id, "Converted Prospect");

            var custA = new Customer(Guid.NewGuid(), company.Id, "Customer A") { LeadId = lead.Id };
            var custB = new Customer(Guid.NewGuid(), company.Id, "Customer B") { ProspectId = convertedProspect.Id };
            lead.ConvertToCustomer(custA.Id);
            convertedProspect.ConvertedCustomerId = custB.Id;

            await custRepo.InsertAsync(custA, autoSave: true);
            await custRepo.InsertAsync(custB, autoSave: true);
            await leadRepo.InsertAsync(lead, autoSave: true);
            await prospectRepo.InsertAsync(unconvertedProspect, autoSave: true);
            await prospectRepo.InsertAsync(convertedProspect, autoSave: true);

            var boA = await boApp.CreateAsync(new CreateBlanketOrderDto
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = custA.Id,
                FromDate = DateTime.Today.AddDays(-1),
                ToDate = DateTime.Today.AddDays(30),
                Items = [new CreateBlanketOrderItemDto { ItemId = item.Id, Qty = 10m, Rate = 55m }]
            });
            await boApp.SubmitAsync(boA.Id);

            var boB = await boApp.CreateAsync(new CreateBlanketOrderDto
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = custB.Id,
                FromDate = DateTime.Today.AddDays(-1),
                ToDate = DateTime.Today.AddDays(30),
                Items = [new CreateBlanketOrderItemDto { ItemId = item.Id, Qty = 10m, Rate = 77m }]
            });
            await boApp.SubmitAsync(boB.Id);

            // Draft BO for Customer A must NOT be offered
            await boApp.CreateAsync(new CreateBlanketOrderDto
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = custA.Id,
                FromDate = DateTime.Today.AddDays(-1),
                ToDate = DateTime.Today.AddDays(30),
                Items = [new CreateBlanketOrderItemDto { ItemId = item.Id, Qty = 5m, Rate = 44m }]
            });

            // 1. Direct party filter returns only that party's submitted BO (never custB's)
            var forCustA = await boApp.GetActiveForPartyAsync(new GetActiveBlanketOrdersInput
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = custA.Id,
                ItemId = item.Id,
                TransactionDate = DateTime.Today
            });
            forCustA.Count.ShouldBe(1);
            forCustA[0].Id.ShouldBe(boA.Id);

            // 2. Lead without direct CustomerId resolves converted Customer A
            var forLead = await boApp.GetActiveForPartyAsync(new GetActiveBlanketOrdersInput
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                LeadId = lead.Id,
                ItemId = item.Id,
                TransactionDate = DateTime.Today
            });
            forLead.Count.ShouldBe(1);
            forLead[0].Id.ShouldBe(boA.Id);

            // 3. Prospect with converted Customer B resolves BO B via GetActiveForPartyAsync & GetItemDetailsAsync
            var forConvertedProspect = await boApp.GetActiveForPartyAsync(new GetActiveBlanketOrdersInput
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                ProspectId = convertedProspect.Id,
                ItemId = item.Id,
                TransactionDate = DateTime.Today
            });
            forConvertedProspect.Count.ShouldBe(1);
            forConvertedProspect[0].Id.ShouldBe(boB.Id);

            var detailsForProspect = await itemDetailsApp.GetItemDetailsAsync(new GetItemDetailsInput
            {
                ItemId = item.Id,
                TransactionType = "Selling",
                CompanyId = company.Id,
                ProspectId = convertedProspect.Id,
                TransactionDate = DateTime.Today
            });
            detailsForProspect.BlanketOrderId.ShouldBe(boB.Id);
            detailsForProspect.Rate.ShouldBe(77m);

            // 4. Unconverted Prospect or missing party must return empty and never leak another customer's BO
            var forUnconverted = await boApp.GetActiveForPartyAsync(new GetActiveBlanketOrdersInput
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                ProspectId = unconvertedProspect.Id,
                ItemId = item.Id,
                TransactionDate = DateTime.Today
            });
            forUnconverted.ShouldBeEmpty();

            var detailsForUnconverted = await itemDetailsApp.GetItemDetailsAsync(new GetItemDetailsInput
            {
                ItemId = item.Id,
                TransactionType = "Selling",
                CompanyId = company.Id,
                ProspectId = unconvertedProspect.Id,
                TransactionDate = DateTime.Today
            });
            detailsForUnconverted.BlanketOrderId.ShouldBeNull();

            var forNoParty = await boApp.GetActiveForPartyAsync(new GetActiveBlanketOrdersInput
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                ItemId = item.Id,
                TransactionDate = DateTime.Today
            });
            forNoParty.ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task GetActiveForPartyAsync_FiltersByTypedOrderNumberAndPaginates_Pr67986()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(
                new Company(Guid.NewGuid(), "BO Search Co"), autoSave: true);
            var customer = await GetRequiredService<IRepository<Customer, Guid>>().InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "BO Search Customer"), autoSave: true);
            var item = await GetRequiredService<IRepository<Item, Guid>>().InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BO-SRCH-ITEM", "BO Search Item", ItemType.Goods), autoSave: true);

            var boApp = GetRequiredService<IBlanketOrderAppService>();
            var bo1 = await boApp.CreateAsync(new CreateBlanketOrderDto
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = customer.Id,
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddMonths(1),
                Items = [new CreateBlanketOrderItemDto { ItemId = item.Id, Qty = 10m, Rate = 10m }]
            });
            await boApp.SubmitAsync(bo1.Id);

            var bo2 = await boApp.CreateAsync(new CreateBlanketOrderDto
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = customer.Id,
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddMonths(1),
                Items = [new CreateBlanketOrderItemDto { ItemId = item.Id, Qty = 20m, Rate = 12m }]
            });
            await boApp.SubmitAsync(bo2.Id);

            // Typed text filter matches only the targeted Blanket Order number
            var filtered = await boApp.GetActiveForPartyAsync(new GetActiveBlanketOrdersInput
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = customer.Id,
                ItemId = item.Id,
                TransactionDate = DateTime.Today,
                Filter = bo2.OrderNumber,
            });
            filtered.Count.ShouldBe(1);
            filtered[0].Id.ShouldBe(bo2.Id);

            // Pagination returns at most MaxResultCount starting at SkipCount
            var page1 = await boApp.GetActiveForPartyAsync(new GetActiveBlanketOrdersInput
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = customer.Id,
                ItemId = item.Id,
                TransactionDate = DateTime.Today,
                SkipCount = 0,
                MaxResultCount = 1,
            });
            page1.Count.ShouldBe(1);

            var page2 = await boApp.GetActiveForPartyAsync(new GetActiveBlanketOrdersInput
            {
                CompanyId = company.Id,
                OrderType = "Selling",
                PartyId = customer.Id,
                ItemId = item.Id,
                TransactionDate = DateTime.Today,
                SkipCount = 1,
                MaxResultCount = 1,
            });
            page2.Count.ShouldBe(1);
            page2[0].Id.ShouldNotBe(page1[0].Id);
        });
    }
}
