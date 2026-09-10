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
}
