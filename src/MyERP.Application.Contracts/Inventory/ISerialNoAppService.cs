using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface ISerialNoAppService : IApplicationService
{
    Task<PagedResultDto<SerialNoDto>> GetListAsync(GetSerialNoListDto input);
    Task<SerialNoDto> GetAsync(Guid id);
}
