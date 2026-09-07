using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IPosProfileAppService : ICrudAppService<
    PosProfileDto,
    Guid,
    GetPosProfileListDto,
    CreateUpdatePosProfileDto>
{
    Task<PosProfileDto> EnableAsync(Guid id);
    Task<PosProfileDto> DisableAsync(Guid id);
    Task<List<PosProfileDto>> GetForCurrentUserAsync(Guid companyId);
}
