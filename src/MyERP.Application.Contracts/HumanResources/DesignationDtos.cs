using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class DesignationDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

public class CreateUpdateDesignationDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}
