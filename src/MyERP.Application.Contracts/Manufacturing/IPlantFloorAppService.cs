using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IPlantFloorAppService : IApplicationService
{
    Task<PlantFloorDto> GetAsync(Guid id);
    Task<PagedResultDto<PlantFloorDto>> GetListAsync(GetPlantFloorListDto input);
    Task<List<PlantFloorDto>> GetAllListAsync(Guid companyId);
    Task<PlantFloorDto> CreateAsync(CreateUpdatePlantFloorDto input);
    Task<PlantFloorDto> UpdateAsync(Guid id, CreateUpdatePlantFloorDto input);
    Task DeleteAsync(Guid id);
}
