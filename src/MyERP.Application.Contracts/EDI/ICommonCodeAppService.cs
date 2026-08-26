using System;
using Volo.Abp.Application.Services;

namespace MyERP.EDI;

public interface ICommonCodeAppService : ICrudAppService<CommonCodeDto, Guid, GetCommonCodeListDto, CreateUpdateCommonCodeDto, CreateUpdateCommonCodeDto>
{
}
