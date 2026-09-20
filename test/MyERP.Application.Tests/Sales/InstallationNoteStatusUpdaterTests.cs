using System;
using System.Collections.Generic;
using System.Linq;
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

public abstract class InstallationNoteStatusUpdaterTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IInstallationNoteAppService _installationNoteAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;

    protected InstallationNoteStatusUpdaterTests()
    {
        _installationNoteAppService = GetRequiredService<IInstallationNoteAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
        _warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        _itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        _deliveryNoteRepository = GetRequiredService<IRepository<DeliveryNote, Guid>>();
    }

    [Fact]
    public async Task SubmitAndCancel_Should_Update_InstalledQty_And_PerInstalled()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Inst Co"), autoSave: true);
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "IN Series", "IN", "IN-"), autoSave: true);
            var customer = await _customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Inst Cust"), autoSave: true);
            var warehouse = await _warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Inst WH"), autoSave: true);
            var item = await _itemRepository.InsertAsync(new Item(Guid.NewGuid(), company.Id, "INST-ITEM-001", "Installation Item", ItemType.Goods) { MaintainStock = false }, autoSave: true);

            var dn = new DeliveryNote(Guid.NewGuid(), company.Id, customer.Id, warehouse.Id, "DN-INST-001", DateTime.UtcNow.Date);
            dn.AddItem(item.Id, "Installation Item Desc", 10m, 100m, 0m);
            dn.Submit();
            await _deliveryNoteRepository.InsertAsync(dn, autoSave: true);

            var dnItemId = dn.Items[0].Id;
            dn.PerInstalled.ShouldBe(0m);
            dn.InstallationStatus.ShouldBe("Not Installed");

            // Create Installation Note for 6 units
            var inDto = await _installationNoteAppService.CreateAsync(new CreateInstallationNoteDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                DeliveryNoteId = dn.Id,
                InstallationDate = DateTime.UtcNow.Date,
                Items = new List<InstallationNoteItemDto>
                {
                    new InstallationNoteItemDto
                    {
                        ItemId = item.Id,
                        DeliveryNoteItemId = dnItemId,
                        Qty = 6m
                    }
                }
            });

            // Submit Installation Note
            await _installationNoteAppService.SubmitAsync(inDto.Id);

            var dnAfterSubmit = await _deliveryNoteRepository.GetAsync(dn.Id, includeDetails: true);
            dnAfterSubmit.Items[0].InstalledQty.ShouldBe(6m);
            dnAfterSubmit.PerInstalled.ShouldBe(60m);
            dnAfterSubmit.InstallationStatus.ShouldBe("Partially Installed");

            // Cancel Installation Note
            await _installationNoteAppService.CancelAsync(inDto.Id);

            var dnAfterCancel = await _deliveryNoteRepository.GetAsync(dn.Id, includeDetails: true);
            dnAfterCancel.Items[0].InstalledQty.ShouldBe(0m);
            dnAfterCancel.PerInstalled.ShouldBe(0m);
            dnAfterCancel.InstallationStatus.ShouldBe("Not Installed");
        });
    }
}
