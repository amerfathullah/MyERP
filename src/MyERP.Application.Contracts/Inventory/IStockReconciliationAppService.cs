using System;
using System.Threading.Tasks;
using MyERP.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockReconciliationAppService : IApplicationService
{
    Task<PagedResultDto<StockReconciliationDto>> GetListAsync(GetStockReconciliationListDto input);
    Task<StockReconciliationDto> GetAsync(Guid id);
    Task<StockReconciliationDto> CreateAsync(CreateStockReconciliationDto input);
    Task<StockReconciliationDto> SubmitAsync(Guid id);
    Task<StockReconciliationDto> CancelAsync(Guid id);
}

