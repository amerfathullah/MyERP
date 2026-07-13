using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

public class SalarySlipDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetAmount { get; set; }
    public int Status { get; set; }
}

[Authorize(MyERPPermissions.Payroll.Default)]
public class SalarySlipAppService : ApplicationService
{
    private readonly IRepository<SalarySlip, Guid> _repository;
    public SalarySlipAppService(IRepository<SalarySlip, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<SalarySlipDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderByDescending(s => s.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<SalarySlipDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<SalarySlipDto> GetAsync(Guid id) => MapToDto(await _repository.GetAsync(id));

    private static SalarySlipDto MapToDto(SalarySlip s) => new()
    {
        Id = s.Id, CompanyId = s.CompanyId, EmployeeId = s.EmployeeId,
        EmployeeName = s.EmployeeName, PostingDate = s.PostingDate,
        StartDate = s.StartDate, EndDate = s.EndDate,
        GrossAmount = s.GrossAmount, TotalDeductions = s.TotalDeductions,
        NetAmount = s.NetAmount, Status = (int)s.Status,
    };
}
