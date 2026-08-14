using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockClosingAppService : IApplicationService
{
    Task<PagedResultDto<StockClosingEntryDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<StockClosingEntryDto> GetAsync(Guid id);
    Task<StockClosingEntryDto> GenerateAsync(CreateStockClosingDto input);
    Task<StockClosingEntryDto> SubmitAsync(Guid id);
    Task<StockClosingEntryDto> CancelAsync(Guid id);
}
