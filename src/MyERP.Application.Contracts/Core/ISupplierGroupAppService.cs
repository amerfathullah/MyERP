using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface ISupplierGroupAppService : IApplicationService
{
    Task<SupplierGroupDto> GetAsync(Guid id);
    Task<PagedResultDto<SupplierGroupDto>> GetListAsync(GetSupplierGroupListDto input);
    Task<SupplierGroupDto> CreateAsync(CreateUpdateSupplierGroupDto input);
    Task<SupplierGroupDto> UpdateAsync(Guid id, CreateUpdateSupplierGroupDto input);
    Task DeleteAsync(Guid id);
}
