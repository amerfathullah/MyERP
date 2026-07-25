using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public class GetEmployeeListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
}

public interface IEmployeeAppService : IApplicationService
{
    Task<EmployeeDto> GetAsync(Guid id);
    Task<PagedResultDto<EmployeeDto>> GetListAsync(GetEmployeeListDto input);
    Task<EmployeeDto> CreateAsync(CreateUpdateEmployeeDto input);
    Task<EmployeeDto> UpdateAsync(Guid id, CreateUpdateEmployeeDto input);
    Task DeleteAsync(Guid id);
}
