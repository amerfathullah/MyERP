using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IBomStockAnalysisAppService : IApplicationService
{
    Task<BomStockAnalysisDto> GetAnalysisAsync(Guid bomId, decimal requiredQty = 1);
}
