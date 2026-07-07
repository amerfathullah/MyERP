using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class AccountDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string AccountCode { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public AccountType AccountType { get; set; }
    public AccountSubType? AccountSubType { get; set; }
    public Guid? ParentAccountId { get; set; }
    public bool IsGroup { get; set; }
    public string? Currency { get; set; }
    public string? Description { get; set; }
    public bool IsFrozen { get; set; }
    public bool IsActive { get; set; }
}
