using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public class GetEmployeeListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
}

public class ChangeEmployeeStatusDto
{
    [Required]
    public EmploymentStatus Status { get; set; }

    /// <summary>Required when <see cref="Status"/> is <see cref="EmploymentStatus.Resigned"/>.</summary>
    public DateTime? DateOfResignation { get; set; }
}

public interface IEmployeeAppService : IApplicationService
{
    Task<EmployeeDto> GetAsync(Guid id);
    Task<PagedResultDto<EmployeeDto>> GetListAsync(GetEmployeeListDto input);
    Task<EmployeeDto> CreateAsync(CreateUpdateEmployeeDto input);
    Task<EmployeeDto> UpdateAsync(Guid id, CreateUpdateEmployeeDto input);
    Task<EmployeeDto> ChangeStatusAsync(Guid id, ChangeEmployeeStatusDto input);
    Task DeleteAsync(Guid id);
}
