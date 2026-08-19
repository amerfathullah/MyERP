using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

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
    public List<SalarySlipComponentDto> Earnings { get; set; } = new();
    public List<SalarySlipComponentDto> Deductions { get; set; } = new();
}

public class SalarySlipComponentDto : EntityDto<Guid>
{
    public Guid SalaryComponentId { get; set; }
    public string ComponentName { get; set; } = null!;
    public decimal Amount { get; set; }
    public bool IsStatutory { get; set; }
}
