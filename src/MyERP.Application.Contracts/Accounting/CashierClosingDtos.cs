using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class CashierClosingPaymentDto : CreationAuditedEntityDto<Guid>
{
    public Guid CashierClosingId { get; set; }
    public string ModeOfPayment { get; set; } = null!;
    public decimal Amount { get; set; }
}

public class CreateUpdateCashierClosingPaymentDto
{
    [Required]
    [StringLength(CashierClosingConsts.MaxModeOfPaymentLength)]
    public string ModeOfPayment { get; set; } = null!;

    public decimal Amount { get; set; }
}

public class CashierClosingDto : FullAuditedEntityDto<Guid>
{
    public string ClosingNumber { get; set; } = null!;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = null!;
    public DateTime Date { get; set; }
    public TimeSpan FromTime { get; set; }
    public TimeSpan ToTime { get; set; }
    public decimal Expense { get; set; }
    public decimal Custody { get; set; }
    public decimal Returns { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal NetAmount { get; set; }
    public bool IsSubmitted { get; set; }

    public List<CashierClosingPaymentDto> Payments { get; set; } = new();
}

public class CreateCashierClosingDto
{
    [Required]
    public DateTime Date { get; set; }

    [Required]
    public TimeSpan FromTime { get; set; }

    [Required]
    public TimeSpan ToTime { get; set; }

    public decimal Expense { get; set; }
    public decimal Custody { get; set; }
    public decimal Returns { get; set; }

    public List<CreateUpdateCashierClosingPaymentDto> Payments { get; set; } = new();
}

public class UpdateCashierClosingDto
{
    [Required]
    public DateTime Date { get; set; }

    [Required]
    public TimeSpan FromTime { get; set; }

    [Required]
    public TimeSpan ToTime { get; set; }

    public decimal Expense { get; set; }
    public decimal Custody { get; set; }
    public decimal Returns { get; set; }

    public List<CreateUpdateCashierClosingPaymentDto> Payments { get; set; } = new();
}

public class CashierClosingGetListInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? UserId { get; set; }
}

public class CalculateCashierClosingTotalsRequestDto
{
    public DateTime Date { get; set; }
    public TimeSpan FromTime { get; set; }
    public TimeSpan ToTime { get; set; }
    public Guid? UserId { get; set; }
}

public class CalculateCashierClosingTotalsResponseDto
{
    public decimal OutstandingAmount { get; set; }
    public List<CreateUpdateCashierClosingPaymentDto> SuggestedPayments { get; set; } = new();
}
