using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockValuationSummaryAppService : IApplicationService
{
    Task<StockValuationSummaryDto> GetSummaryAsync(Guid companyId, Guid? warehouseId = null);
}
