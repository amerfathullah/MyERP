using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class PartySpecificItemDto : EntityDto<Guid>
{
    public PartySpecificItemPartyType PartyType { get; set; }
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public PartySpecificItemRestrictBasedOn RestrictBasedOn { get; set; }
    public Guid BasedOnValueId { get; set; }
    public string? BasedOnValueName { get; set; }
}

public class CreateUpdatePartySpecificItemDto
{
    public PartySpecificItemPartyType PartyType { get; set; }
    public Guid PartyId { get; set; }
    public PartySpecificItemRestrictBasedOn RestrictBasedOn { get; set; }
    public Guid BasedOnValueId { get; set; }
}

public class GetPartySpecificItemListDto : PagedAndSortedResultRequestDto
{
    public PartySpecificItemPartyType? PartyType { get; set; }
    public Guid? PartyId { get; set; }
}
