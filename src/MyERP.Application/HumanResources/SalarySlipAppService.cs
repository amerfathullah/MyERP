using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using MyERP.Shared;
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

    public async Task<PagedResultDto<SalarySlipDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
             query = query.Where(x => x.EmployeeName != null && x.EmployeeName.ToLower().Contains(filter.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(s => s.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<SalarySlipDto>(totalCount, items.Select(x => ObjectMapper.Map<SalarySlip, SalarySlipDto>(x)).ToList());
    }

    public async Task<SalarySlipDto> GetAsync(Guid id) => ObjectMapper.Map<SalarySlip, SalarySlipDto>(await _repository.GetAsync(id));
}

