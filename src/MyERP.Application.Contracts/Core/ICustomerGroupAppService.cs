using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface ICustomerGroupAppService : IApplicationService
{
    Task<CustomerGroupDto> GetAsync(Guid id);
    Task<PagedResultDto<CustomerGroupDto>> GetListAsync(GetCustomerGroupListDto input);
    Task<CustomerGroupDto> CreateAsync(CreateUpdateCustomerGroupDto input);
    Task<CustomerGroupDto> UpdateAsync(Guid id, CreateUpdateCustomerGroupDto input);
    Task DeleteAsync(Guid id);
}
