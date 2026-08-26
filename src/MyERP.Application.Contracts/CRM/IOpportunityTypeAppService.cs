using System;
using Volo.Abp.Application.Services;

namespace MyERP.CRM;

public interface IOpportunityTypeAppService : ICrudAppService<OpportunityTypeDto, Guid, GetOpportunityTypeListDto, CreateUpdateOpportunityTypeDto, CreateUpdateOpportunityTypeDto>
{
}
