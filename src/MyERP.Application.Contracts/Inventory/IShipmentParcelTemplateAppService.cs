using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IShipmentParcelTemplateAppService : IApplicationService
{
    Task<ShipmentParcelTemplateDto> GetAsync(Guid id);
    Task<PagedResultDto<ShipmentParcelTemplateDto>> GetListAsync(GetShipmentParcelTemplateListDto input);
    Task<List<ShipmentParcelTemplateDto>> GetAllListAsync();
    Task<ShipmentParcelTemplateDto> CreateAsync(CreateUpdateShipmentParcelTemplateDto input);
    Task<ShipmentParcelTemplateDto> UpdateAsync(Guid id, CreateUpdateShipmentParcelTemplateDto input);
    Task DeleteAsync(Guid id);
}
