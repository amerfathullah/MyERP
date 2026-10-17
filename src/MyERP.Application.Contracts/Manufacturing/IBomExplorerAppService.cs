using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IBomExplorerAppService : IApplicationService
{
    Task<BomExplorerDto> GetExplorerAsync(BomExplorerRequestDto input);
    Task<List<BomLookupDto>> GetBomListAsync(Guid companyId);
}
