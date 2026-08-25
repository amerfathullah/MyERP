using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface ITerritoryAppService : IApplicationService
{
    Task<TerritoryDto> GetAsync(Guid id);
    Task<PagedResultDto<TerritoryDto>> GetListAsync(GetTerritoryListDto input);
    Task<TerritoryDto> CreateAsync(CreateUpdateTerritoryDto input);
    Task<TerritoryDto> UpdateAsync(Guid id, CreateUpdateTerritoryDto input);
    Task DeleteAsync(Guid id);
}
