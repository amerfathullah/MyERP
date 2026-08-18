using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface IDesignationAppService : IApplicationService
{
    Task<PagedResultDto<DesignationDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<DesignationDto> GetAsync(Guid id);
    Task<DesignationDto> CreateAsync(CreateUpdateDesignationDto input);
    Task<DesignationDto> UpdateAsync(Guid id, CreateUpdateDesignationDto input);
    Task DeleteAsync(Guid id);
}
