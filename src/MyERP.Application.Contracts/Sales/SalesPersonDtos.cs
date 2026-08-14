using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class SalesPersonDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public Guid? ParentSalesPersonId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? EmployeeId { get; set; }
    public decimal CommissionRate { get; set; }
    public bool IsEnabled { get; set; }
    public List<SalesTargetDto> Targets { get; set; } = new();
}

public class SalesTargetDto
{
    public Guid? FiscalYearId { get; set; }
    public decimal TargetQty { get; set; }
    public decimal TargetAmount { get; set; }
}

public class CreateSalesPersonDto
{
    public string Name { get; set; } = null!;
    public Guid? ParentSalesPersonId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? EmployeeId { get; set; }
    public decimal CommissionRate { get; set; }
}

public class UpdateSalesPersonDto
{
    public Guid? ParentSalesPersonId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? EmployeeId { get; set; }
    public decimal CommissionRate { get; set; }
}

public class CreateSalesTargetDto
{
    public Guid? FiscalYearId { get; set; }
    public decimal TargetQty { get; set; }
    public decimal TargetAmount { get; set; }
}
