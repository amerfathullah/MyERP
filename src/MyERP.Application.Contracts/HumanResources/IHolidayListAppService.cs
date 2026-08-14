using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface IHolidayListAppService : IApplicationService
{
    Task<PagedResultDto<HolidayListDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<HolidayListDto> GetAsync(Guid id);
    Task<HolidayListDto> CreateAsync(CreateHolidayListDto input);
    Task DeleteAsync(Guid id);
}
