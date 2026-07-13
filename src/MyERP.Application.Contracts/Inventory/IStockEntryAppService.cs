using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockEntryAppService : IApplicationService
{
    Task<StockEntryDto> GetAsync(Guid id);
    Task<PagedResultDto<StockEntryDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<StockEntryDto> CreateAsync(CreateStockEntryDto input);
    Task<StockEntryDto> SubmitAsync(Guid id);
    Task<StockEntryDto> PostAsync(Guid id);
    Task<StockEntryDto> CancelAsync(Guid id);
}
