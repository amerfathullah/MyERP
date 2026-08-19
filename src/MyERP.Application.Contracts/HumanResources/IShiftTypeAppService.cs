using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface IShiftTypeAppService : IApplicationService
{
    Task<ShiftTypeDto> GetAsync(Guid id);
    Task<PagedResultDto<ShiftTypeDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<ShiftTypeDto> CreateAsync(CreateShiftTypeDto input);
    Task<ShiftTypeDto> UpdateAsync(Guid id, CreateShiftTypeDto input);
    Task DeleteAsync(Guid id);
}
