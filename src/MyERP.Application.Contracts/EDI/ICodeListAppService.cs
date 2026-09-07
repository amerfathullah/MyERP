using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.EDI;

public interface ICodeListAppService : ICrudAppService<CodeListDto, Guid, GetCodeListListDto, CreateUpdateCodeListDto, CreateUpdateCodeListDto>
{
    Task<CodeListDto?> ResolveAsync(string identifier);
    Task<string?> GetDefaultCodeAsync(string identifier);
}
