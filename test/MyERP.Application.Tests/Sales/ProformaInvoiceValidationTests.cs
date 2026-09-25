using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using MyERP.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.SettingManagement;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Application tests for Proforma Invoice line validations and Sales Order item update guards
/// (ERPNext PR #59401 / commit 3e5da36b59 & PR #59402 / commit 63ae699e41).
/// </summary>
public abstract class ProformaInvoiceValidationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private async Task EnableProformaInvoiceAsync()
    {
        var settingManager = GetRequiredService<ISettingManager>();
        await settingManager.SetGlobalAsync(MyERPSettings.Selling.EnableProformaInvoice, "true");
    }

    private async Task EnsureSeriesAsync(Guid companyId)
    {
        var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), companyId, "Proforma Series", "PRO", "PRO-"), autoSave: true);
    }

    [Fact]
    public async Task CreateAsync_LineFromAnotherSalesOrder_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await EnableProformaInvoiceAsync();

            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var proformaService = GetRequiredService<IProformaInvoiceAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proforma Line Co"), autoSave: true);
            await EnsureSeriesAsync(company.Id);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Cust PFI 1"), autoSave: true);

            var so1 = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PFI-001", DateTime.UtcNow);
            so1.AddItem(Guid.NewGuid(), "SO1 Item", 10m, 100m, 0m);
            so1.Submit();
            await soRepo.InsertAsync(so1, autoSave: true);

            var so2 = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PFI-002", DateTime.UtcNow);
            so2.AddItem(Guid.NewGuid(), "SO2 Item", 5m, 50m, 0m);
            so2.Submit();
            await soRepo.InsertAsync(so2, autoSave: true);

            var input = new CreateProformaInvoiceDto
            {
                SalesOrderId = so1.Id,
                BasedOn = ProformaInvoiceBasis.Quantity,
                Items = new List<CreateProformaInvoiceItemDto>
                {
                    new()
                    {
                        SalesOrderItemId = so2.Items[0].Id, // from SO2!
                        Quantity = 2m
                    }
                }
            };

            var ex = await Should.ThrowAsync<BusinessException>(() => proformaService.CreateAsync(input));
            ex.Data["detail"]!.ToString()!.ShouldContain("does not belong to Sales Order SO-PFI-001");
        });
    }

    [Fact]
    public async Task CreateAsync_NonPositiveQuantity_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await EnableProformaInvoiceAsync();

            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var proformaService = GetRequiredService<IProformaInvoiceAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proforma Line Co 2"), autoSave: true);
            await EnsureSeriesAsync(company.Id);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Cust PFI 2"), autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PFI-003", DateTime.UtcNow);
            so.AddItem(Guid.NewGuid(), "Widget", 10m, 100m, 0m);
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            var input = new CreateProformaInvoiceDto
            {
                SalesOrderId = so.Id,
                BasedOn = ProformaInvoiceBasis.Quantity,
                Items = new List<CreateProformaInvoiceItemDto>
                {
                    new()
                    {
                        SalesOrderItemId = so.Items[0].Id,
                        Quantity = 0m
                    }
                }
            };

            var ex = await Should.ThrowAsync<BusinessException>(() => proformaService.CreateAsync(input));
            ex.Data["detail"]!.ToString()!.ShouldContain("Qty must be a positive number");
        });
    }

    [Fact]
    public async Task CreateAsync_AmountBasis_NonPositiveAmount_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await EnableProformaInvoiceAsync();

            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var proformaService = GetRequiredService<IProformaInvoiceAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proforma Line Co 3"), autoSave: true);
            await EnsureSeriesAsync(company.Id);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Cust PFI 3"), autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PFI-004", DateTime.UtcNow);
            so.AddItem(Guid.NewGuid(), "Widget", 10m, 100m, 0m);
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            var input = new CreateProformaInvoiceDto
            {
                SalesOrderId = so.Id,
                BasedOn = ProformaInvoiceBasis.Amount,
                Items = new List<CreateProformaInvoiceItemDto>
                {
                    new()
                    {
                        SalesOrderItemId = so.Items[0].Id,
                        Quantity = 2m,
                        Amount = 0m
                    }
                }
            };

            var ex = await Should.ThrowAsync<BusinessException>(() => proformaService.CreateAsync(input));
            ex.Data["detail"]!.ToString()!.ShouldContain("Amount must be a positive number");
        });
    }

    [Fact]
    public async Task CreateAsync_CustomDescription_PersistsAndFallsBack()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await EnableProformaInvoiceAsync();

            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var proformaService = GetRequiredService<IProformaInvoiceAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proforma Line Co 4"), autoSave: true);
            await EnsureSeriesAsync(company.Id);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Cust PFI 4"), autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PFI-005", DateTime.UtcNow);
            so.AddItem(Guid.NewGuid(), "Original Widget Desc", 10m, 100m, 0m);
            so.AddItem(Guid.NewGuid(), "Original Gadget Desc", 5m, 50m, 0m);
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            var input = new CreateProformaInvoiceDto
            {
                SalesOrderId = so.Id,
                BasedOn = ProformaInvoiceBasis.Quantity,
                Items = new List<CreateProformaInvoiceItemDto>
                {
                    new()
                    {
                        SalesOrderItemId = so.Items[0].Id,
                        Quantity = 3m,
                        Description = "Customized Widget Description"
                    },
                    new()
                    {
                        SalesOrderItemId = so.Items[1].Id,
                        Quantity = 2m,
                        Description = null // fallback to SO description
                    }
                }
            };

            var result = await proformaService.CreateAsync(input);
            result.Items.Count.ShouldBe(2);
            result.Items[0].Description.ShouldBe("Customized Widget Description");
            result.Items[1].Description.ShouldBe("Original Gadget Desc");
        });
    }

    [Fact]
    public async Task UpdateItemsAsync_SalesOrder_BlocksDeletingRowWithIssuedProforma()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await EnableProformaInvoiceAsync();

            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var proformaService = GetRequiredService<IProformaInvoiceAppService>();
            var salesOrderAppService = GetRequiredService<ISalesOrderAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proforma Line Co 5"), autoSave: true);
            await EnsureSeriesAsync(company.Id);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Cust PFI 5"), autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PFI-006", DateTime.UtcNow);
            so.AddItem(Guid.NewGuid(), "Item To Keep", 10m, 100m, 0m);
            so.AddItem(Guid.NewGuid(), "Item With Proforma", 5m, 50m, 0m);
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            var proformedItem = so.Items[1];

            // Create and issue proforma against item 2
            await proformaService.CreateAsync(new CreateProformaInvoiceDto
            {
                SalesOrderId = so.Id,
                BasedOn = ProformaInvoiceBasis.Quantity,
                Items = new List<CreateProformaInvoiceItemDto>
                {
                    new()
                    {
                        SalesOrderItemId = proformedItem.Id,
                        Quantity = 2m
                    }
                }
            });

            // Now attempt to delete proformed item via UpdateItemsAsync
            var updateDto = new MyERP.Purchasing.UpdateOrderItemsDto
            {
                RemovedItemIds = new List<Guid> { proformedItem.Id },
                Items = new List<MyERP.Purchasing.UpdateOrderItemDto>()
            };

            var ex = await Should.ThrowAsync<BusinessException>(() => salesOrderAppService.UpdateItemsAsync(so.Id, updateDto));
            ex.Data["detail"]!.ToString()!.ShouldContain("Cannot delete item 'Item With Proforma' which has an issued Proforma Invoice.");
        });
    }
}
