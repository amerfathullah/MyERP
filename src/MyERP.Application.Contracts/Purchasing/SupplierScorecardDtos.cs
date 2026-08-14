using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing;

public class ScorecardDto : EntityDto<Guid>
{
    public Guid SupplierId { get; set; }
    public Guid CompanyId { get; set; }
    public string PeriodType { get; set; } = null!;
    public decimal Score { get; set; }
    public string? CurrentStanding { get; set; }
    public string? WeightingFunction { get; set; }
    public List<ScorecardStandingDto> Standings { get; set; } = new();
    public List<ScorecardCriterionDto> Criteria { get; set; } = new();
}

public class ScorecardStandingDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
    public bool PreventPos { get; set; }
    public bool PreventRfqs { get; set; }
    public bool WarnPos { get; set; }
    public bool WarnRfqs { get; set; }
}

public class ScorecardCriterionDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public decimal Weight { get; set; }
    public decimal MaxScore { get; set; }
    public string? Formula { get; set; }
}

public class CreateScorecardDto
{
    public Guid SupplierId { get; set; }
    public Guid CompanyId { get; set; }
    public ScorecardPeriodType PeriodType { get; set; } = ScorecardPeriodType.Monthly;
    public string? WeightingFunction { get; set; }
    public List<CreateStandingDto> Standings { get; set; } = new();
    public List<CreateCriterionDto> Criteria { get; set; } = new();
}

public class CreateStandingDto
{
    public string Name { get; set; } = null!;
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
    public bool PreventPos { get; set; }
    public bool PreventRfqs { get; set; }
    public bool WarnPos { get; set; }
    public bool WarnRfqs { get; set; }
}

public class CreateCriterionDto
{
    public string Name { get; set; } = null!;
    public decimal Weight { get; set; }
    public decimal MaxScore { get; set; }
    public string? Formula { get; set; }
}

public class CreateScorecardPeriodDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Score { get; set; }
}
