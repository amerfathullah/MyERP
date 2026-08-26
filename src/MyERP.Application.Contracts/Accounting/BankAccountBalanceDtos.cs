using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class BankAccountBalanceDto : FullAuditedEntityDto<Guid>
{
    public Guid BankAccountId { get; set; }
    public string? BankAccountName { get; set; }
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public DateTime Date { get; set; }
    public decimal Balance { get; set; }
}

public class CreateUpdateBankAccountBalanceDto
{
    [Required]
    public Guid BankAccountId { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Required]
    public decimal Balance { get; set; }
}

public class GetBankAccountBalanceListDto : PagedAndSortedResultRequestDto
{
    public Guid? BankAccountId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
