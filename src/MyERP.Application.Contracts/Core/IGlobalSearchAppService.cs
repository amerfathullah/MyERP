using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IGlobalSearchAppService : IApplicationService
{
    Task<List<SearchResultDto>> SearchAsync(GlobalSearchInput input);
}
