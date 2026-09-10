using System;
using System.Collections.Generic;
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
/// Regression coverage for a real gap found via ERPNext validate() parity: every other Selling
/// document (SalesOrder, SalesInvoice, DeliveryNote) wires CompanyRestrictionValidationService into
/// CreateAsync; Quotation was the one that didn't — and since
/// DocumentConversionAppService.ConvertQuotationToSalesOrderAsync builds the SalesOrder directly
/// rather than going through SalesOrderAppService.CreateAsync, an unchecked Quotation could carry a
/// cross-company Customer/Item all the way to a submitted Sales Order with no check ever firing.
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
}
