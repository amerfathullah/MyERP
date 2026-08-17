using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class UnreconcilePaymentAllocationDto
{
    public Guid Id { get; set; }
    public Guid PaymentLedgerEntryId { get; set; }
    public string AgainstVoucherType { get; set; } = null!;
    public Guid AgainstVoucherId { get; set; }
    public decimal Amount { get; set; }
    public bool Unlinked { get; set; }
}

public class UnreconcilePaymentDto : AuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public UnreconcileVoucherType VoucherType { get; set; }
    public Guid VoucherId { get; set; }
    public int Status { get; set; }
    public List<UnreconcilePaymentAllocationDto> Allocations { get; set; } = new();
}

public class CreateUnreconcilePaymentDto
{
    [Required] public Guid CompanyId { get; set; }
    public UnreconcileVoucherType VoucherType { get; set; }
    [Required] public Guid VoucherId { get; set; }
}

public class GetUnreconcilePaymentListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
}
