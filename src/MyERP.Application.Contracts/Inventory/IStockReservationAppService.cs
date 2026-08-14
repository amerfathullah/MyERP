using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockReservationAppService : IApplicationService
{
    Task<PagedResultDto<StockReservationEntryDto>> GetListAsync(GetStockReservationListDto input);
    Task<StockReservationEntryDto> GetAsync(Guid id);
    Task<StockReservationEntryDto> CreateAsync(CreateStockReservationDto input);
    Task<StockReservationEntryDto> CancelAsync(Guid id);
    Task<decimal> GetReservedQtyAsync(Guid itemId, Guid warehouseId);
    Task CancelForOrderAsync(Guid salesOrderId);
}
