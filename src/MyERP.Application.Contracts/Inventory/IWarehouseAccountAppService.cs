using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IWarehouseAccountAppService : IApplicationService
{
    Task<ListResultDto<WarehouseAccountDto>> GetListAsync(Guid companyId);
    Task<WarehouseAccountDto> SaveAsync(CreateWarehouseAccountDto input);
    Task DeleteAsync(Guid id);
}
