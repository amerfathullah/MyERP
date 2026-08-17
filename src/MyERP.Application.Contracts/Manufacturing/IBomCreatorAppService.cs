using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IBomCreatorAppService : IApplicationService
{
    Task<PagedResultDto<BomCreatorDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<BomCreatorDto> GetAsync(Guid id);
    Task<BomCreatorDto> CreateAsync(CreateUpdateBomCreatorDto input);
    Task<BomCreatorDto> UpdateAsync(Guid id, CreateUpdateBomCreatorDto input);
    Task DeleteAsync(Guid id);

    /// <summary>Processes the draft tree, generating one BOM per expandable item.</summary>
    Task<BomCreatorDto> CreateBomsAsync(Guid id);
}
