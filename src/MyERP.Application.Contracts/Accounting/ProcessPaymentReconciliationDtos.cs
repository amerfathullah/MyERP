using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class ProcessPaymentReconciliationDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public Guid ReceivablePayableAccountId { get; set; }
    public Guid? DefaultAdvanceAccountId { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = null!;
    public int ReconciledCount { get; set; }
    public string? ErrorLog { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreateProcessPaymentReconciliationDto
{
    public Guid CompanyId { get; set; }
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public Guid ReceivablePayableAccountId { get; set; }
    public Guid? DefaultAdvanceAccountId { get; set; }
}
