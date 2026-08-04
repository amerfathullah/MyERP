using System.Threading.Tasks;
using MyERP.Inventory.DomainServices;
using MyERP.Purchasing.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;

namespace MyERP.Inventory.EventHandlers;

public class SubcontractingOrderClosedEventHandler : ILocalEventHandler<SubcontractingOrderClosedOrCancelledEvent>, ITransientDependency
{
    private readonly BinService _binService;
    private readonly IRepository<MyERP.Purchasing.Entities.SubcontractingOrder, System.Guid> _scoRepository;

    public SubcontractingOrderClosedEventHandler(
        BinService binService,
        IRepository<MyERP.Purchasing.Entities.SubcontractingOrder, System.Guid> scoRepository)
    {
        _binService = binService;
        _scoRepository = scoRepository;
    }

    public async Task HandleEventAsync(SubcontractingOrderClosedOrCancelledEvent eventData)
    {
        // When SCO is closed or cancelled, release reservations for all its supplied items.
        // Get the SCO with its supplied items collection (Requires eager loading if it's navigation prop)
        // Since SubcontractingOrder is an aggregate root, its items are loaded with it.
        var sco = await _scoRepository.GetAsync(eventData.SubcontractingOrderId);

        // Subcontracting reserves raw materials from their original warehouse (ReserveWarehouseId).
        // The reserved qty is `RequiredQty - TransferredQty`. If the order is closed/cancelled,
        // we must release this remaining reservation.
        foreach (var suppliedItem in sco.SuppliedItems)
        {
            if (suppliedItem.ReserveWarehouseId.HasValue)
            {
                var remainingToTransfer = System.Math.Max(0, suppliedItem.RequiredQty - suppliedItem.TransferredQty);
                if (remainingToTransfer > 0)
                {
                    // Negative qty change to release the reservation
                    await _binService.UpdateReservedQtyForSubContractAsync(
                        suppliedItem.ItemId, 
                        suppliedItem.ReserveWarehouseId.Value, 
                        -remainingToTransfer, 
                        eventData.TenantId);
                }
            }
        }
    }
}
