using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class AuthorizationRuleDto : EntityDto<Guid>
{
    public Guid? CompanyId { get; set; }
    public string TransactionType { get; set; } = null!;
    public string BasedOn { get; set; } = null!;
    public decimal ThresholdValue { get; set; }
    public Guid? SystemUserId { get; set; }
    public string? SystemRole { get; set; }
    public string? ApprovingRole { get; set; }
    public Guid? ApprovingUserId { get; set; }
    public Guid? CustomerId { get; set; }
}

public class CreateAuthorizationRuleDto
{
    public Guid? CompanyId { get; set; }
    public string TransactionType { get; set; } = null!;
    public AuthorizationBasedOn BasedOn { get; set; }
    public decimal ThresholdValue { get; set; }
    public Guid? SystemUserId { get; set; }
    public string? SystemRole { get; set; }
    public string? ApprovingRole { get; set; }
    public Guid? ApprovingUserId { get; set; }
    public Guid? CustomerId { get; set; }
}

public class UpdateAuthorizationRuleDto
{
    public decimal ThresholdValue { get; set; }
    public Guid? SystemUserId { get; set; }
    public string? SystemRole { get; set; }
    public string? ApprovingRole { get; set; }
    public Guid? ApprovingUserId { get; set; }
    public Guid? CustomerId { get; set; }
}
