using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface ISerialAndBatchBundleAppService : IApplicationService
{
    Task<PagedResultDto<SerialAndBatchBundleDto>> GetListAsync(GetBundleListDto input);
    Task<SerialAndBatchBundleDto> GetAsync(Guid id);
}
