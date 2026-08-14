using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IInventoryTurnoverAppService : IApplicationService
{
    Task<InventoryTurnoverReportDto> GetReportAsync(
        Guid companyId, DateTime fromDate, DateTime toDate, Guid? warehouseId = null);
}
