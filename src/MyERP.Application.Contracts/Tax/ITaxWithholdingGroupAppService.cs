using System;
using Volo.Abp.Application.Services;

namespace MyERP.Tax;

public interface ITaxWithholdingGroupAppService : ICrudAppService<TaxWithholdingGroupDto, Guid, GetTaxWithholdingGroupListDto, CreateUpdateTaxWithholdingGroupDto, CreateUpdateTaxWithholdingGroupDto>
{
}
