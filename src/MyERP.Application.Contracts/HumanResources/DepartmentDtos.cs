using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class DepartmentDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateDepartmentDto
{
    public string Name { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
    public bool IsActive { get; set; } = true;
}
