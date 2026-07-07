using System;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Accounting;

public class CreateUpdateAccountDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(AccountConsts.MaxAccountCodeLength)]
    public string AccountCode { get; set; } = null!;

    [Required]
    [StringLength(AccountConsts.MaxAccountNameLength)]
    public string AccountName { get; set; } = null!;

    [Required]
    public AccountType AccountType { get; set; }

    public AccountSubType? AccountSubType { get; set; }

    public Guid? ParentAccountId { get; set; }

    public bool IsGroup { get; set; }

    [StringLength(AccountConsts.MaxCurrencyLength)]
    public string? Currency { get; set; }

    [StringLength(AccountConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsFrozen { get; set; }

    public bool IsActive { get; set; } = true;
}
