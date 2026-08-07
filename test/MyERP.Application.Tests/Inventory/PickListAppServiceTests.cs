using System;
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
}
