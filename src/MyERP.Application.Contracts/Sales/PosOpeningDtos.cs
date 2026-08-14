using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class PosOpeningDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid PosProfileId { get; set; }
    public Guid UserId { get; set; }
    public DateTime OpeningDate { get; set; }
    public string Status { get; set; } = null!;
    public decimal TotalOpeningAmount { get; set; }
    public Guid? PosClosingEntryId { get; set; }
    public List<PosOpeningPaymentDto> Payments { get; set; } = new();
}

public class PosOpeningPaymentDto
{
    public Guid ModeOfPaymentId { get; set; }
    public string ModeName { get; set; } = null!;
    public decimal OpeningAmount { get; set; }
}

public class CreatePosOpeningDto
{
    public Guid CompanyId { get; set; }
    public Guid PosProfileId { get; set; }
    public Guid UserId { get; set; }
    public List<CreatePosOpeningPaymentDto> Payments { get; set; } = new();
}

public class CreatePosOpeningPaymentDto
{
    public Guid ModeOfPaymentId { get; set; }
    public string ModeName { get; set; } = null!;
    public decimal OpeningAmount { get; set; }
}
