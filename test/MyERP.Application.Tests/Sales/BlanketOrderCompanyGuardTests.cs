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
}
