using System;
using Volo.Abp.Application.Services;

namespace MyERP.EDI;

public interface ICodeListAppService : ICrudAppService<CodeListDto, Guid, GetCodeListListDto, CreateUpdateCodeListDto, CreateUpdateCodeListDto>
{
}
