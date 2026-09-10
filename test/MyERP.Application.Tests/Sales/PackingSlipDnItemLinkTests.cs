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

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: ERPNext's
/// packing_slip.py validate_items() blocks packing more than a Delivery Note line's remaining
/// (unpacked) quantity. MyERP's PackingSlipAppService.CreateAsync had no such check — and worse,
/// PackingSlipItem.DeliveryNoteItemId (the field both this guard and the existing
/// AdjustParentDeliveryNotePackedQtyAsync write-back depend on) was never actually set:
/// CreateAsync called PackingSlip.AddItem(), whose signature has no DeliveryNoteItemId parameter,
/// so every Packing Slip Item's link back to its DN line was silently left null — making the
/// Submit-time "increment DN PackedQty" logic permanently dead code despite looking wired.
/// </summary>
public abstract class PackingSlipDnItemLinkTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubmitAsync_LinksDnItem_AndIncrementsPackedQty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var dnRepository = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var packingSlipAppService = GetRequiredService<IPackingSlipAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Packing DN Link Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Packing DN Link Customer"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Packing DN Link WH"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PACK-DN-1", "Packing DN Link Item", ItemType.Goods), autoSave: true);

            var dn = new DeliveryNote(Guid.NewGuid(), company.Id, customer.Id, warehouse.Id, "DN-PACK-001", DateTime.UtcNow);
            dn.AddItem(item.Id, "Widget", quantity: 10m, unitPrice: 5m, taxAmount: 0m);
            await dnRepository.InsertAsync(dn, autoSave: true);
            var dnItemId = dn.Items.Single().Id;

            var created = await packingSlipAppService.CreateAsync(new CreatePackingSlipDto
            {
                CompanyId = company.Id,
                DeliveryNoteId = dn.Id,
                FromCaseNo = 1,
                ToCaseNo = 1,
                Items = new List<CreatePackingSlipItemDto>
                {
                    new() { ItemId = item.Id, Qty = 4m, NetWeight = 1m, DeliveryNoteItemId = dnItemId }
                }
            });

            created.Items.Single().DeliveryNoteItemId.ShouldBe(dnItemId);

            await packingSlipAppService.SubmitAsync(created.Id);

            var reloadedDn = await dnRepository.GetAsync(dn.Id);
            reloadedDn.Items.Single().PackedQty.ShouldBe(4m);
        });
    }

    [Fact]
    public async Task CreateAsync_QtyExceedsRemainingDnQty_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var dnRepository = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var packingSlipAppService = GetRequiredService<IPackingSlipAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Packing Overpack Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Packing Overpack Customer"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Packing Overpack WH"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PACK-OVER-1", "Packing Overpack Item", ItemType.Goods), autoSave: true);

            var dn = new DeliveryNote(Guid.NewGuid(), company.Id, customer.Id, warehouse.Id, "DN-PACK-002", DateTime.UtcNow);
            dn.AddItem(item.Id, "Widget", quantity: 5m, unitPrice: 5m, taxAmount: 0m);
            await dnRepository.InsertAsync(dn, autoSave: true);
            var dnItemId = dn.Items.Single().Id;

            // First slip packs all 5 units and is submitted, so PackedQty is fully accumulated.
            var firstSlip = await packingSlipAppService.CreateAsync(new CreatePackingSlipDto
            {
                CompanyId = company.Id,
                DeliveryNoteId = dn.Id,
                FromCaseNo = 1,
                ToCaseNo = 1,
                Items = new List<CreatePackingSlipItemDto>
                {
                    new() { ItemId = item.Id, Qty = 5m, NetWeight = 1m, DeliveryNoteItemId = dnItemId }
                }
            });
            await packingSlipAppService.SubmitAsync(firstSlip.Id);

            // A second slip against the same, now-fully-packed DN line must be rejected.
            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                packingSlipAppService.CreateAsync(new CreatePackingSlipDto
                {
                    CompanyId = company.Id,
                    DeliveryNoteId = dn.Id,
                    FromCaseNo = 2,
                    ToCaseNo = 2,
                    Items = new List<CreatePackingSlipItemDto>
                    {
                        new() { ItemId = item.Id, Qty = 1m, NetWeight = 1m, DeliveryNoteItemId = dnItemId }
                    }
                }));
        });
    }
}
