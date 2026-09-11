using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.CRM.Entities;
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
/// Regression coverage for real company-boundary gaps: Quotation CreateAsync & UpdateAsync
/// must ensure Customer, Items, and Opportunity all belong to the quotation company.
/// </summary>
public abstract class QuotationCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
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
            var quotationAppService = GetRequiredService<IQuotationAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Quotation Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Quotation Guard Other Co"), autoSave: true);

            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "QUOT-ITEM-001", "Quotation Guard Item", ItemType.Goods), autoSave: true);
            var customer = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), otherCompany.Id, "Quotation Guard Cross-Co Customer"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                quotationAppService.CreateAsync(new CreateQuotationDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    IssueDate = DateTime.Today,
                    Items = new List<CreateQuotationItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Widget", Quantity = 5m, UnitPrice = 10m, Uom = "Unit" }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_OpportunityFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var oppRepository = GetRequiredService<IRepository<Opportunity, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var quotationAppService = GetRequiredService<IQuotationAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Quotation Opp Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Quotation Opp Other Co"), autoSave: true);

            var ownerCustomer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "Opp Owner Cust"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "QUOT-ITEM-OPP", "Quotation Opp Item", ItemType.Goods), autoSave: true);

            var crossOpp = new Opportunity(Guid.NewGuid(), otherCompany.Id, "OPP-CROSS-01", "Cross Opp");
            await oppRepository.InsertAsync(crossOpp, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                quotationAppService.CreateAsync(new CreateQuotationDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = ownerCustomer.Id,
                    OpportunityId = crossOpp.Id,
                    IssueDate = DateTime.Today,
                    Items = new List<CreateQuotationItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Widget Opp", Quantity = 1m, UnitPrice = 10m, Uom = "Unit" }
                    }
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_CustomerFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var quotationRepository = GetRequiredService<IRepository<Quotation, Guid>>();
            var quotationAppService = GetRequiredService<IQuotationAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Quotation Upd Cust Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Quotation Upd Cust Other Co"), autoSave: true);

            var ownerCustomer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "Upd Cust Owner"), autoSave: true);
            var otherCustomer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "Upd Cust Other"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "QUOT-ITEM-UPD", "Quotation Upd Item", ItemType.Goods), autoSave: true);

            var quotation = new Quotation(Guid.NewGuid(), ownerCompany.Id, ownerCustomer.Id, "QUOT-UPD-001", DateTime.UtcNow.Date);
            quotation.AddItem(item.Id, "Widget", 1m, 10m, 0m);
            await quotationRepository.InsertAsync(quotation, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                quotationAppService.UpdateAsync(quotation.Id, new CreateQuotationDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = otherCustomer.Id,
                    IssueDate = quotation.IssueDate,
                    Items = new List<CreateQuotationItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Widget", Quantity = 1m, UnitPrice = 10m, Uom = "Unit" }
                    }
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_ItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var quotationRepository = GetRequiredService<IRepository<Quotation, Guid>>();
            var quotationAppService = GetRequiredService<IQuotationAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Quotation Upd Item Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Quotation Upd Item Other Co"), autoSave: true);

            var ownerCustomer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "Upd Item Cust Owner"), autoSave: true);
            var ownerItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "QUOT-ITEM-OWN", "Quotation Own Item", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "QUOT-ITEM-OTH", "Quotation Other Item", ItemType.Goods), autoSave: true);

            var quotation = new Quotation(Guid.NewGuid(), ownerCompany.Id, ownerCustomer.Id, "QUOT-UPD-002", DateTime.UtcNow.Date);
            quotation.AddItem(ownerItem.Id, "Widget", 1m, 10m, 0m);
            await quotationRepository.InsertAsync(quotation, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                quotationAppService.UpdateAsync(quotation.Id, new CreateQuotationDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = ownerCustomer.Id,
                    IssueDate = quotation.IssueDate,
                    Items = new List<CreateQuotationItemDto>
                    {
                        new() { ItemId = otherItem.Id, Description = "Widget Other", Quantity = 1m, UnitPrice = 10m, Uom = "Unit" }
                    }
                }));
        });
    }
}
