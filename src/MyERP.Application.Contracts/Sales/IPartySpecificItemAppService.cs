using System;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IPartySpecificItemAppService :
    ICrudAppService<
        PartySpecificItemDto,
        Guid,
        GetPartySpecificItemListDto,
        CreateUpdatePartySpecificItemDto>
{
}
