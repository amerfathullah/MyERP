using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemAttributeAppService : IApplicationService
{
    Task<ItemAttributeDto> GetAsync(Guid id);
    Task<List<ItemAttributeDto>> GetListAsync();
    Task<ItemAttributeDto> CreateAsync(CreateItemAttributeDto input);
    Task<ItemAttributeDto> AddValueAsync(Guid id, ItemAttributeValueDto input);
    Task DeleteAsync(Guid id);
}
