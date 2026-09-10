using System;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Regression coverage for a Buying-type Blanket Order's qty allowance: unlike Sales Orders
/// (SalesOrderAppService already wires BlanketOrderItem.RecordOrder/UnrecordOrder into
/// Submit/Cancel), Purchase Orders had zero enforcement — PurchaseOrderItem didn't even carry a
/// BlanketOrderId, so a PO could reference a Buying Blanket Order with no qty cap ever applied.
/// </summary>
public abstract class PurchaseOrderBlanketOrderTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubmitAsync_DeductsFromBlanketOrderAllocation_AndBlocksOverAllowance()
    {
        Guid companyId = default, supplierId = default, itemId = default, blanketOrderId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var blanketOrderRepository = GetRequiredService<IRepository<BlanketOrder, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PO Blanket Test Co"), autoSave: true);
            await seriesRepository.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "PO Series", "PurchaseOrder", "POBLK-"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "PO Blanket Supplier"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "POBLK-1", "PO Blanket Item", ItemType.Goods), autoSave: true);

            var bo = new BlanketOrder(Guid.NewGuid(), company.Id, "BO-PUR-001", "Buying", supplier.Id,
                DateTime.Today, DateTime.Today.AddMonths(6));
            bo.AddItem(item.Id, qty: 100m, rate: 10m, itemName: item.ItemName);
            bo.Submit();
            await blanketOrderRepository.InsertAsync(bo, autoSave: true);

            companyId = company.Id;
            supplierId = supplier.Id;
            itemId = item.Id;
            blanketOrderId = bo.Id;
        });

        var purchaseOrderAppService = GetRequiredService<IPurchaseOrderAppService>();
        var blanketOrderRepo = GetRequiredService<IRepository<BlanketOrder, Guid>>();

        // First PO consumes the full 100 qty (allowance defaults to 0%, so max = 100).
        var firstPo = await purchaseOrderAppService.CreateAsync(new CreatePurchaseOrderDto
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            OrderDate = DateTime.Today,
            Items =
            {
                new CreatePurchaseOrderItemDto
                {
                    ItemId = itemId,
                    Description = "PO Blanket Item",
                    Quantity = 100m,
                    UnitPrice = 10m,
                    BlanketOrderId = blanketOrderId,
                },
            },
        });
        await purchaseOrderAppService.SubmitAsync(firstPo.Id);

        await WithUnitOfWorkAsync(async () =>
        {
            var bo = await blanketOrderRepo.GetAsync(blanketOrderId);
            bo.Items[0].OrderedQty.ShouldBe(100m);
        });

        // A second PO against the same line now exceeds the blanket order's allocation.
        var secondPo = await purchaseOrderAppService.CreateAsync(new CreatePurchaseOrderDto
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            OrderDate = DateTime.Today,
            Items =
            {
                new CreatePurchaseOrderItemDto
                {
                    ItemId = itemId,
                    Description = "PO Blanket Item",
                    Quantity = 1m,
                    UnitPrice = 10m,
                    BlanketOrderId = blanketOrderId,
                },
            },
        });

        await Should.ThrowAsync<BusinessException>(() => purchaseOrderAppService.SubmitAsync(secondPo.Id));

        // The blocked attempt must not have partially deducted the allocation.
        await WithUnitOfWorkAsync(async () =>
        {
            var bo = await blanketOrderRepo.GetAsync(blanketOrderId);
            bo.Items[0].OrderedQty.ShouldBe(100m);
        });

        // Cancelling the first PO releases its allocation (reverse of submit).
        await purchaseOrderAppService.CancelAsync(firstPo.Id);

        await WithUnitOfWorkAsync(async () =>
        {
            var bo = await blanketOrderRepo.GetAsync(blanketOrderId);
            bo.Items[0].OrderedQty.ShouldBe(0m);
        });
    }
}
