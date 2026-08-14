using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class AccountingPeriodDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string PeriodName { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
}
