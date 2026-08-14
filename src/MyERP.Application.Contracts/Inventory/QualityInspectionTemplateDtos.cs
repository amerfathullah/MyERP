using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class QiTemplateDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? BomId { get; set; }
    public bool IsEnabled { get; set; }
    public int ParameterCount { get; set; }
    public List<QiTemplateParameterDto> Parameters { get; set; } = new();
}

public class QiTemplateParameterDto : EntityDto<Guid>
{
    public string Specification { get; set; } = null!;
    public string? ExpectedValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsNumeric { get; set; }
    public bool FormulaBased { get; set; }
    public string? Formula { get; set; }
    public string? AcceptanceCriteria { get; set; }
}

public class CreateQiTemplateDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? BomId { get; set; }
    public List<CreateQiTemplateParameterDto>? Parameters { get; set; }
}

public class CreateQiTemplateParameterDto
{
    public string Specification { get; set; } = null!;
    public string? ExpectedValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsNumeric { get; set; }
    public bool FormulaBased { get; set; }
    public string? Formula { get; set; }
    public string? AcceptanceCriteria { get; set; }
}
