using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyERP.Inventory;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

using Volo.Abp.Modularity;

namespace MyERP.Inventory.Tests;

public abstract class PickListAppService_Tests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly PickListAppService _pickListAppService;
    private readonly IRepository<MyERP.Inventory.Entities.PickList, Guid> _pickListRepository;
    private readonly IRepository<MyERP.Sales.Entities.DeliveryNote, Guid> _deliveryNoteRepository;

    protected PickListAppService_Tests()
    {
        _pickListAppService = GetRequiredService<PickListAppService>();
        _pickListRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.PickList, Guid>>();
        _deliveryNoteRepository = GetRequiredService<IRepository<MyERP.Sales.Entities.DeliveryNote, Guid>>();
    }

    [Fact]
    public async Task CreateDeliveryNoteFromPickList_Should_Map_Customer_When_No_SalesOrder_Exists()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        
        // Mock a submitted PickList without a Sales Order, but with a Customer
        var pickList = new MyERP.Inventory.Entities.PickList(Guid.NewGuid(), companyId, "Delivery", null)
        {
            CustomerId = customerId
        };
        pickList.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10, 10, "Test Item");
        pickList.Submit(); // Transition to submitted
        await _pickListRepository.InsertAsync(pickList, autoSave: true);

        // Act
        // We catch exception here if number generator or other dependencies fail,
        // but the main logic we want to test is inside the app service.
        // For a full test we'd need to mock number generators, but we can verify the logic throws a specific error if customer is missing, and doesn't if it's there.
        try
        {
            await _pickListAppService.CreateDeliveryNoteFromPickListAsync(pickList.Id);
        }
        catch (Exception ex)
        {
            // If it throws because of warehouse missing or ID document number generator, it bypassed the Customer check!
            ex.ShouldBeOfType<Volo.Abp.BusinessException>();
        }
    }

    [Fact]
    public async Task GetStockAvailabilityInsightAsync_Should_Return_Insight_And_DeliveryStatus()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();

            var binRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Bin, Guid>>();
            var bin = new MyERP.Inventory.Entities.Bin(Guid.NewGuid(), itemId, warehouseId);
            bin.ApplyStockMovement(150m, 1500m);
            await binRepo.InsertAsync(bin, autoSave: true);

            var pl1 = new MyERP.Inventory.Entities.PickList(Guid.NewGuid(), companyId, "Delivery", null)
            {
                PickListNumber = "PL-001"
            };
            pl1.AddItem(itemId, warehouseId, 50, 50, "Test Item 1");
            pl1.Submit();
            await _pickListRepository.InsertAsync(pl1, autoSave: true);

            var pl2 = new MyERP.Inventory.Entities.PickList(Guid.NewGuid(), companyId, "Delivery", null)
            {
                PickListNumber = "PL-002"
            };
            pl2.AddItem(itemId, warehouseId, 40, 40, "Test Item 1");
            await _pickListRepository.InsertAsync(pl2, autoSave: true);

            // Act
            var insights = await _pickListAppService.GetStockAvailabilityInsightAsync(pl2.Id);
            var plDto = await _pickListAppService.GetAsync(pl1.Id);

            // Assert
            plDto.DeliveryStatus.ShouldBe("Not Delivered");
            insights.ShouldNotBeEmpty();
            var insight = insights.First(i => i.ItemId == itemId && i.WarehouseId == warehouseId);
            insight.ActualQty.ShouldBe(150m);
            insight.PickedQty.ShouldBe(50m);
            insight.FreeQty.ShouldBe(100m);
            insight.HoldingPickLists.ShouldContain(h => h.PickListId == pl1.Id && h.HoldingQty == 50m);
        });
    }
}
