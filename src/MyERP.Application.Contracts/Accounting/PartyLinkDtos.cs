using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class PartyLinkDto : EntityDto<Guid>
{
    public string PrimaryPartyType { get; set; } = null!;
    public Guid PrimaryPartyId { get; set; }
    public string SecondaryPartyType { get; set; } = null!;
    public Guid SecondaryPartyId { get; set; }
}

public class CreatePartyLinkDto
{
    public string PrimaryPartyType { get; set; } = null!;
    public Guid PrimaryPartyId { get; set; }
    public string SecondaryPartyType { get; set; } = null!;
    public Guid SecondaryPartyId { get; set; }
}
